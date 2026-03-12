using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Refills token buckets for all players every tick.
    /// Burst-compiled ISystem for tight buffer iteration.
    /// O(1) per player per RPC type. Budget: &lt;0.05ms for 64 players * 9 types.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct RateLimitRefillSystem : ISystem
    {
        private EntityQuery _profileQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ValidationConfig>();
            _profileQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ValidationChildTag>()
                .WithAllRW<RateLimitEntry>()
                .Build(ref state);
            state.RequireForUpdate(_profileQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            var config = SystemAPI.GetSingleton<ValidationConfig>();

            foreach (var buffer in
                SystemAPI.Query<DynamicBuffer<RateLimitEntry>>()
                    .WithAll<ValidationChildTag>())
            {
                var buf = buffer;
                for (int i = 0; i < buf.Length; i++)
                {
                    var entry = buf[i];

                    // Refill tokens (capped at MaxBurst)
                    // Use config default since per-entry max burst was set at bake time
                    float maxBurst = config.DefaultMaxBurst;
                    float tokensPerSec = config.DefaultTokensPerSecond;

                    entry.TokenCount = Unity.Mathematics.math.min(
                        maxBurst,
                        entry.TokenCount + tokensPerSec * dt);
                    entry.BurstConsumed = 0;

                    buf[i] = entry;
                }
            }
        }
    }
}
