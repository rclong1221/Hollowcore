using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;

namespace DIG.Swarm.Systems
{
    /// <summary>
    /// EPIC 16.2 Phase 3: Handles particle → combat entity promotion.
    /// Reads SwarmPromotionEvent transients, instantiates combat prefabs at particle positions,
    /// destroys the source particle entity. The new entity enters the existing AI/combat pipeline.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwarmTierEvaluationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SwarmPromotionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SwarmConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            using (SwarmProfilerMarkers.TierPromotion.Auto())
            {
                var config = SystemAPI.GetSingleton<SwarmConfig>();
                if (config.CombatPrefab == Entity.Null)
                    return;

                var ecb = new EntityCommandBuffer(Allocator.Temp);
                float currentTime = (float)SystemAPI.Time.ElapsedTime;

                foreach (var (evt, eventEntity) in
                    SystemAPI.Query<RefRO<SwarmPromotionEvent>>()
                    .WithEntityAccess())
                {
                    var e = evt.ValueRO;

                    // Destroy source particle/agent entity
                    if (e.SourceEntity != Entity.Null && state.EntityManager.Exists(e.SourceEntity))
                    {
                        ecb.DestroyEntity(e.SourceEntity);
                    }

                    // Instantiate combat prefab
                    var combatEntity = ecb.Instantiate(config.CombatPrefab);

                    // Position at particle location
                    float3 forward = math.normalizesafe(e.Velocity);
                    if (math.lengthsq(forward) < 0.001f)
                        forward = new float3(0f, 0f, 1f);

                    quaternion rotation = quaternion.LookRotationSafe(forward, math.up());
                    ecb.SetComponent(combatEntity, LocalTransform.FromPositionRotation(e.Position, rotation));

                    // Tag as swarm combat entity for tier tracking
                    ecb.AddComponent(combatEntity, new SwarmCombatTag
                    {
                        SourceParticleID = e.ParticleID,
                        PromotionTime = currentTime
                    });

                    // Destroy the event entity
                    ecb.DestroyEntity(eventEntity);
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }
    }
}
