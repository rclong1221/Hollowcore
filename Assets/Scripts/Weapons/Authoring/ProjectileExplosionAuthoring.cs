using Unity.Entities;
using UnityEngine;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// EPIC 15.10: Authoring component for projectile explosions.
    /// Add this to projectile prefabs to enable voxel destruction on detonation.
    ///
    /// Supports two trigger modes:
    /// - Timer: Explodes after FuseTime seconds (like a grenade)
    /// - Impact: Explodes on collision (like a rocket/RPG)
    /// - Both: Explodes on whichever happens first
    /// </summary>
    [AddComponentMenu("DIG/Projectiles/Projectile Explosion Config")]
    public class ProjectileExplosionAuthoring : MonoBehaviour
    {
        [Header("Explosion Configuration")]
        [Tooltip("Radius of the voxel destruction crater")]
        [Range(1f, 20f)]
        public float explosionRadius = 4f;

        [Tooltip("Damage dealt to voxels at center (falls off with distance)")]
        [Range(10f, 1000f)]
        public float explosionDamage = 100f;

        [Tooltip("Whether to drop loot from destroyed voxels")]
        public bool spawnLoot = true;

        [Header("Trigger Mode")]
        [Tooltip("Explode after this many seconds (0 = use projectile lifetime)")]
        [Range(0f, 30f)]
        public float fuseTime = 3f;

        [Tooltip("Explode on impact with surfaces/entities")]
        public bool detonateOnImpact = false;

        [Tooltip("Explode when timer expires")]
        public bool detonateOnTimer = true;

        public class ProjectileExplosionBaker : Baker<ProjectileExplosionAuthoring>
        {
            public override void Bake(ProjectileExplosionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Always add explosion config
                AddComponent(entity, new ProjectileExplosionConfig
                {
                    ExplosionRadius = authoring.explosionRadius,
                    ExplosionDamage = authoring.explosionDamage,
                    SpawnLoot = authoring.spawnLoot
                });

                // Add trigger components based on settings
                if (authoring.detonateOnTimer)
                {
                    AddComponent(entity, new DetonateOnTimer
                    {
                        FuseTime = authoring.fuseTime
                    });
                }

                if (authoring.detonateOnImpact)
                {
                    AddComponent(entity, new DetonateOnImpact());
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw explosion radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);

            // Draw inner core (full damage)
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius * 0.3f);
        }
#endif
    }
}
