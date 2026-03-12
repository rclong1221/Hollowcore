using Unity.Entities;
using Unity.Mathematics;
using Audio.Systems;
using DIG.Surface.Debug;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 3: Ricochet &amp; Penetration System.
    /// Reads EnvironmentHitRequest entities (before HitscanImpactBridgeSystem destroys them),
    /// resolves surface material properties, and enqueues additional VFX events for
    /// ricochet sparks and penetration exit effects.
    /// The base impact VFX is still handled by HitscanImpactBridgeSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(HitscanImpactBridgeSystem))]
    public partial class RicochetPenetrationSystem : SystemBase
    {
        private SurfaceMaterialRegistry _registry;

        protected override void OnUpdate()
        {
            using var _marker = SurfaceFXProfiler.RicochetPenetrationMarker.Auto();

            if (_registry == null)
                _registry = UnityEngine.Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
            if (_registry == null) return;

            foreach (var (request, entity) in
                     SystemAPI.Query<RefRO<EnvironmentHitRequest>>()
                     .WithEntityAccess())
            {
                var req = request.ValueRO;

                // Need velocity to compute incidence angle
                if (math.lengthsq(req.Velocity) < 0.01f) continue;

                // Resolve surface material
                int materialId = SurfaceDetectionService.GetDefaultId();
                if (!_registry.TryGetById(materialId, out var material) || material == null)
                    material = _registry.DefaultMaterial;
                if (material == null) continue;

                float3 velDir = math.normalize(req.Velocity);
                float3 normal = math.normalizesafe(req.Normal);

                // Incident angle: angle between incoming velocity and surface normal
                // dot(-velDir, normal) = cos(angle). 1.0 = head-on, 0.0 = parallel/grazing
                float cosAngle = math.dot(-velDir, normal);
                float incidentAngle = math.degrees(math.acos(math.clamp(cosAngle, 0f, 1f)));

                // Ricochet check
                if (material.AllowsRicochet && incidentAngle > GetRicochetThreshold(material.Hardness))
                {
                    EnqueueRicochet(req, normal, velDir, material);
                    SurfaceFXProfiler.RicochetsThisFrame++;
                }

                // Penetration check
                if (material.AllowsPenetration)
                {
                    float bulletPower = math.length(req.Velocity);
                    float surfaceResistance = material.Density;
                    if (bulletPower > surfaceResistance)
                    {
                        EnqueuePenetrationExit(req, normal, velDir, material);
                        SurfaceFXProfiler.PenetrationsThisFrame++;
                    }
                }
            }
        }

        /// <summary>
        /// Grazing angle threshold in degrees based on surface hardness.
        /// Harder surfaces ricochet at steeper angles.
        /// Hardness 0 → 75° (only very grazing), Hardness 255 → 30° (ricochets easily)
        /// </summary>
        private float GetRicochetThreshold(byte hardness)
        {
            return 75f - (hardness / 255f) * 45f;
        }

        private void EnqueueRicochet(EnvironmentHitRequest req, float3 normal, float3 velDir, SurfaceMaterial material)
        {
            float3 reflected = math.reflect(velDir, normal);

            // Spark trail at impact point, oriented along reflection
            SurfaceImpactQueue.Enqueue(new SurfaceImpactData
            {
                Position = req.Position,
                Normal = normal,
                Velocity = reflected * math.length(req.Velocity) * 0.6f,
                SurfaceId = SurfaceIdResolver.FromMaterial(material),
                ImpactClass = ImpactClass.Environmental, // Ricochet sparks use environmental class
                SurfaceMaterialId = material.Id,
                Intensity = 0.7f,
                LODTier = EffectLODTier.Full
            });
        }

        private void EnqueuePenetrationExit(EnvironmentHitRequest req, float3 normal, float3 velDir, SurfaceMaterial material)
        {
            // Exit point: offset slightly along bullet direction past the surface
            float penetrationDepth = 0.15f; // Approximate thin wall thickness
            float3 exitPosition = req.Position + velDir * penetrationDepth;
            float3 exitNormal = -normal; // Exit normal faces opposite direction

            // Exit-side dust puff / debris
            SurfaceImpactQueue.Enqueue(new SurfaceImpactData
            {
                Position = exitPosition,
                Normal = exitNormal,
                Velocity = velDir * math.length(req.Velocity) * 0.4f,
                SurfaceId = SurfaceIdResolver.FromMaterial(material),
                ImpactClass = ImpactClass.Bullet_Light, // Exit effect is smaller
                SurfaceMaterialId = material.Id,
                Intensity = 0.5f,
                LODTier = EffectLODTier.Full
            });
        }
    }
}
