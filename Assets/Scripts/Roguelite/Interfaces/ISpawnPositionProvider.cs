using Unity.Mathematics;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Games implement this to resolve WHERE enemies spawn in their zone geometry.
    /// The spawn director calls this when it needs a position for a new spawn request.
    ///
    /// Examples:
    ///   NavMeshSpawnProvider     — samples random navmesh points at distance from player
    ///   MarkerSpawnProvider      — picks from ZoneActivationResult.SpawnPoints
    ///   OffScreenSpawnProvider   — spawns just outside camera view (top-down games)
    ///   NestSpawnProvider        — spawns near pre-placed nest/hive objects
    /// </summary>
    public interface ISpawnPositionProvider
    {
        /// <summary>
        /// Get a valid spawn position for an enemy.
        /// Returns false if no valid position found this frame (all points occupied, etc).
        /// </summary>
        bool TryGetSpawnPosition(
            float3 playerPosition,
            float minDistance,
            float maxDistance,
            ref Random rng,
            out float3 position);
    }
}
