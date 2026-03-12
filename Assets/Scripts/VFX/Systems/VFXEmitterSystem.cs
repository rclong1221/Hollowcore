using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DIG.VFX.Authoring;

namespace DIG.VFX.Systems
{
    /// <summary>
    /// EPIC 16.7 Phase 6: Reads VFXEmitter components and creates VFXRequest entities
    /// based on emission mode (OneShot, Repeating, Proximity).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class VFXEmitterSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<VFXBudgetConfig>();
        }

        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Camera position for proximity checks
            float3 cameraPos = float3.zero;
            var cam = Camera.main;
            if (cam != null)
                cameraPos = cam.transform.position;

            foreach (var (emitter, transform, entity) in
                     SystemAPI.Query<RefRW<VFXEmitter>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                bool shouldEmit = false;
                ref var e = ref emitter.ValueRW;

                switch (e.EmissionMode)
                {
                    case VFXEmissionMode.OneShot:
                        if (!e.HasEmittedOneShot)
                        {
                            shouldEmit = true;
                            e.HasEmittedOneShot = true;
                        }
                        break;

                    case VFXEmissionMode.Repeating:
                        if (currentTime - e.LastEmitTime >= e.RepeatInterval)
                            shouldEmit = true;
                        break;

                    case VFXEmissionMode.Proximity:
                        if (e.TriggerRadius > 0f && cam != null)
                        {
                            float dist = math.distance(transform.ValueRO.Position, cameraPos);
                            if (dist <= e.TriggerRadius && currentTime - e.LastEmitTime >= e.RepeatInterval)
                                shouldEmit = true;
                        }
                        break;
                }

                if (!shouldEmit) continue;

                e.LastEmitTime = currentTime;

                var reqEntity = ecb.CreateEntity();
                ecb.AddComponent(reqEntity, new VFXRequest
                {
                    Position = transform.ValueRO.Position,
                    Rotation = transform.ValueRO.Rotation,
                    VFXTypeId = e.VFXTypeId,
                    Category = e.Category,
                    Intensity = e.Intensity,
                    Scale = e.Scale,
                    ColorTint = e.ColorTint,
                    Duration = e.Duration,
                    SourceEntity = entity,
                    Priority = e.Priority
                });
                ecb.AddComponent<VFXCulled>(reqEntity);
                ecb.SetComponentEnabled<VFXCulled>(reqEntity, false);
                ecb.AddComponent<VFXCleanupTag>(reqEntity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
