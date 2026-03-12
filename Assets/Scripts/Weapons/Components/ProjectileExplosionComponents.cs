using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Weapons
{
    /// <summary>
    /// Configuration for projectile explosions.
    /// Add this to any projectile that should create a voxel explosion.
    /// Works with both timer-based and impact-based triggers.
    /// </summary>
    [GhostComponent]
    public struct ProjectileExplosionConfig : IComponentData
    {
        /// <summary>
        /// Radius of the voxel explosion crater.
        /// </summary>
        public float ExplosionRadius;

        /// <summary>
        /// Damage dealt to voxels at explosion center.
        /// Falls off based on falloff settings.
        /// </summary>
        public float ExplosionDamage;

        /// <summary>
        /// Whether to spawn loot from destroyed voxels.
        /// </summary>
        public bool SpawnLoot;
    }

    /// <summary>
    /// Tag component for projectiles that explode after a timer.
    /// When Projectile.ElapsedTime >= FuseTime, the projectile detonates.
    /// </summary>
    [GhostComponent]
    public struct DetonateOnTimer : IComponentData
    {
        /// <summary>
        /// Time in seconds before detonation.
        /// If 0, uses Projectile.Lifetime as the fuse time.
        /// </summary>
        public float FuseTime;
    }

    /// <summary>
    /// Tag component for projectiles that explode on impact.
    /// When ProjectileImpacted is added, the projectile detonates.
    /// Can be combined with DetonateOnTimer for "impact OR timer" behavior.
    /// </summary>
    [GhostComponent]
    public struct DetonateOnImpact : IComponentData
    {
        // Tag component - no data needed.
        // Presence indicates impact should trigger detonation.
    }

    /// <summary>
    /// Added to projectiles that have triggered detonation.
    /// Prevents double-detonation from both timer and impact.
    /// </summary>
    [GhostComponent]
    public struct ProjectileDetonated : IComponentData
    {
        /// <summary>
        /// Position where the detonation occurred.
        /// </summary>
        public float3 DetonationPoint;
    }
}
