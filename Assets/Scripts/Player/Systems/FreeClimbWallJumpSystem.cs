using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;
using DIG.Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Handles wall jumping (surface-to-surface leap) while climbing.
    /// 
    /// Trigger: Player presses Jump while climbing with directional input.
    /// Algorithm:
    /// 1. Calculate jump direction from input and surface orientation
    /// 2. Validate path is not obstructed
    /// 3. Find target surface via multi-phase raycasting
    /// 4. If valid: start wall jump transition (lerp to target)
    /// 5. If no valid surface + lateral input: trigger dismount
    /// 
    /// Update Order: After FreeClimbMovementSystem, Before FreeClimbLedgeSystem
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    // [BurstCompile]
    public partial struct FreeClimbWallJumpSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }
        
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var currentTime = SystemAPI.Time.ElapsedTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var isServer = state.WorldUnmanaged.IsServer();
            
            new WallJumpJob
            {
                DeltaTime = deltaTime,
                CurrentTime = currentTime,
                PhysicsWorld = physicsWorld.PhysicsWorld,
                LocalTransformLookup = localTransformLookup,
                IsServer = isServer
            }.ScheduleParallel();
        }

        // [BurstCompile]
        private partial struct WallJumpJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public double CurrentTime;
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] 
            [NativeDisableContainerSafetyRestriction]
            public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public bool IsServer;

            private void Execute(
                Entity entity, 
                ref FreeClimbState climb, 
                RefRO<FreeClimbSettings> settings, 
                RefRO<PlayerInput> input, 
                ref LocalTransform lt, 
                ref PhysicsVelocity vel)
            {
                var cfg = settings.ValueRO;
                var playerInput = input.ValueRO;
                
                // ========== Handle ongoing wall jump transition (both client and server) ==========
                if (climb.IsWallJumping)
                {
                    ProcessWallJumpTransition(ref climb, ref lt, ref vel, cfg);
                    return;
                }
                
                // SERVER-ONLY: Only server initiates new wall jumps
                if (!IsServer)
                    return;
                
                // ========== Check for new wall jump initiation ==========
                // Skip if not climbing or in other transitions
                if (!climb.IsClimbing || climb.IsTransitioning || climb.IsClimbingUp)
                    return;
                
                // Require jump input
                if (!playerInput.Jump.IsSet)
                    return;
                
                // Require directional input above threshold
                float inputMagnitude = math.length(new float2(playerInput.Horizontal, playerInput.Vertical));
                if (inputMagnitude < cfg.WallJumpInputThreshold)
                    return;
                
                // Calculate jump direction in world space
                float3 worldUp = new float3(0, 1, 0);
                float3 surfaceNormal = climb.GripWorldNormal;
                
                // Calculate surface-relative right vector
                float3 surfaceRight = math.cross(worldUp, surfaceNormal);
                if (math.lengthsq(surfaceRight) < 0.001f)
                {
                    // Surface is nearly horizontal (floor/ceiling), use player forward
                    surfaceRight = math.cross(lt.Forward(), surfaceNormal);
                }
                surfaceRight = math.normalizesafe(surfaceRight);
                
                // Jump direction: combination of horizontal (surface-relative) and vertical (world up)
                float3 jumpDir = surfaceRight * playerInput.Horizontal + worldUp * playerInput.Vertical;
                jumpDir = math.normalizesafe(jumpDir);
                
                // Calculate target search position
                float3 handPos = climb.GripWorldPosition;
                
                // Safety: Guard against NaN
                if (!math.all(math.isfinite(handPos)) || !math.all(math.isfinite(jumpDir))) return;

                float3 targetSearchPos = handPos + jumpDir * cfg.WallJumpMaxDistance;
                
                // ========== Obstruction Check ==========
                if (IsPathObstructed(handPos, targetSearchPos, surfaceNormal, cfg, entity))
                    return; // Path is blocked
                
                // ========== Find Target Surface ==========
                if (TryFindTargetSurface(handPos, targetSearchPos, surfaceNormal, jumpDir, cfg, 
                    out float3 hitPos, out float3 hitNormal, out Entity hitEntity))
                {
                    // Valid surface found - start wall jump
                    StartWallJump(ref climb, ref lt, hitPos, hitNormal, hitEntity, cfg);
                }
                else
                {
                    // No surface found - check if lateral input should trigger dismount
                    if (math.abs(playerInput.Horizontal) > 0.5f && math.abs(playerInput.Vertical) < 0.3f)
                    {
                        // Lateral jump with no target = dismount (jump off wall)
                         UnityEngine.Debug.Log($"[CLIMB_ABORT] Wall Jump Failed (No Target + Lateral Input) -> Dismounting. H:{playerInput.Horizontal} V:{playerInput.Vertical}");
                        TriggerDismount(ref climb, ref lt, ref vel, jumpDir, cfg, CurrentTime);
                    }
                    // Otherwise: do nothing (invalid jump attempt)
                }
            }
            
            private void ProcessWallJumpTransition(
                ref FreeClimbState climb, 
                ref LocalTransform lt, 
                ref PhysicsVelocity vel,
                FreeClimbSettings cfg)
            {
                // Advance transition
                climb.WallJumpProgress += DeltaTime * cfg.WallJumpSpeed;
                float t = math.saturate(climb.WallJumpProgress);
                
                // Ease-out for smoother landing
                float easedT = EaseOutQuad(t);
                
                // Lerp position and rotation
                lt.Position = math.lerp(climb.WallJumpStartPos, climb.WallJumpTargetPos, easedT);
                lt.Rotation = math.slerp(climb.WallJumpStartRot, climb.WallJumpTargetRot, easedT);
                
                // Keep physics zeroed during transition
                vel.Linear = float3.zero;
                vel.Angular = float3.zero;
                
                // Check for completion
                if (t >= 1f)
                {
                    climb.IsWallJumping = false;
                    
                    // Attach to new surface
                    climb.GripWorldPosition = climb.WallJumpTargetGrip;
                    climb.GripWorldNormal = climb.WallJumpTargetNormal;
                    climb.SurfaceEntity = climb.WallJumpTargetSurface;
                    
                    // Update local space grip for moving platform support
                    if (climb.SurfaceEntity != Entity.Null && 
                        LocalTransformLookup.HasComponent(climb.SurfaceEntity))
                    {
                        var surfaceTransform = LocalTransformLookup[climb.SurfaceEntity];
                        climb.GripLocalPosition = InverseTransformPoint(
                            surfaceTransform.Position, 
                            surfaceTransform.Rotation, 
                            climb.GripWorldPosition);
                    }
                }
            }
            
            private bool IsPathObstructed(
                float3 startPos, 
                float3 endPos, 
                float3 surfaceNormal,
                FreeClimbSettings cfg,
                Entity selfEntity)
            {
                // Simple obstruction check: raycast along the jump path
                // More sophisticated version would do capsule sweep
                
                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.ObstacleLayers,
                    GroupIndex = 0
                };
                
                var rayInput = new RaycastInput
                {
                    Start = startPos,
                    End = endPos,
                    Filter = filter
                };
                
                if (PhysicsWorld.CastRay(rayInput, out RaycastHit hit))
                {
                    // Hit something - check if it's a valid climb surface
                    // If not climbable, it's an obstruction
                    float angleFromUp = math.degrees(math.acos(math.dot(hit.SurfaceNormal, new float3(0, 1, 0))));
                    bool isClimbable = angleFromUp >= cfg.MinSurfaceAngle && angleFromUp <= cfg.MaxSurfaceAngle;
                    
                    // If hit distance is too short and not climbable, path is obstructed
                    if (hit.Fraction < 0.3f && !isClimbable)
                        return true;
                }
                
                return false;
            }
            
            private bool TryFindTargetSurface(
                float3 handPos,
                float3 targetPos,
                float3 currentNormal,
                float3 jumpDir,
                FreeClimbSettings cfg,
                out float3 hitPosition,
                out float3 hitNormal,
                out Entity hitEntity)
            {
                hitPosition = float3.zero;
                hitNormal = float3.zero;
                hitEntity = Entity.Null;
                
                var climbFilter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.ClimbableLayers,
                    GroupIndex = 0
                };
                
                // Phase 1: Direct linecast toward target
                var ray1 = new RaycastInput
                {
                    Start = handPos,
                    End = targetPos,
                    Filter = climbFilter
                };
                
                if (PhysicsWorld.CastRay(ray1, out RaycastHit hit1))
                {
                    if (IsValidClimbSurface(hit1.SurfaceNormal, cfg) && 
                        hit1.Fraction * cfg.WallJumpMaxDistance >= cfg.WallJumpMinDistance)
                    {
                        hitPosition = hit1.Position;
                        hitNormal = hit1.SurfaceNormal;
                        hitEntity = hit1.Entity;
                        return true;
                    }
                }

                // Safety: Guard inputs
                if (!math.all(math.isfinite(targetPos)) || !math.all(math.isfinite(currentNormal)))
                {
                    hitPosition = targetPos; hitNormal = currentNormal; hitEntity = Entity.Null;
                    return false;
                }
                // Phase 2: Raycast forward from target position (into potential wall)
                float3 forwardDir = -currentNormal; // Assume facing same direction
                var ray2 = new RaycastInput
                {
                    Start = targetPos,
                    End = targetPos + forwardDir * cfg.WallJumpDepth,
                    Filter = climbFilter
                };
                
                if (PhysicsWorld.CastRay(ray2, out RaycastHit hit2))
                {
                    if (IsValidClimbSurface(hit2.SurfaceNormal, cfg))
                    {
                        hitPosition = hit2.Position;
                        hitNormal = hit2.SurfaceNormal;
                        hitEntity = hit2.Entity;
                        return true;
                    }
                }
                
                // Phase 3: Try at shorter distances (fallback)
                float3 midPoint = math.lerp(handPos, targetPos, 0.6f);
                var ray3 = new RaycastInput
                {
                    Start = midPoint,
                    End = midPoint + forwardDir * cfg.WallJumpDepth,
                    Filter = climbFilter
                };
                
                if (PhysicsWorld.CastRay(ray3, out RaycastHit hit3))
                {
                    if (IsValidClimbSurface(hit3.SurfaceNormal, cfg))
                    {
                        hitPosition = hit3.Position;
                        hitNormal = hit3.SurfaceNormal;
                        hitEntity = hit3.Entity;
                        return true;
                    }
                }
                
                return false;
            }
            
            private bool IsValidClimbSurface(float3 surfaceNormal, FreeClimbSettings cfg)
            {
                float angleFromUp = math.degrees(math.acos(math.dot(surfaceNormal, new float3(0, 1, 0))));
                return angleFromUp >= cfg.MinSurfaceAngle && angleFromUp <= cfg.MaxSurfaceAngle;
            }
            
            private void StartWallJump(
                ref FreeClimbState climb, 
                ref LocalTransform lt,
                float3 targetGrip, 
                float3 targetNormal, 
                Entity targetEntity,
                FreeClimbSettings cfg)
            {
                climb.IsWallJumping = true;
                climb.WallJumpProgress = 0f;
                climb.TransitionStartTime = CurrentTime; // EPIC 14.27: For safety timeout
                
                // Store starting state
                climb.WallJumpStartPos = lt.Position;
                climb.WallJumpStartRot = lt.Rotation;
                
                // Calculate player target position from grip position
                float3 normalOffset = targetNormal * cfg.SurfaceOffset;
                float3 verticalOffset = new float3(0, cfg.HandTargetOffset.y, 0);
                climb.WallJumpTargetPos = targetGrip + normalOffset - verticalOffset;
                
                // Calculate target rotation (face the new surface)
                float3 facingDir = -targetNormal;
                facingDir.y = 0;
                if (math.lengthsq(facingDir) > 0.001f)
                {
                    climb.WallJumpTargetRot = quaternion.LookRotation(
                        math.normalize(facingDir), 
                        new float3(0, 1, 0));
                }
                else
                {
                    climb.WallJumpTargetRot = lt.Rotation;
                }
                
                // Store target surface info for completion
                climb.WallJumpTargetGrip = targetGrip;
                climb.WallJumpTargetNormal = targetNormal;
                climb.WallJumpTargetSurface = targetEntity;
            }
            
            private void TriggerDismount(
                ref FreeClimbState climb, 
                ref LocalTransform lt,
                ref PhysicsVelocity vel,
                float3 jumpDir,
                FreeClimbSettings cfg,
                double currentTime)
            {
                // EPIC 13.20: Track dismount for cooldown
                climb.LastDismountTime = currentTime;
                climb.LastClimbedSurface = climb.SurfaceEntity;
                
                // Stop climbing
                climb.IsClimbing = false;
                climb.SurfaceEntity = Entity.Null;
                
                // Give player some velocity in the jump direction + up
                float3 dismountVel = jumpDir * 3f + new float3(0, 2f, 0);
                vel.Linear = dismountVel;
            }
            
            private static float EaseOutQuad(float t)
            {
                return 1f - (1f - t) * (1f - t);
            }
            
            private static float3 InverseTransformPoint(float3 parentPos, quaternion parentRot, float3 worldPoint)
            {
                float3 relative = worldPoint - parentPos;
                return math.mul(math.inverse(parentRot), relative);
            }
        }
    }
}
