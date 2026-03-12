using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Collections;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Detects valid mantle and vault opportunities when player attempts to jump near obstacles.
    /// Uses raycasts to find ledges and validates dimensions before adding MantleCandidate component.
    /// Runs in predicted simulation for client/server consistency.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerStateSystem))]
    public partial struct MantleDetectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (playerState, playerInput, transform, mantleState, entity) in
                     SystemAPI.Query<RefRO<PlayerState>, RefRO<PlayerInput>, RefRO<LocalTransform>, RefRO<MantleState>>()
                         .WithAll<Simulate>()
                         .WithEntityAccess())
            {
                var pState = playerState.ValueRO;
                var input = playerInput.ValueRO;
                var pos = transform.ValueRO.Position;
                var mState = mantleState.ValueRO;
                
                // Skip if already mantling or on cooldown
                if (mState.IsActive > 0 || mState.CooldownRemaining > 0f)
                    continue;
                
                // Skip if already climbing or in other special states
                // Check explicit FreeClimbState if present
                if (SystemAPI.HasComponent<FreeClimbState>(entity))
                {
                    if (SystemAPI.GetComponent<FreeClimbState>(entity).IsClimbing)
                        continue;
                }

                if (pState.MovementState == PlayerMovementState.Climbing ||
                    pState.MovementState == PlayerMovementState.Rolling ||
                    pState.MovementState == PlayerMovementState.Diving)
                    continue;
                
                // Get settings
                var settings = MantleSettings.Default;
                if (SystemAPI.HasComponent<MantleSettings>(entity))
                {
                    settings = SystemAPI.GetComponent<MantleSettings>(entity);
                }
                
                bool shouldDetect = false;
                bool isVaultAttempt = false;
                
                // Detect mantle opportunity on jump input
                if (input.Jump.IsSet && !pState.IsGrounded)
                {
                    shouldDetect = true;
                    isVaultAttempt = false;
                }
                
                // Detect vault opportunity when sprinting + jumping near obstacle
                if (input.Jump.IsSet && pState.MovementState == PlayerMovementState.Sprinting)
                {
                    shouldDetect = true;
                    isVaultAttempt = true;
                }
                
                if (!shouldDetect)
                    continue;
                
                // Already has a candidate, don't re-detect
                if (SystemAPI.HasComponent<MantleCandidate>(entity))
                    continue;
                
                // Determine max height based on stance and action type
                float maxHeight = isVaultAttempt ? settings.MaxVaultHeight :
                                  (pState.Stance == PlayerStance.Crouching ? settings.MaxMantleHeightCrouching : settings.MaxMantleHeightStanding);
                
                // Get player forward direction (assumes rotation around Y axis)
                var rotation = transform.ValueRO.Rotation;
                var forward = math.mul(rotation, new float3(0, 0, 1));
                forward.y = 0;
                forward = math.normalize(forward);
                
                // Perform detection raycast sequence
                if (TryDetectLedge(ref physicsWorld, pos, forward, maxHeight, settings, isVaultAttempt,
                    out var ledgePos, out var ledgeNormal, out var ledgeHeight, out var ledgeWidth))
                {
                    // Valid ledge found, add candidate component
                    var candidate = new MantleCandidate
                    {
                        LedgePosition = ledgePos,
                        LedgeNormal = ledgeNormal,
                        LedgeHeight = ledgeHeight,
                        LedgeWidth = ledgeWidth,
                        IsVault = isVaultAttempt
                    };
                    
                    ecb.AddComponent(entity, candidate);
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        /// <summary>
        /// Attempts to detect a valid ledge for mantling/vaulting using raycasts.
        /// Returns true if a valid ledge is found.
        /// </summary>
        private static bool TryDetectLedge(
            ref PhysicsWorld physicsWorld,
            float3 playerPos,
            float3 forward,
            float maxHeight,
            MantleSettings settings,
            bool isVault,
            out float3 ledgePos,
            out float3 ledgeNormal,
            out float ledgeHeight,
            out float ledgeWidth)
        {
            ledgePos = float3.zero;
            ledgeNormal = float3.zero;
            ledgeHeight = 0f;
            ledgeWidth = 0f;
            
            // Cast forward to detect obstacle
            var forwardRayStart = playerPos + new float3(0, 0.5f, 0); // Chest height
            var forwardRayEnd = forwardRayStart + forward * settings.MantleReachDistance;
            
            var forwardRayInput = new RaycastInput
            {
                Start = forwardRayStart,
                End = forwardRayEnd,
                Filter = CollisionFilter.Default
            };
            
            if (!physicsWorld.CastRay(forwardRayInput, out var forwardHit))
                return false; // No obstacle in front
            
            // Check obstacle height - cast down from above to find top surface
            var obstaclePoint = forwardHit.Position;
            float checkHeight = playerPos.y + maxHeight + 0.2f; // Slightly above max
            var downRayStart = new float3(obstaclePoint.x, checkHeight, obstaclePoint.z);
            var downRayEnd = downRayStart + new float3(0, -maxHeight - 0.3f, 0);
            
            var downRayInput = new RaycastInput
            {
                Start = downRayStart,
                End = downRayEnd,
                Filter = CollisionFilter.Default
            };
            
            if (!physicsWorld.CastRay(downRayInput, out var topHit))
                return false; // No top surface found
            
            // Calculate ledge height relative to player
            ledgeHeight = topHit.Position.y - playerPos.y;
            
            // Validate height is within range
            if (ledgeHeight < 0.3f || ledgeHeight > maxHeight)
                return false;
            
            ledgePos = topHit.Position;
            ledgeNormal = topHit.SurfaceNormal;
            
            // Validate surface is relatively flat (normal pointing up)
            if (ledgeNormal.y < 0.7f)
                return false; // Too steep
            
            // Check ledge width by casting left and right
            var right = math.cross(new float3(0, 1, 0), forward);
            float widthCheckDist = settings.MinLedgeWidth * 0.5f;
            
            // Cast left
            var leftRayStart = ledgePos + right * (-widthCheckDist);
            var leftRayEnd = leftRayStart + new float3(0, -0.5f, 0);
            var leftRayInput = new RaycastInput
            {
                Start = leftRayStart,
                End = leftRayEnd,
                Filter = CollisionFilter.Default
            };
            
            bool leftValid = physicsWorld.CastRay(leftRayInput, out var leftHit) &&
                            math.abs(leftHit.Position.y - ledgePos.y) < 0.1f;
            
            // Cast right
            var rightRayStart = ledgePos + right * widthCheckDist;
            var rightRayEnd = rightRayStart + new float3(0, -0.5f, 0);
            var rightRayInput = new RaycastInput
            {
                Start = rightRayStart,
                End = rightRayEnd,
                Filter = CollisionFilter.Default
            };
            
            bool rightValid = physicsWorld.CastRay(rightRayInput, out var rightHit) &&
                             math.abs(rightHit.Position.y - ledgePos.y) < 0.1f;
            
            if (!leftValid || !rightValid)
                return false; // Ledge too narrow
            
            ledgeWidth = widthCheckDist * 2f;
            
            // Additional check: ensure there's space above ledge (no ceiling blocking)
            var ceilingCheckStart = ledgePos + new float3(0, 0.1f, 0);
            var ceilingCheckEnd = ceilingCheckStart + new float3(0, 2.0f, 0); // Check 2m above
            var ceilingRayInput = new RaycastInput
            {
                Start = ceilingCheckStart,
                End = ceilingCheckEnd,
                Filter = CollisionFilter.Default
            };
            
            if (physicsWorld.CastRay(ceilingRayInput, out var ceilingHit))
            {
                // Check if ceiling is too low
                if (ceilingHit.Position.y - ledgePos.y < 1.5f)
                    return false; // Not enough clearance
            }
            
            return true;
        }
    }
}
