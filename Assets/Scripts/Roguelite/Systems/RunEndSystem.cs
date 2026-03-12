using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: On RunEnd: calculates final score, queues meta-currency conversion (23.2).
    /// RunEnd phase persists for 1 frame so downstream systems (23.2 MetaCurrencyConversionSystem)
    /// can react to it. Transitions to MetaScreen on the following frame.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PermadeathSystem))]
    public partial class RunEndSystem : SystemBase
    {
        private bool _scoreCalculated;
        private bool _readyToTransition;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<RunConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var run = SystemAPI.GetSingleton<RunState>();

            if (run.Phase != RunPhase.RunEnd)
            {
                _scoreCalculated = false;
                _readyToTransition = false;
                return;
            }

            // Frame 1: Calculate score, notify UI. Leave phase as RunEnd so downstream
            // systems (23.2 MetaCurrencyConversionSystem) can react this frame.
            if (!_scoreCalculated)
            {
                ref var config = ref SystemAPI.GetSingleton<RunConfigSingleton>().Config.Value;

                int zonesCleared = run.CurrentZoneIndex;
                int zoneScore = zonesCleared * 1000;
                int currencyBonus = run.RunCurrency * 2;
                int timeBonus = run.ElapsedTime > 0f ? (int)(10000f / run.ElapsedTime) : 0;
                int victoryBonus = run.EndReason == RunEndReason.BossDefeated ? 5000 : 0;

                run.Score = zoneScore + currencyBonus + timeBonus + victoryBonus;
                SystemAPI.SetSingleton(run);

                if (RunUIRegistry.HasProvider)
                    RunUIRegistry.Provider.OnRunEnd(run.EndReason, run.Score, run.RunCurrency, zonesCleared);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[RunEnd] Score={run.Score} (zones={zoneScore}, currency={currencyBonus}, time={timeBonus}, victory={victoryBonus}). " +
                          $"Reason={run.EndReason}, MetaRate={config.MetaCurrencyConversionRate}");
#endif

                _scoreCalculated = true;
                _readyToTransition = true;
                return;
            }

            // Frame 2+: Transition to MetaScreen now that downstream systems have had
            // a full frame to read RunPhase.RunEnd.
            if (_readyToTransition)
            {
                run.Phase = RunPhase.MetaScreen;
                SystemAPI.SetSingleton(run);
                _readyToTransition = false;
            }
        }
    }
}
