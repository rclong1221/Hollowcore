using System.Collections.Generic;
using UnityEngine;
using DIG.Roguelite.Analytics;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.6: Snapshot of per-run statistics for UI display.
    /// </summary>
    public struct RunStatisticsSnapshot
    {
        public int TotalKills;
        public int EliteKills;
        public int BossKills;
        public float DamageTaken;
        public float DamageDealt;
        public int ItemsCollected;
        public int RunCurrencySpent;
        public int RunCurrencyEarned;
        public byte ZonesCleared;
        public byte ModifiersAcquired;
        public float FastestZoneTime;
        public float SlowestZoneTime;
    }

    /// <summary>
    /// EPIC 23.6: Snapshot of a single zone's timing data for UI display.
    /// </summary>
    public struct ZoneTimingSnapshot
    {
        public byte ZoneIndex;
        public byte ZoneType;
        public float Duration;
        public int Kills;
        public int DamageTaken;
        public int CurrencyEarned;
    }

    /// <summary>
    /// EPIC 23.6: Provider interface for game-specific analytics/stats HUD.
    /// Games implement this to display post-run stats screen, live run stats, etc.
    /// </summary>
    public interface IRunAnalyticsProvider
    {
        void OnRunStatisticsChanged(RunStatisticsSnapshot stats);
        void OnZoneCompleted(ZoneTimingSnapshot zoneTiming);
        void OnRunHistoryChanged(IReadOnlyList<RunHistoryEntry> history);
    }

    /// <summary>
    /// EPIC 23.6: Central registry for run analytics UI providers.
    /// Follows RunUIRegistry pattern: register/unregister with replacement warning.
    /// </summary>
    public static class RunAnalyticsRegistry
    {
        private static IRunAnalyticsProvider _provider;
        private static RunStatisticsSnapshot _lastStats;

        public static IRunAnalyticsProvider Provider => _provider;
        public static bool HasProvider => _provider != null;
        public static RunStatisticsSnapshot LastStats => _lastStats;

        public static void Register(IRunAnalyticsProvider provider)
        {
            if (_provider != null && provider != null)
                Debug.LogWarning("[RunAnalyticsRegistry] Replacing existing analytics provider.");
            _provider = provider;
        }

        public static void Unregister(IRunAnalyticsProvider provider)
        {
            if (_provider == provider)
                _provider = null;
        }

        public static void SetStats(RunStatisticsSnapshot stats)
        {
            _lastStats = stats;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _provider = null;
            _lastStats = default;
        }
    }
}
