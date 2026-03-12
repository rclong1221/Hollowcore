using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;

namespace DIG.Swarm.Systems
{
    /// <summary>
    /// EPIC 16.2 Phase 3: Handles combat entity → particle demotion.
    /// Reads SwarmDemotionEvent transients, destroys combat entities,
    /// creates new particle entities at the same position.
    /// Only runs when demotion events exist (RequireForUpdate skip otherwise).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwarmPromotionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SwarmDemotionSystem : ISystem
    {
        private uint _demotionParticleID;
        private EntityQuery _eventQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SwarmConfig>();
            _demotionParticleID = 1000000;

            _eventQuery = SystemAPI.QueryBuilder()
                .WithAll<SwarmDemotionEvent>()
                .Build();
            state.RequireForUpdate(_eventQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (SwarmProfilerMarkers.TierDemotion.Auto())
            {
                var config = SystemAPI.GetSingleton<SwarmConfig>();
                var ecb = new EntityCommandBuffer(Allocator.Temp);

                foreach (var (evt, eventEntity) in
                    SystemAPI.Query<RefRO<SwarmDemotionEvent>>()
                    .WithEntityAccess())
                {
                    var e = evt.ValueRO;

                    if (e.CombatEntity != Entity.Null && state.EntityManager.Exists(e.CombatEntity))
                    {
                        ecb.DestroyEntity(e.CombatEntity);
                    }

                    var particleEntity = ecb.CreateEntity();

                    ecb.AddComponent(particleEntity, new SwarmParticle
                    {
                        Position = e.Position,
                        Velocity = e.Velocity,
                        Speed = config.BaseSpeed,
                        ParticleID = _demotionParticleID++
                    });

                    ecb.AddComponent(particleEntity, new SwarmAnimState
                    {
                        AnimClipIndex = 2,
                        AnimTime = 0f,
                        AnimSpeed = 1f
                    });

                    ecb.DestroyEntity(eventEntity);
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }
    }
}
