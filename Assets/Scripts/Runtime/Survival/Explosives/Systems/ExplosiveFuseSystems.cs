using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Explosives
{
    /// <summary>
    /// Arms explosives after a short placement delay.
    /// Prevents instant detonation when placed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ExplosiveArmingSystem : ISystem
    {
        private const float ArmingDelay = 0.5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var explosive in
                     SystemAPI.Query<RefRW<PlacedExplosive>>())
            {
                ref var exp = ref explosive.ValueRW;

                // Skip already armed
                if (exp.IsArmed)
                    continue;

                // Update placement timer
                exp.TimeSincePlacement += deltaTime;

                // Arm after delay
                if (exp.TimeSincePlacement >= ArmingDelay)
                {
                    exp.IsArmed = true;
                }
            }
        }
    }

    /// <summary>
    /// Counts down the fuse timer on armed explosives.
    /// Adds DetonationRequest when timer expires.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExplosiveArmingSystem))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ExplosiveFuseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (explosive, entity) in
                     SystemAPI.Query<RefRW<PlacedExplosive>>()
                     .WithNone<DetonationRequest>()
                     .WithEntityAccess())
            {
                ref var exp = ref explosive.ValueRW;

                // Only count down armed explosives
                if (!exp.IsArmed)
                    continue;

                // Decrement fuse
                exp.FuseTimeRemaining -= deltaTime;

                // Trigger detonation when expired
                if (exp.FuseTimeRemaining <= 0f)
                {
                    exp.FuseTimeRemaining = 0f;
                    ecb.AddComponent<DetonationRequest>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Client-side system that updates explosive visual/audio state.
    /// Increases beep frequency as fuse runs down.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExplosiveBeepSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (explosive, visualState) in
                     SystemAPI.Query<RefRO<PlacedExplosive>, RefRW<ExplosiveVisualState>>()
                     .WithAll<Simulate>())
            {
                ref var visual = ref visualState.ValueRW;
                var exp = explosive.ValueRO;

                // Not armed yet - no beeping
                if (!exp.IsArmed)
                {
                    visual.IsBeeping = false;
                    visual.LightOn = false;
                    continue;
                }

                visual.IsBeeping = true;

                // Calculate beep interval based on remaining time
                // Fast beeping near end, slow at start
                float fusePercent = exp.FuseTimeRemaining / exp.InitialFuseTime;
                visual.BeepInterval = 0.1f + fusePercent * 0.9f; // 0.1s to 1.0s

                // Update beep timer
                visual.TimeSinceBeep += deltaTime;

                // Toggle light on beep
                if (visual.TimeSinceBeep >= visual.BeepInterval)
                {
                    visual.TimeSinceBeep = 0f;
                    visual.LightOn = !visual.LightOn;
                }
            }
        }
    }
}
