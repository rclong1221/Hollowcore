using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Icon entry written by MapIconUpdateSystem, read by MapUIBridgeSystem.
    /// </summary>
    public struct MapIconEntry
    {
        public float2 WorldPos2D;          // XZ world position
        public MapIconType IconType;       // byte
        public byte Priority;             // for overlap resolution
        public uint ColorPacked;          // RGBA or 0 for theme default
        public Entity SourceEntity;       // for click-to-track in world map
    }

    /// <summary>
    /// EPIC 17.6: Compass entry written by CompassSystem, read by MapUIBridgeSystem.
    /// </summary>
    public struct CompassEntry
    {
        public float Angle;                // Radians from player forward direction
        public float Distance;             // World-space distance from player
        public MapIconType IconType;       // byte
        public FixedString32Bytes Label;  // POI label
        public bool IsQuestWaypoint;      // Highlighted differently on compass
    }

    /// <summary>
    /// EPIC 17.6: Discovered POI record for save/load.
    /// </summary>
    public struct DiscoveredPOIRecord
    {
        public int POIId;
        public float DiscoverTimestamp;    // Elapsed playtime when discovered
    }
}
