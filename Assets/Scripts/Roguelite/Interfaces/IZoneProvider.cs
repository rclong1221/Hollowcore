using Unity.Mathematics;
using UnityEngine;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Games implement this for their level technology.
    /// Examples: voxel cave generation, prefab room stitching, additive scene loading,
    /// hand-crafted zone activation, large open-world terrain with POIs.
    /// </summary>
    public interface IZoneProvider
    {
        /// <summary>Begin loading/generating a zone. May be async (scene loading, generation, etc).</summary>
        void Initialize(uint seed, int zoneIndex, ZoneDefinitionSO definition);

        /// <summary>True when zone geometry, navmesh, lighting, and all assets are ready.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Make zone playable. Called once after IsReady returns true.
        /// Returns activation data the framework uses for player placement,
        /// spawn point registration, and interactable placement.
        /// </summary>
        ZoneActivationResult Activate();

        /// <summary>Tear down zone. Unload assets, destroy spawned entities, cleanup.</summary>
        void Deactivate();

        /// <summary>Full cleanup on run end or provider swap.</summary>
        void Dispose();
    }

    /// <summary>
    /// Data returned by IZoneProvider.Activate(). The framework reads this
    /// to place the player, register spawn points, and seed interactables.
    /// All arrays are optional — null = not applicable for this zone style.
    /// </summary>
    public struct ZoneActivationResult
    {
        /// <summary>Where to teleport the player (or first player in co-op).</summary>
        public float3 PlayerSpawnPosition;

        /// <summary>Additional spawn positions for co-op players. Null = offset from primary.</summary>
        public float3[] CoopSpawnPositions;

        /// <summary>
        /// Valid positions where enemies can spawn. Null = game uses ISpawnPositionProvider
        /// for dynamic position sampling (navmesh queries, off-screen, etc).
        /// </summary>
        public float3[] SpawnPoints;

        /// <summary>
        /// Positions where interactables (chests, shrines, equipment) can appear.
        /// Null = no interactables or game handles placement itself.
        /// </summary>
        public float3[] InteractableNodes;

        /// <summary>
        /// Position of the zone exit / teleporter / portal.
        /// float3.zero with HasExit=false means no fixed exit.
        /// </summary>
        public float3 ExitPosition;

        /// <summary>Whether this zone has a fixed exit position.</summary>
        public bool HasExit;

        /// <summary>
        /// Axis-aligned bounds of the playable area. Used by spawn director for
        /// distance-from-player checks and off-screen spawning. Zero = unbounded.
        /// </summary>
        public Bounds PlayableArea;
    }
}
