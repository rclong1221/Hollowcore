using Unity.Collections;
using Unity.Entities;

namespace DIG.Weapons
{
    /// <summary>
    /// EPIC 15.10: Tag to identify the projectile registry singleton.
    /// Query for this to find the registry entity.
    /// </summary>
    public struct ProjectileRegistrySingleton : IComponentData { }

    /// <summary>
    /// EPIC 15.10: Buffer element for projectile prefab entries.
    /// Index matches ProjectilePrefabIndex in weapon configs.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ProjectilePrefabElement : IBufferElementData
    {
        /// <summary>
        /// Index used by weapons to reference this projectile.
        /// Must match ThrowableAction.ProjectilePrefabIndex, etc.
        /// </summary>
        public int PrefabIndex;

        /// <summary>
        /// The baked prefab entity to instantiate.
        /// </summary>
        public Entity PrefabEntity;

        /// <summary>
        /// Default lifetime for this projectile type.
        /// </summary>
        public float Lifetime;

        /// <summary>
        /// Default damage for this projectile type.
        /// Can be overridden by weapon config.
        /// </summary>
        public float Damage;

        /// <summary>
        /// Projectile type for physics/behavior.
        /// </summary>
        public ProjectileType Type;
    }
}
