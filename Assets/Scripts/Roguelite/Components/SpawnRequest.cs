using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Transient entity created by SpawnDirectorSystem.
    /// Game's spawner system consumes these and instantiates actual enemies.
    /// Bridges framework orchestration -> game instantiation.
    /// </summary>
    public struct SpawnRequest : IComponentData
    {
        public Entity Prefab;
        public float3 Position;
        public uint Seed;
        public float Difficulty;
        public bool IsElite;
        public int PoolEntryIndex;
    }

    /// <summary>
    /// Cleanup component for tracking consumed spawn requests.
    /// </summary>
    public struct SpawnRequestConsumed : ICleanupComponentData { }
}
