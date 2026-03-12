using Unity.Entities;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Transient entity request for one-shot stinger playback.
    /// Created via MusicStingerAPI, consumed and destroyed by MusicStingerSystem same frame.
    /// </summary>
    public struct MusicStingerRequest : IComponentData
    {
        /// <summary>References MusicDatabaseSO stinger definition.</summary>
        public int StingerId;

        /// <summary>Higher priority interrupts lower (death > loot > quest).</summary>
        public byte Priority;

        /// <summary>If true, plays alongside current stinger.</summary>
        public bool AllowOverlap;

        /// <summary>Multiplier on StingerVolume (default 1.0).</summary>
        public float VolumeScale;
    }
}
