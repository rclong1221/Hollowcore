using Unity.Entities;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Map icon classification on non-player entities (enemies, NPCs, POIs, etc.).
    /// 8 bytes — placed by MapIconAuthoring baker. NOT on the player entity (0 archetype impact).
    /// </summary>
    public struct MapIcon : IComponentData
    {
        public MapIconType IconType;       // byte
        public byte Priority;             // 0=low, 255=highest (overlap resolution)
        public bool VisibleOnMinimap;     // 1 byte
        public bool VisibleOnWorldMap;    // 1 byte
        public uint CustomColorPacked;   // RGBA packed (0 = use theme default)
    }

    public enum MapIconType : byte
    {
        Player       = 0,
        PartyMember  = 1,
        Enemy        = 2,
        NPC          = 3,
        QuestObjective = 4,
        QuestGiver   = 5,
        Vendor       = 6,
        CraftStation = 7,
        Loot         = 8,
        POI          = 9,
        FastTravel   = 10,
        Danger       = 11,
        Boss         = 12
    }
}
