using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Completed run summary stored on the MetaBank entity.
    /// Heap-allocated buffer — stores last N runs for history/analytics.
    /// Persisted via RunHistorySaveModule (TypeId=17).
    /// </summary>
    [InternalBufferCapacity(0)] // Heap-allocated — stores many entries
    public struct RunHistoryEntry : IBufferElementData
    {
        /// <summary>Unique run identifier.</summary>
        public uint RunId;

        /// <summary>Master seed used for this run.</summary>
        public uint Seed;

        /// <summary>Ascension/heat level for this run.</summary>
        public byte AscensionLevel;

        /// <summary>How the run ended.</summary>
        public RunEndReason EndReason;

        /// <summary>Number of zones cleared before run end.</summary>
        public byte ZonesCleared;

        /// <summary>Final calculated score.</summary>
        public int Score;

        /// <summary>Total run duration in seconds.</summary>
        public float Duration;

        /// <summary>Meta-currency earned from this run.</summary>
        public int MetaCurrencyEarned;

        /// <summary>Total kills during this run (EPIC 23.6).</summary>
        public int TotalKills;

        /// <summary>Unix timestamp (seconds) when the run ended.</summary>
        public long Timestamp;
    }
}
