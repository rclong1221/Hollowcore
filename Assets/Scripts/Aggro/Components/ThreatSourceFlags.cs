using System;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.33: Bitmask identifying how threat was generated.
    /// Each system that adds threat OR's in its flag, building a complete
    /// provenance record per ThreatEntry.
    /// </summary>
    [Flags]
    public enum ThreatSourceFlags : byte
    {
        None      = 0,
        Damage    = 1 << 0,
        Vision    = 1 << 1,
        Hearing   = 1 << 2,
        Social    = 1 << 3,
        Proximity = 1 << 4,
        Taunt     = 1 << 5,
        Healing   = 1 << 6,
    }
}
