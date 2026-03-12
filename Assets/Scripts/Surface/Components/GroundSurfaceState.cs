using Unity.Entities;
using Unity.NetCode;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 1: Bit flags for surface properties cached on GroundSurfaceState.
    /// Burst jobs can branch without managed SurfaceMaterial lookups.
    /// </summary>
    [System.Flags]
    public enum SurfaceFlags : byte
    {
        None = 0,
        IsSlippery = 1 << 0,
        IsLiquid = 1 << 1,
        AllowsRicochet = 1 << 2,
        AllowsPenetration = 1 << 3
    }

    /// <summary>
    /// EPIC 16.10 Phase 1: Add to any entity that needs to know what surface it stands on.
    /// GroundSurfaceQuerySystem writes results; gameplay systems read them.
    /// Players, NPCs, enemies, vehicles — anything that touches the ground.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct GroundSurfaceState : IComponentData
    {
        /// <summary>SurfaceMaterial.Id of the ground surface. -1 = unknown/airborne.</summary>
        [GhostField] public int SurfaceMaterialId;

        /// <summary>SurfaceID enum value for fast Burst-friendly switch statements.</summary>
        [GhostField] public SurfaceID SurfaceId;

        /// <summary>Whether the entity is currently on solid ground.</summary>
        [GhostField] public bool IsGrounded;

        /// <summary>Elapsed time since last query (internal).</summary>
        public float TimeSinceLastQuery;

        /// <summary>How often to raycast (seconds). 0 = every frame.</summary>
        public float QueryInterval;

        /// <summary>Surface hardness cached from SurfaceMaterial (0-255).</summary>
        [GhostField] public byte CachedHardness;

        /// <summary>Surface density cached from SurfaceMaterial (0-255).</summary>
        [GhostField] public byte CachedDensity;

        /// <summary>Cached flags for fast Burst checks.</summary>
        [GhostField] public SurfaceFlags Flags;

        public static GroundSurfaceState Default => new()
        {
            SurfaceMaterialId = -1,
            SurfaceId = SurfaceID.Default,
            IsGrounded = false,
            TimeSinceLastQuery = 0f,
            QueryInterval = 0.25f,
            CachedHardness = 128,
            CachedDensity = 128,
            Flags = SurfaceFlags.None
        };
    }
}
