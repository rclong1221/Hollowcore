using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using Audio.Systems;
using DIG.Surface;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// EPIC 13.18.5 / EPIC 15.24: Projectile Impact Presentation System
    ///
    /// Handles VFX, audio, and decal spawning when projectiles impact surfaces.
    /// Routes through SurfaceImpactQueue for unified processing.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ProjectileImpactPresentationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<ProjectileImpacted>();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (impacted, projectile, entity) in
                     SystemAPI.Query<RefRO<ProjectileImpacted>, RefRO<Projectile>>()
                     .WithAll<ProjectileImpacted>()
                     .WithEntityAccess())
            {
                var impact = impacted.ValueRO;
                var proj = projectile.ValueRO;

                // Calculate impact intensity based on projectile damage
                float intensity = math.clamp(proj.Damage / 50f, 0.2f, 1f);

                // Resolve surface material from hit entity
                int materialId = SurfaceDetectionService.GetDefaultId();
                if (impact.HitEntity != Entity.Null &&
                    EntityManager.HasComponent<SurfaceMaterialId>(impact.HitEntity))
                {
                    materialId = EntityManager.GetComponentData<SurfaceMaterialId>(impact.HitEntity).Id;
                }

                // EPIC 15.24: Route through unified SurfaceImpactQueue
                SurfaceImpactQueue.Enqueue(new SurfaceImpactData
                {
                    Position = impact.ImpactPoint,
                    Normal = impact.ImpactNormal,
                    Velocity = float3.zero,
                    SurfaceId = SurfaceID.Default, // Resolved by presenter from materialId
                    ImpactClass = ImpactClassResolver.FromDamage(proj.Damage),
                    SurfaceMaterialId = materialId,
                    Intensity = intensity,
                    LODTier = EffectLODTier.Full // Computed by presenter
                });

                // Mark as processed (remove the component so we don't process again)
                ecb.RemoveComponent<ProjectileImpacted>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
