using Unity.Collections;
using Unity.Entities;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Permanent landmark entity for map discovery, compass, and fast travel.
    /// 20 bytes — placed on town markers, dungeon entrances, boss arenas, fast travel points.
    /// </summary>
    public struct PointOfInterest : IComponentData
    {
        public int POIId;                           // Stable unique ID (matches POIRegistrySO)
        public POIType Type;                        // byte
        public FixedString32Bytes Label;           // Display name
        public bool DiscoveredByPlayer;            // Client-side, set by FogOfWarSystem on reveal
    }

    public enum POIType : byte
    {
        Town       = 0,
        Dungeon    = 1,
        BossArena  = 2,
        FastTravel = 3,
        Landmark   = 4,
        Camp       = 5,
        Cave       = 6,
        Ruins      = 7,
        Shrine     = 8,
        Vendor     = 9
    }
}
