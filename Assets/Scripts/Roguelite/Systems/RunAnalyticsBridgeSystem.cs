using Unity.Entities;
using DIG.Roguelite.Analytics;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.6: Reads RunStatistics + ZoneTimingEntry from ECS.
    /// Pushes snapshots to RunAnalyticsRegistry for game HUD (post-run stats screen).
    /// Uses RunStatistics.Version for precise change detection — only pushes when
    /// the tracking system actually modified data. Covers all fields (damage, currency, kills).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RunAnalyticsBridgeSystem : SystemBase
    {
        private uint _lastVersion;
        private int _lastZoneTimingCount;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<RunStatistics>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<RunState>(out var runEntity))
                return;

            var stats = EntityManager.GetComponentData<RunStatistics>(runEntity);

            // Version-based change detection — catches ALL field changes
            // (kills, damage, currency, zones, modifiers, etc.)
            if (stats.Version != _lastVersion)
            {
                _lastVersion = stats.Version;

                var snapshot = new RunStatisticsSnapshot
                {
                    TotalKills = stats.TotalKills,
                    EliteKills = stats.EliteKills,
                    BossKills = stats.BossKills,
                    DamageTaken = stats.DamageTaken,
                    DamageDealt = stats.DamageDealt,
                    ItemsCollected = stats.ItemsCollected,
                    RunCurrencySpent = stats.RunCurrencySpent,
                    RunCurrencyEarned = stats.RunCurrencyEarned,
                    ZonesCleared = stats.ZonesCleared,
                    ModifiersAcquired = stats.ModifiersAcquired,
                    FastestZoneTime = stats.FastestZoneTime,
                    SlowestZoneTime = stats.SlowestZoneTime
                };

                RunAnalyticsRegistry.SetStats(snapshot);

                if (RunAnalyticsRegistry.HasProvider)
                    RunAnalyticsRegistry.Provider.OnRunStatisticsChanged(snapshot);
            }

            // Check zone timing buffer for new entries
            if (EntityManager.HasBuffer<ZoneTimingEntry>(runEntity))
            {
                var timings = EntityManager.GetBuffer<ZoneTimingEntry>(runEntity);
                if (timings.Length > _lastZoneTimingCount)
                {
                    // Push new zone completions
                    for (int i = _lastZoneTimingCount; i < timings.Length; i++)
                    {
                        var t = timings[i];
                        var zoneSnapshot = new ZoneTimingSnapshot
                        {
                            ZoneIndex = t.ZoneIndex,
                            ZoneType = t.ZoneType,
                            Duration = t.Duration,
                            Kills = t.Kills,
                            DamageTaken = t.DamageTaken,
                            CurrencyEarned = t.CurrencyEarned
                        };

                        if (RunAnalyticsRegistry.HasProvider)
                            RunAnalyticsRegistry.Provider.OnZoneCompleted(zoneSnapshot);
                    }
                    _lastZoneTimingCount = timings.Length;
                }
            }

            // Reset tracking when a new run starts (Version resets to 0)
            if (stats.Version == 0 && _lastZoneTimingCount > 0)
            {
                _lastZoneTimingCount = 0;
                _lastVersion = 0;
            }
        }
    }
}
