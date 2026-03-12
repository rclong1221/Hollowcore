using DIG.Roguelite;
using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Polls IZoneClearCondition each frame during Active/BossEncounter phases.
    /// On clear: sets ZoneState.IsCleared, transitions to ZoneTransition phase.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class ZoneClearDetectionSystem : SystemBase
    {
        private IZoneClearCondition _condition;
        private bool _conditionInitialized;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<ZoneState>();
            RequireForUpdate<RunConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();

            var run = SystemAPI.GetSingleton<RunState>();
            if (run.Phase != RunPhase.Active && run.Phase != RunPhase.BossEncounter)
            {
                _conditionInitialized = false;
                return;
            }

            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            var zoneState = SystemAPI.GetSingleton<ZoneState>();

            if (zoneState.IsCleared) return;

            // Lazy-init condition from ZoneClearMode
            if (!_conditionInitialized)
            {
                _condition = CreateCondition(zoneState.ClearMode);

                var sequencer = World.GetExistingSystemManaged<ZoneSequenceResolverSystem>();
                var zoneDef = sequencer?.GetZoneAtIndex(zoneState.ZoneIndex);
                _condition?.OnZoneActivated(this, zoneDef);
                _conditionInitialized = true;
            }

            // Manual mode: only game code via ZoneClearAPI triggers clear
            if (zoneState.ClearMode == ZoneClearMode.Manual)
                return;

            // Objective mode: game provides custom condition (set via SetCustomCondition)
            // Fall through to condition polling

            if (_condition == null) return;

            if (_condition.IsCleared(this))
            {
                zoneState.IsCleared = true;
                EntityManager.SetComponentData(runEntity, zoneState);

                // Check if there are more zones
                ref var config = ref SystemAPI.GetSingleton<RunConfigSingleton>().Config.Value;
                int nextZone = zoneState.ZoneIndex + 1;

                var sequencer = World.GetExistingSystemManaged<ZoneSequenceResolverSystem>();
                bool hasMoreZones = sequencer != null && sequencer.IsResolved
                    ? (sequencer.GetZoneAtIndex(nextZone) != null || (sequencer.ResolvedZoneCount > 0))
                    : (nextZone < config.ZoneCount);

                if (hasMoreZones)
                {
                    run.Phase = RunPhase.ZoneTransition;
                }
                else
                {
                    run.Phase = RunPhase.RunEnd;
                    run.EndReason = RunEndReason.BossDefeated;
                }

                SystemAPI.SetSingleton(run);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ZoneClear] Zone {zoneState.ZoneIndex} cleared. " +
                          $"Killed={zoneState.EnemiesKilled}, Time={zoneState.TimeInZone:F1}s. " +
                          $"Next phase={run.Phase}");
#endif
            }
        }

        /// <summary>Set a custom clear condition (for Objective mode or game-specific logic).</summary>
        public void SetCustomCondition(IZoneClearCondition condition)
        {
            _condition = condition;
            _conditionInitialized = false;
        }

        private static IZoneClearCondition CreateCondition(ZoneClearMode mode)
        {
            return mode switch
            {
                ZoneClearMode.AllEnemiesDead => new AllEnemiesDeadCondition(),
                ZoneClearMode.PlayerTriggered => new PlayerTriggeredCondition(),
                ZoneClearMode.TimerSurvival => new TimerExpiredCondition(),
                ZoneClearMode.BossKill => new AllEnemiesDeadCondition(), // Boss kill = boss is an enemy
                ZoneClearMode.TriggerThenBoss => new TriggerThenBossCondition(),
                ZoneClearMode.Objective => null, // Game must provide via SetCustomCondition
                ZoneClearMode.Manual => null,
                _ => null
            };
        }
    }
}
