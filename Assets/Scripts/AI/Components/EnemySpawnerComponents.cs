using Unity.Entities;
using Unity.NetCode;

namespace DIG.AI.Components
{
    /// <summary>
    /// Configuration and state for an enemy spawner entity.
    /// Supports spawning from 1 to 1,000,000 entities with frame-budgeted batching.
    ///
    /// Place on a subscene GameObject with EnemySpawnerAuthoring.
    /// The system spawns server-side; NetCode ghost replication handles clients.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct EnemySpawner : IComponentData
    {
        /// <summary>Ghost prefab to instantiate.</summary>
        public Entity Prefab;

        /// <summary>Total number of entities to spawn.</summary>
        public int TotalCount;

        /// <summary>Max entities to instantiate per frame (controls frame budget).</summary>
        public int BatchSize;

        /// <summary>Radius around spawner to scatter entities. 0 = all at spawner position.</summary>
        public float SpawnRadius;

        /// <summary>Grid spacing for organized placement. 0 = random scatter within radius.</summary>
        public float GridSpacing;

        /// <summary>Vertical offset so entities don't spawn inside the ground.</summary>
        public float YOffset;

        /// <summary>Whether to start spawning immediately on load.</summary>
        public bool SpawnOnStart;

        /// <summary>Random seed for deterministic placement. 0 = use entity index.</summary>
        public uint Seed;

        // --- Runtime state ---

        /// <summary>How many entities have been spawned so far.</summary>
        public int SpawnedCount;

        /// <summary>Whether the spawner is actively spawning.</summary>
        public bool IsSpawning;

        /// <summary>Whether all entities have been spawned.</summary>
        public bool IsComplete;
    }

    /// <summary>
    /// Add this tag to trigger a spawner to begin spawning.
    /// Automatically removed after spawning starts.
    /// </summary>
    public struct EnemySpawnRequest : IComponentData { }
}
