#if UNITY_EDITOR
using Unity.Entities;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.7: Applies LiveTuningOverrides to actual game state.
    /// Only compiled in UNITY_EDITOR — zero runtime cost in builds.
    /// Runs before DifficultyScalingSystem so overrides take effect in the same frame.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LiveTuningApplySystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<LiveTuningOverrides>();
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            var overrides = SystemAPI.GetSingletonRW<LiveTuningOverrides>();
            var runState = SystemAPI.GetSingletonRW<RunState>();

            // Difficulty override
            if (overrides.ValueRO.DifficultyMultiplierOverride > 0f)
            {
                if (SystemAPI.HasSingleton<RuntimeDifficultyScale>())
                {
                    var diff = SystemAPI.GetSingletonRW<RuntimeDifficultyScale>();
                    diff.ValueRW.ZoneDifficultyMultiplier = overrides.ValueRO.DifficultyMultiplierOverride;
                }
            }

            // Spawn rate override
            if (SystemAPI.HasSingleton<ZoneState>())
            {
                var zone = SystemAPI.GetSingletonRW<ZoneState>();

                if (overrides.ValueRO.PauseSpawning != 0)
                    zone.ValueRW.SpawnBudget = 0f;
                else if (overrides.ValueRO.SpawnRateOverride > 0f)
                    zone.ValueRW.SpawnBudget = overrides.ValueRO.SpawnRateOverride;
            }

            // Currency grants (one-shot — consume after applying)
            if (overrides.ValueRO.GrantRunCurrency != 0)
            {
                runState.ValueRW.RunCurrency += overrides.ValueRO.GrantRunCurrency;
                overrides.ValueRW.GrantRunCurrency = 0;
            }

            if (overrides.ValueRO.GrantMetaCurrency != 0)
            {
                if (SystemAPI.HasSingleton<MetaBank>())
                {
                    var meta = SystemAPI.GetSingletonRW<MetaBank>();
                    meta.ValueRW.MetaCurrency += overrides.ValueRO.GrantMetaCurrency;
                }
                overrides.ValueRW.GrantMetaCurrency = 0;
            }

            // Phase force (one-shot)
            if (overrides.ValueRO.ForcePhase != RunPhase.None)
            {
                runState.ValueRW.Phase = overrides.ValueRO.ForcePhase;
                overrides.ValueRW.ForcePhase = RunPhase.None;
            }
        }
    }
}
#endif
