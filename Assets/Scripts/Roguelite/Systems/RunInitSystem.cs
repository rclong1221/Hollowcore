using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Reacts to phase changes. Bridges to IZoneProvider (23.3),
    /// configures difficulty, sets up zone economy on phase transitions.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RunLifecycleSystem))]
    public partial class RunInitSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<RunConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();

            // Only process when phase has changed
            if (!EntityManager.IsComponentEnabled<RunPhaseChangedTag>(runEntity))
                return;

            var run = SystemAPI.GetSingleton<RunState>();
            ref var config = ref SystemAPI.GetSingleton<RunConfigSingleton>().Config.Value;

            switch (run.Phase)
            {
                case RunPhase.Preparation:
                    // Apply meta-upgrades (23.2), configure starting state
                    run.RunCurrency = config.StartingRunCurrency;
                    SystemAPI.SetSingleton(run);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[RunInit] Preparation: seed={run.Seed}, startCurrency={run.RunCurrency}");
#endif
                    break;

                case RunPhase.Active:
                    // Zone is now playable — 23.3 ZoneTransitionSystem handles IZoneProvider
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[RunInit] Zone {run.CurrentZoneIndex} active (zoneSeed={run.ZoneSeed})");
#endif
                    break;

                case RunPhase.ZoneTransition:
                    // Award zone clear currency
                    run.RunCurrency += config.RunCurrencyPerZoneClear;
                    SystemAPI.SetSingleton(run);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[RunInit] Zone {run.CurrentZoneIndex} cleared, +{config.RunCurrencyPerZoneClear} currency (total={run.RunCurrency})");
#endif
                    break;

                case RunPhase.RunEnd:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[RunInit] Run ended: reason={run.EndReason}, score={run.Score}, zones={run.CurrentZoneIndex}");
#endif
                    break;
            }

            // Consume the phase changed tag. Systems that need to react to phase changes
            // should order themselves UpdateBefore(RunInitSystem).
            EntityManager.SetComponentEnabled<RunPhaseChangedTag>(runEntity, false);
        }
    }
}
