using Unity.Entities;
using UnityEngine;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// EPIC 15.10: Registry for projectile prefabs.
    /// Add to a GameObject in your main SubScene (e.g., on Atlas_Server or a dedicated registry object).
    /// Weapons reference projectiles by their PrefabIndex.
    /// </summary>
    public class ProjectileRegistryAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct ProjectileEntry
        {
            [Tooltip("Index used by weapons to reference this projectile (e.g., ThrowableAction.ProjectilePrefabIndex)")]
            public int PrefabIndex;

            [Tooltip("Projectile prefab with Projectile components")]
            public GameObject ProjectilePrefab;

            [Tooltip("Projectile type")]
            public ProjectileType Type;

            [Tooltip("Default lifetime in seconds")]
            public float Lifetime;

            [Tooltip("Default damage (can be overridden by weapon)")]
            public float Damage;
        }

        [Header("Projectile Prefabs")]
        [Tooltip("Register all projectile prefabs here. Index must match weapon configs.")]
        public ProjectileEntry[] Projectiles;

        public class Baker : Baker<ProjectileRegistryAuthoring>
        {
            public override void Bake(ProjectileRegistryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                // Add buffer for projectile entries
                var buffer = AddBuffer<ProjectilePrefabElement>(entity);

                if (authoring.Projectiles != null)
                {
                    foreach (var proj in authoring.Projectiles)
                    {
                        if (proj.ProjectilePrefab == null)
                        {
                            Debug.LogWarning($"[ProjectileRegistry] Null prefab at index {proj.PrefabIndex}");
                            continue;
                        }

                        var prefabEntity = GetEntity(proj.ProjectilePrefab, TransformUsageFlags.Dynamic);

                        buffer.Add(new ProjectilePrefabElement
                        {
                            PrefabIndex = proj.PrefabIndex,
                            PrefabEntity = prefabEntity,
                            Lifetime = proj.Lifetime > 0 ? proj.Lifetime : 5f,
                            Damage = proj.Damage,
                            Type = proj.Type
                        });
                    }
                }

                // Mark as singleton for easy lookup
                AddComponent<ProjectileRegistrySingleton>(entity);
            }
        }
    }
}
