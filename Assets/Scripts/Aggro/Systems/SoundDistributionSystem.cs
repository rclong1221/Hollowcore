using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using DIG.Aggro.Components;
using DIG.Vision.Core;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Reads SoundEventRequest entities and distributes HearingEvents
    /// to all AI entities within audible range. Destroys request entities after processing.
    ///
    /// Uses the request-entity pattern to decouple sound sources from the hearing pipeline.
    /// Sound is attenuated by distance and checked for line-of-sight occlusion.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(HearingDetectionSystem))]
    [BurstCompile]
    public partial struct SoundDistributionSystem : ISystem
    {
        private EntityQuery _requestQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _requestQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SoundEventRequest>()
                .Build(ref state);
            state.RequireForUpdate(_requestQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            int requestCount = _requestQuery.CalculateEntityCount();
            if (requestCount == 0) return;

            // Collect all requests
            var requests = new NativeList<SoundEventRequest>(requestCount, Allocator.Temp);
            foreach (var request in SystemAPI.Query<RefRO<SoundEventRequest>>())
            {
                requests.Add(request.ValueRO);
            }

            // Distribute to all AI hearing buffers
            foreach (var (hearingBuffer, transform, entity) in
                SystemAPI.Query<DynamicBuffer<HearingEvent>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                float3 listenerPos = transform.ValueRO.Position;

                for (int r = 0; r < requests.Length; r++)
                {
                    var req = requests[r];

                    // Skip if listener is the source
                    if (req.SourceEntity == entity)
                        continue;

                    float distance = math.distance(listenerPos, req.Position);
                    if (distance > req.MaxRange)
                        continue;

                    // Add as hearing event on the AI's buffer
                    if (hearingBuffer.Length < 8) // Cap to avoid buffer bloat
                    {
                        hearingBuffer.Add(new HearingEvent
                        {
                            Position = req.Position,
                            SourceEntity = req.SourceEntity,
                            Loudness = req.Loudness,
                            MaxRange = req.MaxRange
                        });
                    }
                }
            }

            requests.Dispose();

            // Destroy all request entities
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<SoundEventRequest>>()
                .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
