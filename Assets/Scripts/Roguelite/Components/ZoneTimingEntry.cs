using Unity.Entities;

namespace DIG.Roguelite.Analytics
{
    /// <summary>
    /// EPIC 23.6: Per-zone breakdown stored on RunState entity.
    /// One entry added per zone clear. Read at run end for analytics display.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ZoneTimingEntry : IBufferElementData
    {
        public byte ZoneIndex;
        public byte ZoneType;
        public float Duration;
        public int Kills;
        public int DamageTaken;
        public int CurrencyEarned;
    }
}
