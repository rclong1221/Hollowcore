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
    /// EPIC 16.2 Phase 2: Lightweight neighbor avoidance for swarm particles.
    /// Fully async pipeline:
    ///   1. ToComponentDataArray (bulk memcpy from chunks)
    ///   2. BuildSpatialHashJob (Burst, parallel) — fills positions + spatial hash
    ///   3. SeparationJob (Burst, parallel IJobEntity) — applies separation forces
    /// Runs every 4th frame to save budget.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwarmParticleMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SwarmSeparationSystem : ISystem
    {
        private int _frameCount;
        private EntityQuery _particleQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SwarmConfig>();
            _particleQuery = SystemAPI.QueryBuilder()
                .WithAll<SwarmParticle>()
                .Build();
            _frameCount = 0;
        }

        public void OnDestroy(ref SystemState state)
        {
            SwarmSpatialData.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            _frameCount++;
            if ((_frameCount & 3) != 0)
                return;

            int particleCount = _particleQuery.CalculateEntityCount();
            if (particleCount == 0) return;

            using (SwarmProfilerMarkers.ParticleSeparation.Auto())
            {
                var config = SystemAPI.GetSingleton<SwarmConfig>();
                float cellSize = config.SeparationRadius * 2f;
                float dt = SystemAPI.Time.DeltaTime;

                // Bulk copy particle data from chunks (faster than per-entity foreach)
                var particles = _particleQuery.ToComponentDataArray<SwarmParticle>(Allocator.TempJob);

                var positions = new NativeArray<float3>(particleCount, Allocator.TempJob);
                var spatialMap = new NativeParallelMultiHashMap<int, int>(particleCount * 2, Allocator.TempJob);

                // Phase 1: Build positions array + spatial hash (Burst, parallel)
                var buildHandle = new BuildSpatialHashJob
                {
                    Particles = particles,
                    Positions = positions,
                    SpatialMapWriter = spatialMap.AsParallelWriter(),
                    CellSize = cellSize,
                }.Schedule(particleCount, 64, state.Dependency);

                // Phase 2: Apply separation forces (Burst, parallel IJobEntity)
                state.Dependency = new SeparationJob
                {
                    Positions = positions,
                    SpatialMap = spatialMap,
                    CellSize = cellSize,
                    SepRadiusSq = config.SeparationRadius * config.SeparationRadius,
                    SeparationRadius = config.SeparationRadius,
                    SeparationWeight = config.SeparationWeight,
                    DeltaTime = dt,
                }.ScheduleParallel(buildHandle);

                // Deferred dispose after all jobs complete
                particles.Dispose(state.Dependency);
                positions.Dispose(state.Dependency);
                spatialMap.Dispose(state.Dependency);
            }
        }

        [BurstCompile]
        struct BuildSpatialHashJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SwarmParticle> Particles;
            [WriteOnly] public NativeArray<float3> Positions;
            public NativeParallelMultiHashMap<int, int>.ParallelWriter SpatialMapWriter;
            public float CellSize;

            public void Execute(int idx)
            {
                float3 pos = Particles[idx].Position;
                Positions[idx] = pos;
                int x = (int)math.floor(pos.x / CellSize);
                int z = (int)math.floor(pos.z / CellSize);
                SpatialMapWriter.Add(x + z * 10000, idx);
            }
        }

        [BurstCompile]
        partial struct SeparationJob : IJobEntity
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> SpatialMap;
            public float CellSize;
            public float SepRadiusSq;
            public float SeparationRadius;
            public float SeparationWeight;
            public float DeltaTime;

            void Execute(ref SwarmParticle particle, [EntityIndexInQuery] int myIdx)
            {
                int cx = (int)math.floor(particle.Position.x / CellSize);
                int cz = (int)math.floor(particle.Position.z / CellSize);

                float3 separation = float3.zero;
                int neighborCount = 0;

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int neighborCell = (cx + dx) + (cz + dz) * 10000;
                        if (!SpatialMap.TryGetFirstValue(neighborCell, out int otherIdx, out var it))
                            continue;

                        do
                        {
                            if (otherIdx == myIdx) continue;

                            float3 diff = particle.Position - Positions[otherIdx];
                            float distSq = math.lengthsq(diff);
                            if (distSq < SepRadiusSq && distSq > 0.0001f)
                            {
                                float dist = math.sqrt(distSq);
                                float strength = 1f - (dist / SeparationRadius);
                                separation += (diff / dist) * strength;
                                neighborCount++;
                            }
                        } while (SpatialMap.TryGetNextValue(out otherIdx, ref it));
                    }
                }

                if (neighborCount > 0)
                {
                    separation /= neighborCount;
                    particle.Position += separation * SeparationWeight * DeltaTime;
                }
            }
        }
    }
}
