using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Audio.Systems;
using DIG.Combat.Systems;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24: Bridges server-side hitscan hit data to the SurfaceImpactQueue.
    /// Reads EnvironmentHitRequest entities (from WeaponFireSystem) and PendingCombatHit entities
    /// (for enemy-hit VFX at hit point), enqueues SurfaceImpactData events.
    /// Runs BEFORE CombatResolutionSystem so PendingCombatHit entities are still available.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(CombatResolutionSystem))]
    public partial class HitscanImpactBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process environment hit requests (created by WeaponFireSystem)
            foreach (var (request, entity) in
                     SystemAPI.Query<RefRO<EnvironmentHitRequest>>()
                     .WithEntityAccess())
            {
                var req = request.ValueRO;

                // Resolve surface material from the hit (default for now — terrain/material detection via BlobAsset in Phase 5)
                int materialId = SurfaceDetectionService.GetDefaultId();

                SurfaceImpactQueue.Enqueue(new SurfaceImpactData
                {
                    Position = req.Position,
                    Normal = req.Normal,
                    Velocity = req.Velocity,
                    SurfaceId = SurfaceID.Default, // Resolved by presenter from materialId
                    ImpactClass = ImpactClass.Bullet_Medium,
                    SurfaceMaterialId = materialId,
                    Intensity = 1f,
                    LODTier = EffectLODTier.Full // Computed by presenter
                });

                ecb.DestroyEntity(entity);
            }

            // Process PendingCombatHit entities for enemy-hit VFX (blood/armor sparks)
            foreach (var (hit, entity) in
                     SystemAPI.Query<RefRO<PendingCombatHit>>()
                     .WithEntityAccess())
            {
                var h = hit.ValueRO;
                if (!h.WasPhysicsHit) continue; // Only physics-based hits produce impact VFX

                // Enemy hit — use Flesh as default surface for living targets
                SurfaceImpactQueue.Enqueue(new SurfaceImpactData
                {
                    Position = h.HitPoint,
                    Normal = h.HitNormal,
                    Velocity = h.AttackDirection * h.HitDistance,
                    SurfaceId = SurfaceID.Flesh,
                    ImpactClass = ImpactClass.Bullet_Medium,
                    SurfaceMaterialId = SurfaceDetectionService.GetDefaultId(),
                    Intensity = math.clamp(h.WeaponData.BaseDamage / 50f, 0.2f, 1f),
                    LODTier = EffectLODTier.Full
                });
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
