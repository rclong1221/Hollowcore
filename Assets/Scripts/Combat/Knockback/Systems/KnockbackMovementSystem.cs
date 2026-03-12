using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Physics;
using Player.Components;
using DIG.AI.Components;
using DIG.Surface;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Applies KnockbackState velocity to entity positions each tick.
    /// Players: writes AddExternalForceRequest (→ ExternalForceSystem → CharacterController).
    /// Enemies: writes LocalTransform.Position directly (kinematic pattern).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(KnockbackResolveSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct KnockbackMovementSystem : ISystem
    {
        private const int KnockbackSourceId = 99999; // Unique source ID for knockback forces

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<KnockbackState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            float deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0f) return;

            // Get config
            KnockbackConfig config;
            if (SystemAPI.HasSingleton<KnockbackConfig>())
                config = SystemAPI.GetSingleton<KnockbackConfig>();
            else
                config = KnockbackConfig.Default;

            bool useFriction = config.EnableSurfaceFriction;

            // --- Player knockback: write AddExternalForceRequest ---
            foreach (var (knockbackState, forceRequest, forceRequestEnabled, groundState) in
                SystemAPI.Query<RefRW<KnockbackState>, RefRW<AddExternalForceRequest>, EnabledRefRW<AddExternalForceRequest>, RefRO<GroundSurfaceState>>()
                    .WithAll<PlayerTag, Simulate>())
            {
                ref var kb = ref knockbackState.ValueRW;
                if (!kb.IsActive) continue;

                // Advance elapsed time
                kb.Elapsed += deltaTime;

                // Check expiration
                if (kb.IsExpired)
                {
                    kb.IsActive = false;
                    continue;
                }

                // Compute easing factor
                float factor = KnockbackEasingMath.EvaluateEasing(kb.Progress, kb.Easing);

                // Compute frame velocity from initial direction + speed * easing
                float3 direction = math.normalizesafe(kb.Velocity, float3.zero);
                float3 frameVelocity = direction * kb.InitialSpeed * factor;

                // Surface friction
                if (useFriction)
                {
                    float friction = KnockbackEasingMath.GetSurfaceFriction(groundState.ValueRO.SurfaceId);
                    frameVelocity *= friction;
                }

                // Launch type: apply gravity
                if (kb.Type == KnockbackType.Launch)
                {
                    kb.Velocity.y -= 9.81f * config.LaunchGravityMultiplier * deltaTime;
                    frameVelocity.y = kb.Velocity.y * factor;
                }

                // Write AddExternalForceRequest — ExternalForceSystem picks this up
                forceRequest.ValueRW = new AddExternalForceRequest
                {
                    Force = frameVelocity * deltaTime,
                    SoftFrames = 0, // Instant application
                    Decay = 10f,    // Fast decay — we re-write every frame during knockback
                    SourceId = KnockbackSourceId
                };
                forceRequestEnabled.ValueRW = true;
            }

            // --- Enemy knockback: direct LocalTransform writes ---
            foreach (var (knockbackState, transform, groundState) in
                SystemAPI.Query<RefRW<KnockbackState>, RefRW<LocalTransform>, RefRO<GroundSurfaceState>>()
                    .WithAll<AIBrain, Simulate>()
                    .WithNone<PlayerTag>())
            {
                ref var kb = ref knockbackState.ValueRW;
                if (!kb.IsActive) continue;

                // Advance elapsed time
                kb.Elapsed += deltaTime;

                // Check expiration
                if (kb.IsExpired)
                {
                    kb.IsActive = false;
                    continue;
                }

                // Compute easing factor
                float factor = KnockbackEasingMath.EvaluateEasing(kb.Progress, kb.Easing);

                // Compute frame velocity
                float3 direction = math.normalizesafe(kb.Velocity, float3.zero);
                float3 frameVelocity = direction * kb.InitialSpeed * factor;

                // Surface friction
                if (useFriction)
                {
                    float friction = KnockbackEasingMath.GetSurfaceFriction(groundState.ValueRO.SurfaceId);
                    frameVelocity *= friction;
                }

                // Launch type: apply gravity
                if (kb.Type == KnockbackType.Launch)
                {
                    kb.Velocity.y -= 9.81f * config.LaunchGravityMultiplier * deltaTime;
                    frameVelocity.y = kb.Velocity.y * factor;
                }

                // Compute displacement
                float3 displacement = frameVelocity * deltaTime;

                // Direct position write (kinematic body pattern, same as EnemySeparationSystem)
                transform.ValueRW.Position += displacement;
            }

            // --- Generic entities (no PlayerTag, no AIBrain): direct writes ---
            foreach (var (knockbackState, transform) in
                SystemAPI.Query<RefRW<KnockbackState>, RefRW<LocalTransform>>()
                    .WithAll<Simulate>()
                    .WithNone<PlayerTag, AIBrain, AddExternalForceRequest>())
            {
                ref var kb = ref knockbackState.ValueRW;
                if (!kb.IsActive) continue;

                kb.Elapsed += deltaTime;
                if (kb.IsExpired)
                {
                    kb.IsActive = false;
                    continue;
                }

                float factor = KnockbackEasingMath.EvaluateEasing(kb.Progress, kb.Easing);
                float3 direction = math.normalizesafe(kb.Velocity, float3.zero);
                float3 frameVelocity = direction * kb.InitialSpeed * factor;

                if (kb.Type == KnockbackType.Launch)
                {
                    kb.Velocity.y -= 9.81f * config.LaunchGravityMultiplier * deltaTime;
                    frameVelocity.y = kb.Velocity.y * factor;
                }

                transform.ValueRW.Position += frameVelocity * deltaTime;
            }
        }
    }
}
