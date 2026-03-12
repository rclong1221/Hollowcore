using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using DIG.Combat.Components;
using Audio.Systems;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 4: Body Fall Impact System.
    /// Detects newly dead NPCs entering ragdoll phase and enqueues body fall VFX
    /// (dust puff + thud audio) at their position.
    /// Uses a BodyFallTriggered tag to ensure one-shot behavior.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class BodyFallImpactSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (corpse, transform, entity) in
                     SystemAPI.Query<RefRO<CorpseState>, RefRO<LocalToWorld>>()
                     .WithNone<BodyFallTriggered>()
                     .WithEntityAccess())
            {
                var state = corpse.ValueRO;
                if (state.Phase != CorpsePhase.Ragdoll) continue;

                float3 position = transform.ValueRO.Position;

                // Enqueue body fall impact effect
                SurfaceImpactQueue.Enqueue(new SurfaceImpactData
                {
                    Position = position,
                    Normal = new float3(0, 1, 0), // Ground normal
                    Velocity = new float3(0, -3, 0), // Downward fall
                    SurfaceId = SurfaceID.Default, // Resolved by presenter
                    ImpactClass = ImpactClass.BodyFall,
                    SurfaceMaterialId = SurfaceDetectionService.GetDefaultId(),
                    Intensity = 0.8f,
                    LODTier = EffectLODTier.Full
                });

                ecb.AddComponent<BodyFallTriggered>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Tag to prevent duplicate body fall VFX on the same corpse.
    /// </summary>
    public struct BodyFallTriggered : IComponentData { }
}
