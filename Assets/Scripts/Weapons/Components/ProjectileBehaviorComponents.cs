using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;

namespace DIG.Weapons
{
    /// <summary>
    /// EPIC 15.13: Compositional projectile behavior components.
    /// These focused components allow mixing and matching projectile behaviors
    /// without modifying spawn systems.
    /// </summary>

    #region Impact Behaviors

    /// <summary>
    /// Deals damage on direct impact with an entity.
    /// Separate from explosion damage - this is for throwing knives, arrows, etc.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct DamageOnImpact : IComponentData
    {
        /// <summary>
        /// Damage dealt on direct hit.
        /// </summary>
        [GhostField]
        public float Damage;

        /// <summary>
        /// Type of damage (for resistance calculations).
        /// </summary>
        public DamageType DamageType;

        /// <summary>
        /// Whether to apply damage to the entity we directly hit.
        /// </summary>
        public bool ApplyToHitEntity;

        /// <summary>
        /// Splash damage radius (0 = no splash, just direct hit).
        /// </summary>
        public float DamageRadius;

        /// <summary>
        /// Damage falloff exponent for splash damage.
        /// 1.0 = linear, 2.0 = quadratic.
        /// </summary>
        public float DamageFalloff;
    }

    /// <summary>
    /// Causes projectile to stick to surfaces/entities on impact.
    /// Used for throwing knives, arrows, etc.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct StickOnImpact : IComponentData
    {
        /// <summary>
        /// Whether to stick to entities (players, enemies).
        /// </summary>
        public bool StickToEntities;

        /// <summary>
        /// Whether to stick to world geometry.
        /// </summary>
        public bool StickToWorld;

        /// <summary>
        /// How deep to embed into the surface (alias: EmbedDepth).
        /// </summary>
        public float PenetrationDepth;

        /// <summary>
        /// Whether to rotate projectile to align with surface normal.
        /// </summary>
        public bool AlignToSurface;

        /// <summary>
        /// Set to true when projectile has stuck to something.
        /// </summary>
        [GhostField]
        public bool IsStuck;
    }

    /// <summary>
    /// Refined bounce behavior component.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ProjectileBounce : IComponentData
    {
        /// <summary>
        /// Energy retained per bounce (0-1). 0.6 = 60% velocity retained.
        /// </summary>
        public float Bounciness;

        /// <summary>
        /// Maximum number of bounces before stopping.
        /// </summary>
        public int MaxBounces;

        /// <summary>
        /// Current bounce count.
        /// </summary>
        [GhostField]
        public int CurrentBounces;
    }

    #endregion

    #region Detonation Behaviors

    /// <summary>
    /// Deals area damage when projectile detonates.
    /// Works with DetonateOnTimer or DetonateOnImpact triggers.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct DamageOnDetonate : IComponentData
    {
        /// <summary>
        /// Damage at explosion center.
        /// </summary>
        [GhostField]
        public float Damage;

        /// <summary>
        /// Radius of damage effect.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Type of damage (for resistance calculations).
        /// </summary>
        public DamageType DamageType;

        /// <summary>
        /// Falloff exponent for damage over distance.
        /// 1.0 = linear, 2.0 = quadratic (realistic).
        /// </summary>
        public float FalloffExponent;

        /// <summary>
        /// Minimum damage multiplier at edge of radius (0-1).
        /// </summary>
        public float EdgeDamageMultiplier;
    }

    #endregion

    #region Status Effects

    /// <summary>
    /// Applies a status effect on impact with an entity.
    /// Used for poison arrows, fire arrows, etc.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ApplyStatusOnHit : IComponentData
    {
        /// <summary>
        /// Status effect to apply (matches StatusEffectType enum).
        /// </summary>
        public byte StatusType;

        /// <summary>
        /// Duration of the status effect.
        /// </summary>
        public float Duration;

        /// <summary>
        /// Intensity/magnitude of the effect.
        /// </summary>
        public float Intensity;
    }

    /// <summary>
    /// Applies a status effect in area when projectile detonates.
    /// Used for flashbangs, smoke grenades, etc.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ApplyStatusOnDetonate : IComponentData
    {
        /// <summary>
        /// Status effect to apply.
        /// </summary>
        public byte StatusType;

        /// <summary>
        /// Radius of effect.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Duration of the status effect.
        /// </summary>
        public float Duration;

        /// <summary>
        /// Intensity/magnitude of the effect.
        /// </summary>
        public float Intensity;
    }

    #endregion

    #region Area Effects

    /// <summary>
    /// Creates a persistent area effect when projectile detonates.
    /// Used for molotovs (fire), smoke grenades, gas, etc.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CreateAreaOnDetonate : IComponentData
    {
        /// <summary>
        /// Type of area to create.
        /// 0 = Fire, 1 = Smoke, 2 = Gas, 3 = Light
        /// </summary>
        public byte AreaType;

        /// <summary>
        /// Radius of the area effect.
        /// </summary>
        public float Radius;

        /// <summary>
        /// How long the area persists.
        /// </summary>
        public float Duration;

        /// <summary>
        /// Prefab entity to spawn for the area effect (optional).
        /// </summary>
        public Entity AreaPrefab;
    }

    #endregion

    #region Projectile Core (Simplified)

    /// <summary>
    /// Core projectile data - simplified version focusing on runtime state.
    /// Configuration should come from other components.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ProjectileCore : IComponentData
    {
        /// <summary>
        /// Maximum lifetime before auto-destroy.
        /// </summary>
        public float Lifetime;

        /// <summary>
        /// Time alive.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ElapsedTime;

        /// <summary>
        /// Entity that spawned this projectile (for damage attribution).
        /// </summary>
        [GhostField]
        public Entity Owner;

        /// <summary>
        /// Whether this projectile has detonated.
        /// </summary>
        [GhostField]
        public bool IsDetonated;
    }

    #endregion
}
