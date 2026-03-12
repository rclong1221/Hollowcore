using Unity.Entities;

namespace DIG.Roguelite.Analytics
{
    /// <summary>
    /// EPIC 23.6: Tracks per-run performance metrics. Lives on RunState entity.
    /// Updated incrementally by RunStatisticsTrackingSystem on kill/damage/currency events.
    /// Reset on run start. Read by RunHistoryRecordSystem on run end.
    /// </summary>
    public struct RunStatistics : IComponentData
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

        /// <summary>
        /// Monotonically increasing counter. Incremented by RunStatisticsTrackingSystem
        /// on any field change. RunAnalyticsBridgeSystem compares against its cached
        /// version to avoid pushing unchanged snapshots.
        /// </summary>
        public uint Version;
    }
}
