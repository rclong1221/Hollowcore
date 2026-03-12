using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Surface;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Visual feedback for knockback — dust impacts, slide trails, stopping puffs.
    /// Enqueues to SurfaceImpactQueue on knockback start/end (material-aware VFX).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class KnockbackVFXBridgeSystem : SystemBase
    {
        private Dictionary<Entity, bool> _wasActive = new();

        protected override void OnCreate()
        {
            RequireForUpdate<KnockbackState>();
        }

        protected override void OnUpdate()
        {
            // Clean up destroyed entities
            var toRemove = new List<Entity>();
            foreach (var entity in _wasActive.Keys)
            {
                if (!EntityManager.Exists(entity))
                    toRemove.Add(entity);
            }
            foreach (var entity in toRemove)
                _wasActive.Remove(entity);

            foreach (var (knockbackState, transform, groundState, entity) in
                SystemAPI.Query<RefRO<KnockbackState>, RefRO<LocalTransform>, RefRO<GroundSurfaceState>>()
                    .WithEntityAccess())
            {
                var kb = knockbackState.ValueRO;
                bool wasActive = _wasActive.TryGetValue(entity, out bool prev) && prev;

                float3 pos = transform.ValueRO.Position;
                SurfaceID surfaceId = groundState.ValueRO.SurfaceId;

                if (kb.IsActive && !wasActive)
                {
                    // Knockback just started — spawn dust impact at feet
                    SurfaceImpactQueue.Enqueue(new SurfaceImpactData
                    {
                        Position = pos,
                        Normal = new float3(0, 1, 0),
                        Velocity = kb.Velocity * -0.5f,
                        SurfaceId = surfaceId,
                        ImpactClass = ImpactClass.BodyFall,
                        SurfaceMaterialId = groundState.ValueRO.SurfaceMaterialId,
                        Intensity = math.saturate(kb.InitialSpeed / 15f),
                        LODTier = EffectLODTier.Full
                    });
                }
                else if (!kb.IsActive && wasActive)
                {
                    // Knockback just ended — stopping dust puff
                    SurfaceImpactQueue.Enqueue(new SurfaceImpactData
                    {
                        Position = pos,
                        Normal = new float3(0, 1, 0),
                        Velocity = float3.zero,
                        SurfaceId = surfaceId,
                        ImpactClass = ImpactClass.Footstep,
                        SurfaceMaterialId = groundState.ValueRO.SurfaceMaterialId,
                        Intensity = 0.3f,
                        LODTier = EffectLODTier.Full
                    });
                }

                _wasActive[entity] = kb.IsActive;
            }
        }
    }
}
