using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;

namespace DIG.Swarm.Systems
{
    /// <summary>
    /// EPIC 16.2 Phase 5: Area damage against swarm particles.
    /// Pipeline:
    ///   1. Collect damage zones (main thread — few zones per frame)
    ///   2. ParticleKillCheckJob (Burst, parallel) — checks each particle against all zones
    ///   3. Main thread: destroy killed entities, create clustered VFX requests
    ///
    /// Only runs when SwarmDamageZone events exist (RequireForUpdate skip otherwise).
    /// Combat-tier entities take damage through the normal pipeline (unaffected by this system).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwarmCombatBehaviorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SwarmAreaDamageSystem : ISystem
    {
        private const float VFX_CLUSTER_RADIUS_SQ = 4f; // 2m squared

        private EntityQuery _damageZoneQuery;
        private EntityQuery _particleQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _damageZoneQuery = SystemAPI.QueryBuilder()
                .WithAll<SwarmDamageZone>()
                .Build();
            _particleQuery = SystemAPI.QueryBuilder()
                .WithAll<SwarmParticle>()
                .Build();

            // System only runs when damage zone events exist
            state.RequireForUpdate(_damageZoneQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (SwarmProfilerMarkers.AreaDamage.Auto())
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);

                // Collect damage zones (bulk copy — few per frame)
                var zones = _damageZoneQuery.ToComponentDataArray<SwarmDamageZone>(Allocator.TempJob);
                var zoneEntities = _damageZoneQuery.ToEntityArray(Allocator.TempJob);

                int particleCount = _particleQuery.CalculateEntityCount();
                if (particleCount > 0)
                {
                    var particles = _particleQuery.ToComponentDataArray<SwarmParticle>(Allocator.TempJob);
                    var particleEntities = _particleQuery.ToEntityArray(Allocator.TempJob);

                    // Burst parallel: check each particle against all zones
                    var killFlags = new NativeArray<byte>(particleCount, Allocator.TempJob);

                    new ParticleKillCheckJob
                    {
                        Particles = particles,
                        Zones = zones,
                        KillFlags = killFlags,
                    }.Schedule(particleCount, 64).Complete();

                    // Main thread: destroy killed particles, collect death positions for VFX
                    var deathPositions = new NativeList<float3>(256, Allocator.Temp);

                    for (int i = 0; i < particleCount; i++)
                    {
                        if (killFlags[i] != 0)
                        {
                            deathPositions.Add(particles[i].Position);
                            ecb.DestroyEntity(particleEntities[i]);
                        }
                    }

                    // Cluster death positions into VFX requests (max 1 VFX per cluster)
                    if (deathPositions.Length > 0)
                    {
                        var clusterCenters = new NativeList<float3>(64, Allocator.Temp);
                        var clusterCounts = new NativeList<int>(64, Allocator.Temp);

                        for (int i = 0; i < deathPositions.Length; i++)
                        {
                            bool foundCluster = false;
                            for (int c = 0; c < clusterCenters.Length; c++)
                            {
                                if (math.distancesq(deathPositions[i], clusterCenters[c]) < VFX_CLUSTER_RADIUS_SQ)
                                {
                                    clusterCounts[c] = clusterCounts[c] + 1;
                                    foundCluster = true;
                                    break;
                                }
                            }

                            if (!foundCluster)
                            {
                                clusterCenters.Add(deathPositions[i]);
                                clusterCounts.Add(1);
                            }
                        }

                        for (int c = 0; c < clusterCenters.Length; c++)
                        {
                            var vfxEntity = ecb.CreateEntity();
                            ecb.AddComponent(vfxEntity, new SwarmDeathVFXRequest
                            {
                                Position = clusterCenters[c],
                                DeathType = 0,
                                Count = (byte)math.min(clusterCounts[c], 255)
                            });
                        }

                        clusterCenters.Dispose();
                        clusterCounts.Dispose();
                    }

                    deathPositions.Dispose();
                    particles.Dispose();
                    particleEntities.Dispose();
                    killFlags.Dispose();
                }

                // Clean up damage zone event entities
                for (int z = 0; z < zoneEntities.Length; z++)
                {
                    ecb.DestroyEntity(zoneEntities[z]);
                }

                zones.Dispose();
                zoneEntities.Dispose();

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }

        /// <summary>
        /// Burst-compiled parallel kill check.
        /// Each particle independently checks against all damage zones.
        /// </summary>
        [BurstCompile]
        struct ParticleKillCheckJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SwarmParticle> Particles;
            [ReadOnly] public NativeArray<SwarmDamageZone> Zones;
            [WriteOnly] public NativeArray<byte> KillFlags;

            public void Execute(int i)
            {
                float3 pos = Particles[i].Position;

                for (int z = 0; z < Zones.Length; z++)
                {
                    float radiusSq = Zones[z].Radius * Zones[z].Radius;
                    if (math.distancesq(pos, Zones[z].Center) <= radiusSq)
                    {
                        KillFlags[i] = 1;
                        return;
                    }
                }

                KillFlags[i] = 0;
            }
        }
    }
}
