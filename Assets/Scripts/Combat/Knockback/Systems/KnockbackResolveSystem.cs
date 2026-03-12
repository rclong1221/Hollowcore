using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Consumes KnockbackRequest entities and writes KnockbackState on targets.
    /// Handles resistance checks, immunity, SuperArmor, falloff, force-to-velocity conversion.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(KnockbackMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct KnockbackResolveSystem : ISystem
    {
        private EntityQuery _requestQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _requestQuery = state.GetEntityQuery(ComponentType.ReadOnly<KnockbackRequest>());
            state.RequireForUpdate(_requestQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Complete any pending jobs that write to components we read
            state.CompleteDependency();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Get config singleton or use defaults
            KnockbackConfig config;
            if (SystemAPI.HasSingleton<KnockbackConfig>())
                config = SystemAPI.GetSingleton<KnockbackConfig>();
            else
                config = KnockbackConfig.Default;

            var requestEntities = _requestQuery.ToEntityArray(Allocator.Temp);
            var requests = _requestQuery.ToComponentDataArray<KnockbackRequest>(Allocator.Temp);

            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                var requestEntity = requestEntities[i];

                // Always destroy the request entity
                ecb.DestroyEntity(requestEntity);

                // Validate target exists and has KnockbackState
                if (!state.EntityManager.Exists(request.TargetEntity))
                    continue;
                if (!state.EntityManager.HasComponent<KnockbackState>(request.TargetEntity))
                    continue;

                // Check KnockbackResistance if present
                float effectiveForce = request.Force;
                bool hasResistance = state.EntityManager.HasComponent<KnockbackResistance>(request.TargetEntity);
                if (hasResistance)
                {
                    var resistance = state.EntityManager.GetComponentData<KnockbackResistance>(request.TargetEntity);

                    // Hard immunity or immunity window
                    if (resistance.IsCurrentlyImmune)
                        continue;

                    // SuperArmor threshold check
                    if (!request.IgnoreSuperArmor && effectiveForce < resistance.SuperArmorThreshold)
                        continue;

                    // Apply resistance percentage
                    effectiveForce *= (1f - math.saturate(resistance.ResistancePercent));
                }

                // Apply distance-based falloff
                effectiveForce *= KnockbackEasingMath.ComputeFalloff(request.Distance, request.MaxRadius, request.Falloff);

                // Skip if below minimum effective force
                if (effectiveForce < config.MinimumEffectiveForce)
                    continue;

                // Read current knockback state
                var currentState = state.EntityManager.GetComponentData<KnockbackState>(request.TargetEntity);

                // Compute velocity
                float3 direction = math.normalizesafe(request.Direction, new float3(0, 0, 1));

                // Apply type-specific direction modifiers
                float3 velocity;
                float duration;
                switch (request.Type)
                {
                    case KnockbackType.Push:
                        direction.y = 0; // Horizontal only
                        direction = math.normalizesafe(direction, new float3(0, 0, 1));
                        velocity = direction * (effectiveForce / config.ForceDivisor);
                        duration = request.DurationOverride > 0f ? request.DurationOverride : config.PushDuration;
                        break;

                    case KnockbackType.Launch:
                        float horizontalForce = effectiveForce / config.ForceDivisor;
                        float3 horizontalDir = direction;
                        horizontalDir.y = 0;
                        horizontalDir = math.normalizesafe(horizontalDir, new float3(0, 0, 1));
                        float verticalRatio = request.LaunchVerticalRatio > 0f ? request.LaunchVerticalRatio : config.DefaultLaunchVerticalRatio;
                        velocity = horizontalDir * horizontalForce + new float3(0, horizontalForce * verticalRatio, 0);
                        duration = request.DurationOverride > 0f ? request.DurationOverride : config.LaunchDuration;
                        break;

                    case KnockbackType.Pull:
                        direction = -direction; // Toward source
                        direction.y = 0;
                        direction = math.normalizesafe(direction, new float3(0, 0, 1));
                        velocity = direction * (effectiveForce / config.ForceDivisor);
                        duration = request.DurationOverride > 0f ? request.DurationOverride : config.PullDuration;
                        break;

                    case KnockbackType.Stagger:
                        direction.y = 0;
                        direction = math.normalizesafe(direction, new float3(0, 0, 1));
                        velocity = direction * (effectiveForce * config.StaggerForceMultiplier / config.ForceDivisor);
                        duration = request.DurationOverride > 0f ? request.DurationOverride : config.StaggerDuration;
                        break;

                    default:
                        velocity = direction * (effectiveForce / config.ForceDivisor);
                        duration = config.PushDuration;
                        break;
                }

                // Clamp to max velocity
                float speed = math.length(velocity);
                if (speed > config.MaxVelocity)
                    velocity = math.normalize(velocity) * config.MaxVelocity;

                float initialSpeed = math.length(velocity);

                // Stronger knockback overrides weaker active knockback
                if (currentState.IsActive && currentState.InitialSpeed >= initialSpeed)
                {
                    // Stagger does NOT override displacement knockback
                    if (request.Type == KnockbackType.Stagger)
                        continue;
                    // Existing is stronger — skip
                    if (currentState.InitialSpeed >= initialSpeed)
                        continue;
                }

                // Write KnockbackState
                var newState = new KnockbackState
                {
                    IsActive = true,
                    Velocity = velocity,
                    InitialSpeed = initialSpeed,
                    Duration = duration,
                    Elapsed = 0f,
                    Easing = request.Easing,
                    Type = request.Type,
                    GroundedOnly = request.Type == KnockbackType.Launch, // Launch: ground-only after initial impulse
                    SourceEntity = request.SourceEntity
                };

                state.EntityManager.SetComponentData(request.TargetEntity, newState);

                // TODO: EPIC 16.1 — Interrupt integration
                // if (request.TriggersInterrupt && effectiveForce >= config.InterruptForceThreshold)
                // {
                //     Create InterruptRequest on target
                // }
            }

            requestEntities.Dispose();
            requests.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
