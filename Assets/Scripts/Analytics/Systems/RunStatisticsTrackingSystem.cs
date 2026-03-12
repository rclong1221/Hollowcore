using Unity.Entities;
using Unity.Mathematics;
using DIG.Combat.Systems;
using DIG.Roguelite;
using DIG.Roguelite.Analytics;
using Player.Components;

namespace DIG.Analytics
{
    /// <summary>
    /// EPIC 23.6: Tracks per-run statistics by reading kill credits, damage events,
    /// and currency changes. Updates RunStatistics incrementally. Records ZoneTimingEntry
    /// on zone transitions. Lives in Assembly-CSharp (needs KillCredited, CombatResultEvent).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RunLifecycleSystem))]
    public partial class RunStatisticsTrackingSystem : SystemBase
    {
        private EntityQuery _killQuery;
        private EntityQuery _combatResultQuery;
        private ComponentLookup<PlayerTag> _playerTagLookup;
        private RunPhase _lastPhase;
        private float _zoneStartTime;
        private int _zoneKills;
        private int _zoneDamageTaken;
        private int _zoneCurrencyEarned;
        private int _lastRunCurrency;
        private bool _statsInitialized;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<RunStatistics>();

            _killQuery = GetEntityQuery(ComponentType.ReadOnly<KillCredited>());
            _combatResultQuery = GetEntityQuery(ComponentType.ReadOnly<CombatResultEvent>());
            _playerTagLookup = GetComponentLookup<PlayerTag>(true);
        }

        protected override void OnUpdate()
        {
            _playerTagLookup.Update(this);

            var run = SystemAPI.GetSingleton<RunState>();
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();

            // Initialize/reset on run start
            if (run.Phase == RunPhase.Preparation && !_statsInitialized)
            {
                EntityManager.SetComponentData(runEntity, new RunStatistics
                {
                    FastestZoneTime = float.MaxValue
                });

                if (EntityManager.HasBuffer<ZoneTimingEntry>(runEntity))
                    EntityManager.GetBuffer<ZoneTimingEntry>(runEntity).Clear();

                _zoneStartTime = run.ElapsedTime;
                _zoneKills = 0;
                _zoneDamageTaken = 0;
                _zoneCurrencyEarned = 0;
                _lastRunCurrency = run.RunCurrency;
                _statsInitialized = true;
            }

            // Reset flag when run is not in progress
            if (run.Phase == RunPhase.None || run.Phase == RunPhase.Lobby)
                _statsInitialized = false;

            // Only track during active gameplay
            if (run.Phase != RunPhase.Active && run.Phase != RunPhase.BossEncounter &&
                run.Phase != RunPhase.ZoneTransition)
                return;

            var stats = EntityManager.GetComponentData<RunStatistics>(runEntity);
            bool dirty = false;

            // Track kills
            if (!_killQuery.IsEmptyIgnoreFilter)
            {
                int killCount = _killQuery.CalculateEntityCount();
                stats.TotalKills += killCount;
                _zoneKills += killCount;
                dirty = true;
            }

            // Track damage via CombatResultEvent (transient entities — use manual query)
            if (!_combatResultQuery.IsEmptyIgnoreFilter)
            {
                CompleteDependency();
                var events = _combatResultQuery.ToComponentDataArray<CombatResultEvent>(Unity.Collections.Allocator.Temp);

                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    if (!evt.DidHit) continue;

                    // ComponentLookup is cheaper than EntityManager.HasComponent per entity
                    if (_playerTagLookup.HasComponent(evt.AttackerEntity))
                    {
                        stats.DamageDealt += evt.FinalDamage;
                        dirty = true;
                    }

                    if (_playerTagLookup.HasComponent(evt.TargetEntity))
                    {
                        stats.DamageTaken += evt.FinalDamage;
                        _zoneDamageTaken += (int)evt.FinalDamage;
                        dirty = true;
                    }
                }

                events.Dispose();
            }

            // Track currency changes
            int currencyDelta = run.RunCurrency - _lastRunCurrency;
            if (currencyDelta > 0)
            {
                stats.RunCurrencyEarned += currencyDelta;
                _zoneCurrencyEarned += currencyDelta;
                dirty = true;
            }
            else if (currencyDelta < 0)
            {
                stats.RunCurrencySpent += -currencyDelta;
                dirty = true;
            }
            _lastRunCurrency = run.RunCurrency;

            // Detect zone transition — record zone timing
            if (run.Phase != _lastPhase)
            {
                if (_lastPhase == RunPhase.Active || _lastPhase == RunPhase.BossEncounter)
                {
                    // Zone just completed
                    float zoneDuration = run.ElapsedTime - _zoneStartTime;

                    if (EntityManager.HasBuffer<ZoneTimingEntry>(runEntity))
                    {
                        EntityManager.GetBuffer<ZoneTimingEntry>(runEntity).Add(new ZoneTimingEntry
                        {
                            ZoneIndex = run.CurrentZoneIndex,
                            ZoneType = 0, // Game-specific — default to combat
                            Duration = zoneDuration,
                            Kills = _zoneKills,
                            DamageTaken = _zoneDamageTaken,
                            CurrencyEarned = _zoneCurrencyEarned
                        });
                    }

                    stats.ZonesCleared++;

                    if (zoneDuration > 0f)
                    {
                        if (zoneDuration < stats.FastestZoneTime)
                            stats.FastestZoneTime = zoneDuration;
                        if (zoneDuration > stats.SlowestZoneTime)
                            stats.SlowestZoneTime = zoneDuration;
                    }

                    // Reset per-zone accumulators
                    _zoneKills = 0;
                    _zoneDamageTaken = 0;
                    _zoneCurrencyEarned = 0;
                    dirty = true;
                }

                if (run.Phase == RunPhase.Active || run.Phase == RunPhase.BossEncounter)
                    _zoneStartTime = run.ElapsedTime;

                _lastPhase = run.Phase;
            }

            // Only write back when something actually changed
            if (dirty)
            {
                stats.Version++;
                EntityManager.SetComponentData(runEntity, stats);
            }
        }
    }
}
