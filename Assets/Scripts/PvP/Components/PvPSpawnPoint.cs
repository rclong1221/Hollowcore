using Unity.Entities;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Placed on entities in arena subscenes marking valid spawn locations.
    /// PvPSpawnSystem queries these to teleport respawning players.
    /// 8 bytes.
    /// </summary>
    public struct PvPSpawnPoint : IComponentData
    {
        public byte TeamId;
        public byte SpawnIndex;
        public byte IsActive;
        public byte Padding;
        public uint LastUsedTick;
    }
}
