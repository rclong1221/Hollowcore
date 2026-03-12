using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 6: Handles ranged interaction initiation.
    ///
    /// For interactables with RangedInteraction:
    /// - Raycast mode: instant raycast from eye → if hits target → set InteractRequest
    /// - Projectile mode: on fire → set IsConnecting, progress each frame, on arrival → set InteractRequest
    /// - ArcProjectile mode: same as Projectile but with gravity-arc trajectory
    ///
    /// Runs BEFORE InteractAbilitySystem so that ranged connections can set InteractRequest
    /// which InteractAbilitySystem then processes in the same frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RangedInteractionSystem : ISystem
    {
        private const float EyeHeight = 1.6f;
        private const float AimConeAngle = 15f; // degrees

        // Collision filter: hits everything except Triggers
        private static readonly CollisionFilter RangedRaycastFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = ~(1u << 6), // Exclude Trigger layer
            GroupIndex = 0
        };

        private ComponentLookup<RangedInteraction> _rangedInteractionLookup;
        private ComponentLookup<RangedInteractionState> _rangedStateLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RangedInteraction>();
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _rangedInteractionLookup = state.GetComponentLookup<RangedInteraction>(true);
            _rangedStateLookup = state.GetComponentLookup<RangedInteractionState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _rangedInteractionLookup.Update(ref state);
            _rangedStateLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            float deltaTime = SystemAPI.Time.DeltaTime;

            // --- Process active projectile connections ---
            foreach (var (rangedState, rangedConfig, transform, entity) in
                     SystemAPI.Query<RefRW<RangedInteractionState>, RefRO<RangedInteraction>,
                                     RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                if (!rangedState.ValueRO.IsConnecting)
                    continue;

                float speed = rangedConfig.ValueRO.ProjectileSpeed;
                if (speed <= 0)
                {
                    // Instant — should not be connecting, complete immediately
                    rangedState.ValueRW.IsConnecting = false;
                    rangedState.ValueRW.ConnectionProgress = 1f;
                    continue;
                }

                // Calculate total distance and progress
                float totalDist = math.distance(rangedState.ValueRO.LaunchPosition,
                                                rangedState.ValueRO.TargetPosition);
                if (totalDist < 0.01f)
                {
                    rangedState.ValueRW.IsConnecting = false;
                    rangedState.ValueRW.ConnectionProgress = 1f;
                    continue;
                }

                float progressPerFrame = (speed * deltaTime) / totalDist;
                rangedState.ValueRW.ConnectionProgress += progressPerFrame;

                if (rangedState.ValueRO.ConnectionProgress >= 1f)
                {
                    // Projectile arrived — interaction can now proceed
                    rangedState.ValueRW.ConnectionProgress = 1f;
                    rangedState.ValueRW.IsConnecting = false;

                    // The InteractRequest was already set when the projectile was fired
                    // InteractAbilitySystem will check RangedInteractionState.IsConnecting
                    // and proceed once it's false
                }
            }

            // --- Detect ranged interaction attempts from players ---
            foreach (var (ability, request, input, transform, entity) in
                     SystemAPI.Query<RefRO<InteractAbility>, RefRW<InteractRequest>,
                                     RefRO<PlayerInput>, RefRO<LocalTransform>>()
                     .WithAll<Simulate, CanInteract>()
                     .WithEntityAccess())
            {
                // Only process when player fires Use and isn't already interacting
                if (!input.ValueRO.Use.IsSet || ability.ValueRO.IsInteracting)
                    continue;

                // Check if the player's target has RangedInteraction
                Entity targetEntity = ability.ValueRO.TargetEntity;
                if (targetEntity == Entity.Null)
                    continue;

                if (!_rangedInteractionLookup.HasComponent(targetEntity))
                    continue;

                var rangedConfig = _rangedInteractionLookup[targetEntity];

                // Check range
                if (!_transformLookup.HasComponent(targetEntity))
                    continue;

                float3 playerPos = transform.ValueRO.Position;
                float3 targetPos = _transformLookup[targetEntity].Position;
                float distance = math.distance(playerPos, targetPos);

                if (distance > rangedConfig.MaxRange)
                    continue;

                // Aim direction check
                if (rangedConfig.RequireAimAtTarget)
                {
                    float3 eyePos = playerPos + new float3(0, EyeHeight, 0);
                    quaternion lookRot = quaternion.Euler(
                        math.radians(input.ValueRO.CameraPitch),
                        math.radians(input.ValueRO.CameraYaw), 0);
                    float3 aimDir = math.rotate(lookRot, new float3(0, 0, 1));
                    float3 toTarget = math.normalizesafe(targetPos - eyePos);
                    float dot = math.dot(aimDir, toTarget);
                    float coneRad = math.cos(math.radians(AimConeAngle));

                    if (dot < coneRad)
                        continue; // Not aiming at target
                }

                switch (rangedConfig.InitType)
                {
                    case RangedInitType.Raycast:
                    {
                        // Instant raycast — verify line of sight
                        float3 eyePos = playerPos + new float3(0, EyeHeight, 0);
                        float3 dir = math.normalizesafe(targetPos - eyePos);

                        var rayInput = new RaycastInput
                        {
                            Start = eyePos,
                            End = eyePos + dir * rangedConfig.MaxRange,
                            Filter = RangedRaycastFilter
                        };

                        if (physicsWorld.CastRay(rayInput, out var hit))
                        {
                            // Check if we hit the target entity (or close enough)
                            var hitEntity = physicsWorld.PhysicsWorld.Bodies[hit.RigidBodyIndex].Entity;

                            // Accept hit if it's the target or very close to target position
                            bool hitTarget = hitEntity == targetEntity ||
                                             math.distancesq(hit.Position, targetPos) < 4f; // 2m tolerance

                            if (hitTarget)
                            {
                                request.ValueRW.StartInteract = true;
                                request.ValueRW.TargetEntity = targetEntity;
                            }
                        }
                        break;
                    }

                    case RangedInitType.Projectile:
                    case RangedInitType.ArcProjectile:
                    {
                        // Fire projectile — set connecting state
                        if (!_rangedStateLookup.HasComponent(targetEntity))
                            break;

                        var rangedState = _rangedStateLookup.GetRefRW(targetEntity);
                        if (rangedState.ValueRO.IsConnecting)
                            break; // Already connecting

                        float3 launchPos = playerPos + new float3(0, EyeHeight, 0);
                        rangedState.ValueRW.IsConnecting = true;
                        rangedState.ValueRW.ConnectionProgress = 0;
                        rangedState.ValueRW.InitiatorEntity = entity;
                        rangedState.ValueRW.LaunchPosition = launchPos;
                        rangedState.ValueRW.TargetPosition = targetPos;

                        // Set the interact request — InteractAbilitySystem will check
                        // IsConnecting and wait until projectile arrives
                        request.ValueRW.StartInteract = true;
                        request.ValueRW.TargetEntity = targetEntity;
                        break;
                    }
                }
            }
        }
    }
}
