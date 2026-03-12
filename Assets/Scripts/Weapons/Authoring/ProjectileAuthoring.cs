using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// Bakes Projectile, ProjectileMovement, and ProjectileImpact components onto projectile prefabs.
    /// CRITICAL: These components must be baked into the prefab for NetCode ghost replication to work.
    /// Without this authoring, dynamically added GhostField components won't replicate to clients.
    /// </summary>
    public class ProjectileAuthoring : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [Tooltip("Base damage dealt by this projectile")]
        public float Damage = 100f;

        [Tooltip("Type of projectile (Bullet, Grenade, Arrow, etc)")]
        public ProjectileType Type = ProjectileType.Grenade;

        [Tooltip("How long the projectile lives before being destroyed")]
        public float Lifetime = 5f;

        [Header("Movement Settings")]
        [Tooltip("Whether this projectile is affected by gravity")]
        public bool HasGravity = true;

        [Tooltip("Gravity strength (default 9.81)")]
        public float Gravity = 9.81f;

        [Tooltip("Air drag coefficient")]
        public float Drag = 0.1f;

        [Header("Impact Settings")]
        [Tooltip("Whether the projectile bounces on impact")]
        public bool BounceOnImpact = true;

        [Tooltip("Maximum number of bounces before stopping")]
        public int MaxBounces = 3;

        [Tooltip("Whether the projectile explodes on impact")]
        public bool ExplodeOnImpact = false;

        [Tooltip("Radius of impact explosion (if ExplodeOnImpact is true)")]
        public float ImpactRadius = 0f;

        public class Baker : Baker<ProjectileAuthoring>
        {
            public override void Bake(ProjectileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Bake Projectile component - tracks projectile state
                AddComponent(entity, new Projectile
                {
                    Damage = authoring.Damage,
                    ExplosionRadius = authoring.ImpactRadius,
                    Lifetime = authoring.Lifetime,
                    ElapsedTime = 0f,
                    Type = authoring.Type,
                    Owner = Entity.Null // Set at runtime by spawner
                });

                // Bake ProjectileMovement component - physics state
                // Velocity is set at runtime by spawner, but component must exist for ghost replication
                AddComponent(entity, new ProjectileMovement
                {
                    Velocity = float3.zero, // Set at runtime
                    Gravity = authoring.Gravity,
                    Drag = authoring.Drag,
                    HasGravity = authoring.HasGravity
                });

                // Bake ProjectileImpact component - collision behavior
                AddComponent(entity, new ProjectileImpact
                {
                    Damage = authoring.Damage,
                    ImpactRadius = authoring.ImpactRadius,
                    ExplodeOnImpact = authoring.ExplodeOnImpact,
                    BounceOnImpact = authoring.BounceOnImpact,
                    MaxBounces = authoring.MaxBounces,
                    CurrentBounces = 0
                });
            }
        }
    }
}
