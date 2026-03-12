using Unity.Entities;
using Player.Components;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 6: Place on trigger volume entities to create surface-conditional
    /// damage zones. Entity inside the zone only takes damage if their GroundSurfaceState
    /// matches the required SurfaceID. Enables "lava floor" without damaging flying entities.
    /// </summary>
    public struct SurfaceDamageZone : IComponentData
    {
        /// <summary>Damage per second while standing on matching surface inside zone.</summary>
        public float DamagePerSecond;

        /// <summary>Which DamageType to apply.</summary>
        public DamageType DamageType;

        /// <summary>
        /// Required SurfaceID to take damage. Entity must be standing on this surface
        /// AND inside the zone trigger. SurfaceID.Default = any surface triggers damage.
        /// </summary>
        public SurfaceID RequiredSurfaceId;

        /// <summary>Damage tick interval in seconds. Default 0.5s.</summary>
        public float TickInterval;

        /// <summary>Ramp-up time before full damage (seconds).</summary>
        public float RampUpDuration;

        /// <summary>If true, applies to any entity (players + NPCs). If false, players only.</summary>
        public bool AffectsNPCs;
    }
}
