using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Survival.Throwables
{
    /// <summary>
    /// Simulates arc trajectory for thrown objects using simple physics.
    /// Objects follow a parabolic arc until they hit the ground.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ThrownObjectPhysicsSystem : ISystem
    {
        private const float Gravity = -9.81f;
        private const float GroundY = 0f; // TODO: Use actual ground detection

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            new ThrownObjectPhysicsJob
            {
                DeltaTime = deltaTime,
                Gravity = Gravity,
                GroundY = GroundY
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        partial struct ThrownObjectPhysicsJob : IJobEntity
        {
            public float DeltaTime;
            public float Gravity;
            public float GroundY;

            void Execute(
                ref LocalTransform transform,
                ref ThrownObjectVelocity velocity,
                ref ThrownObject thrownObject)
            {
                // Skip if already landed
                if (thrownObject.HasLanded)
                    return;

                // Apply gravity
                velocity.Linear.y += Gravity * DeltaTime;

                // Update position
                float3 newPosition = transform.Position + velocity.Linear * DeltaTime;

                // Simple ground check
                if (newPosition.y <= GroundY)
                {
                    newPosition.y = GroundY;
                    velocity.Linear = float3.zero;
                    thrownObject.HasLanded = true;
                }

                transform.Position = newPosition;
            }
        }
    }

    /// <summary>
    /// Manages thrown object lifetime and despawning.
    /// Runs on server for authoritative destruction.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ThrownObjectLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (thrownObject, entity) in
                     SystemAPI.Query<RefRW<ThrownObject>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var obj = ref thrownObject.ValueRW;

                // Decrement lifetime
                obj.RemainingLifetime -= deltaTime;

                // Update intensity based on remaining lifetime (fade out in last 20%)
                float lifetimePercent = obj.RemainingLifetime / obj.InitialLifetime;
                if (lifetimePercent < 0.2f)
                {
                    obj.Intensity = lifetimePercent / 0.2f;
                }

                // Destroy when expired
                if (obj.RemainingLifetime <= 0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Updates EmitsLight components based on ThrownObject state.
    /// Handles flickering effect when near end of lifetime.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ThrownObjectLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct FlareIntensitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = (float)SystemAPI.Time.ElapsedTime;

            new FlareIntensityJob
            {
                Time = time
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        partial struct FlareIntensityJob : IJobEntity
        {
            public float Time;

            void Execute(
                in ThrownObject thrownObject,
                ref EmitsLight light)
            {
                // Base intensity from thrown object
                float baseIntensity = thrownObject.Intensity * light.MaxIntensity;

                // Add flickering when near end of lifetime (last 20%)
                float lifetimePercent = thrownObject.RemainingLifetime / thrownObject.InitialLifetime;
                if (lifetimePercent < 0.2f && light.FlickerAtEnd)
                {
                    // Random flicker using noise-like pattern
                    float flicker = math.sin(Time * 30f) * 0.3f + math.sin(Time * 47f) * 0.2f;
                    flicker = math.saturate(0.5f + flicker);
                    baseIntensity *= flicker;
                }

                light.Intensity = baseIntensity;
            }
        }
    }

    /// <summary>
    /// Updates attraction state based on ThrownObject state.
    /// Activates attraction when object has landed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ThrownObjectLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct AttractionActivationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new AttractionActivationJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        partial struct AttractionActivationJob : IJobEntity
        {
            void Execute(
                in ThrownObject thrownObject,
                ref AttractsCreatures attraction)
            {
                // Activate attraction when landed and has remaining lifetime
                attraction.IsActive = thrownObject.HasLanded && thrownObject.RemainingLifetime > 0f;
            }
        }
    }

    /// <summary>
    /// Updates sound emission state based on ThrownObject state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ThrownObjectLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoundLureUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new SoundLureUpdateJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        partial struct SoundLureUpdateJob : IJobEntity
        {
            void Execute(
                in ThrownObject thrownObject,
                ref EmitsSound sound)
            {
                // Play sound when landed and has remaining lifetime
                sound.IsPlaying = thrownObject.HasLanded && thrownObject.RemainingLifetime > 0f;

                // Reduce volume near end of lifetime
                sound.Volume = thrownObject.Intensity;
            }
        }
    }
}
