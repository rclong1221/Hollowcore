using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;

namespace DIG.Swarm.Systems
{
    /// <summary>
    /// EPIC 16.2 Phase 2: Frame-budgeted batch spawning for swarm particles.
    /// Creates minimal particle entities (SwarmParticle + SwarmAnimState only).
    ///
    /// Three spawn modes:
    /// - Area: scatter within radius around spawner (one-time batch)
    /// - Edge: spawn along flow field grid perimeter (one-time batch)
    /// - Continuous: maintain target population by spawning at grid edges over time
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(FlowFieldBuildSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SwarmSpawnerSystem : ISystem
    {
        private uint _nextParticleID;
        private bool _loggedOnce;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SwarmSpawner>();
            state.RequireForUpdate<SwarmConfig>();
            _nextParticleID = 1;
        }

        public void OnUpdate(ref SystemState state)
        {
            using (SwarmProfilerMarkers.Spawner.Auto())
            {
                // One-time diagnostic
                if (!_loggedOnce)
                {
                    _loggedOnce = true;
                    bool hasSpawner = SystemAPI.HasSingleton<SwarmSpawner>();
                    bool hasConfig = SystemAPI.HasSingleton<SwarmConfig>();
                    bool hasGridComp = SystemAPI.HasSingleton<FlowFieldGrid>();
                    UnityEngine.Debug.Log($"[Swarm Spawner] Starting in {state.WorldUnmanaged.Name}: " +
                        $"Spawner={hasSpawner}, Config={hasConfig}, Grid={hasGridComp}");
                }
                var config = SystemAPI.GetSingleton<SwarmConfig>();
                float dt = SystemAPI.Time.DeltaTime;

                // Try to get flow field grid (needed for Edge/Continuous modes)
                bool hasGrid = SystemAPI.HasSingleton<FlowFieldGrid>();
                FlowFieldGrid grid = default;
                if (hasGrid)
                    grid = SystemAPI.GetSingleton<FlowFieldGrid>();

                // Count current live particles (for Continuous mode) — O(1) via chunk metadata
                int liveParticleCount = 0;
                bool needsCount = false;
                foreach (var spawner in SystemAPI.Query<RefRO<SwarmSpawner>>())
                {
                    if (spawner.ValueRO.Mode == SwarmSpawnMode.Continuous)
                    {
                        needsCount = true;
                        break;
                    }
                }
                if (needsCount)
                {
                    var particleQuery = SystemAPI.QueryBuilder().WithAll<SwarmParticle>().Build();
                    liveParticleCount = particleQuery.CalculateEntityCount();
                }

                var ecb = new EntityCommandBuffer(Allocator.Temp);

                foreach (var (spawner, transform, entity) in
                    SystemAPI.Query<RefRW<SwarmSpawner>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
                {
                    ref var sp = ref spawner.ValueRW;

                    if (sp.Mode == SwarmSpawnMode.Continuous)
                    {
                        HandleContinuousSpawn(ref sp, ref ecb, ref config, ref grid, hasGrid,
                            liveParticleCount, dt, entity, ref state);
                    }
                    else
                    {
                        HandleBatchSpawn(ref sp, ref ecb, ref config, ref grid, hasGrid,
                            transform.ValueRO.Position, entity, ref state);
                    }
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();

                // Log first spawn batch
                if (_nextParticleID > 1 && _nextParticleID < 20)
                {
                    var diagQuery = SystemAPI.QueryBuilder().WithAll<SwarmParticle>().Build();
                    int totalParticles = diagQuery.CalculateEntityCount();
                    UnityEngine.Debug.Log($"[Swarm Spawner] Particles alive: {totalParticles} (world: {state.WorldUnmanaged.Name})");
                }
            }
        }

        private void HandleBatchSpawn(
            ref SwarmSpawner sp, ref EntityCommandBuffer ecb, ref SwarmConfig config,
            ref FlowFieldGrid grid, bool hasGrid, float3 origin, Entity entity, ref SystemState state)
        {
            // Handle spawn triggers
            if (!sp.IsSpawning)
            {
                bool shouldStart = sp.SpawnOnStart && sp.SpawnedCount == 0;
                if (state.EntityManager.HasComponent<SwarmSpawnRequest>(entity))
                {
                    shouldStart = true;
                    ecb.RemoveComponent<SwarmSpawnRequest>(entity);
                }

                if (!shouldStart) return;
                sp.IsSpawning = true;
            }

            if (sp.IsComplete) return;

            // Edge mode requires grid
            if (sp.Mode == SwarmSpawnMode.Edge && !hasGrid)
                return;

            int remaining = sp.TotalParticles - sp.SpawnedCount;
            int batchCount = math.min(remaining, sp.BatchSize);
            if (batchCount <= 0)
            {
                sp.IsComplete = true;
                return;
            }

            uint seed = sp.Seed == 0 ? (uint)(entity.Index + 1) : sp.Seed;
            var rng = new Random(seed + (uint)sp.SpawnedCount);

            for (int i = 0; i < batchCount; i++)
            {
                float3 pos;
                if (sp.Mode == SwarmSpawnMode.Edge)
                    pos = GetEdgePosition(ref rng, ref grid, sp.EdgeInset);
                else
                    pos = GetAreaPosition(ref rng, origin, sp.SpawnRadius);

                SpawnParticle(ref ecb, pos, ref rng, ref config);
            }

            sp.SpawnedCount += batchCount;
            if (sp.SpawnedCount >= sp.TotalParticles)
                sp.IsComplete = true;
        }

        private void HandleContinuousSpawn(
            ref SwarmSpawner sp, ref EntityCommandBuffer ecb, ref SwarmConfig config,
            ref FlowFieldGrid grid, bool hasGrid, int liveParticleCount, float dt,
            Entity entity, ref SystemState state)
        {
            // Handle initial trigger
            if (!sp.IsSpawning)
            {
                bool shouldStart = sp.SpawnOnStart;
                if (state.EntityManager.HasComponent<SwarmSpawnRequest>(entity))
                {
                    shouldStart = true;
                    ecb.RemoveComponent<SwarmSpawnRequest>(entity);
                }

                if (!shouldStart) return;
                sp.IsSpawning = true;
            }

            if (!hasGrid) return;

            // Pause spawning when frame rate is critically low (< 10 FPS)
            // to prevent death spiral: slow frame → large dt → mass spawn → slower frame
            if (dt > 0.1f)
                return;

            // How many do we need?
            int deficit = sp.TargetPopulation - liveParticleCount;
            if (deficit <= 0) return;

            // Rate-limit spawning (cap dt to prevent burst spawns after hitches)
            float clampedDt = math.min(dt, 0.05f);
            sp.SpawnAccumulator += sp.SpawnRate * clampedDt;
            int maxThisFrame = (int)sp.SpawnAccumulator;
            if (maxThisFrame <= 0) return;
            sp.SpawnAccumulator -= maxThisFrame;

            // Don't exceed deficit or batch budget
            int batchCount = math.min(math.min(maxThisFrame, deficit), sp.BatchSize);

            uint seed = sp.Seed == 0 ? (uint)(entity.Index + 1) : sp.Seed;
            var rng = new Random(seed + (uint)sp.SpawnedCount + (uint)(dt * 100000f));

            for (int i = 0; i < batchCount; i++)
            {
                float3 pos = GetEdgePosition(ref rng, ref grid, sp.EdgeInset);
                SpawnParticle(ref ecb, pos, ref rng, ref config);
            }

            sp.SpawnedCount += batchCount;
        }

        private void SpawnParticle(ref EntityCommandBuffer ecb, float3 pos,
            ref Random rng, ref SwarmConfig config)
        {
            var particleEntity = ecb.CreateEntity();
            float speedVariance = rng.NextFloat(-config.SpeedVariance, config.SpeedVariance);

            ecb.AddComponent(particleEntity, new SwarmParticle
            {
                Position = pos,
                Velocity = float3.zero,
                Speed = config.BaseSpeed + speedVariance,
                ParticleID = _nextParticleID++
            });

            ecb.AddComponent(particleEntity, new SwarmAnimState
            {
                AnimClipIndex = 1, // Walk — they're already moving in from the edge
                AnimTime = rng.NextFloat(),
                AnimSpeed = 1f
            });
        }

        /// <summary>
        /// Random position within a circle around origin.
        /// </summary>
        private static float3 GetAreaPosition(ref Random rng, float3 origin, float radius)
        {
            float angle = rng.NextFloat(0f, math.PI * 2f);
            float dist = math.sqrt(rng.NextFloat()) * radius;
            return origin + new float3(
                math.cos(angle) * dist,
                0f,
                math.sin(angle) * dist
            );
        }

        /// <summary>
        /// Random position along the perimeter of the flow field grid.
        /// Picks a random edge (N/S/E/W) then a random point along that edge.
        /// </summary>
        private static float3 GetEdgePosition(ref Random rng, ref FlowFieldGrid grid, float inset)
        {
            float minX = grid.WorldOrigin.x + inset;
            float maxX = grid.WorldOrigin.x + grid.GridWidth * grid.CellSize - inset;
            float minZ = grid.WorldOrigin.z + inset;
            float maxZ = grid.WorldOrigin.z + grid.GridHeight * grid.CellSize - inset;

            // Pick random edge: 0=North, 1=South, 2=East, 3=West
            int edge = rng.NextInt(0, 4);
            float x, z;

            switch (edge)
            {
                case 0: // North edge (max Z)
                    x = rng.NextFloat(minX, maxX);
                    z = maxZ;
                    break;
                case 1: // South edge (min Z)
                    x = rng.NextFloat(minX, maxX);
                    z = minZ;
                    break;
                case 2: // East edge (max X)
                    x = maxX;
                    z = rng.NextFloat(minZ, maxZ);
                    break;
                default: // West edge (min X)
                    x = minX;
                    z = rng.NextFloat(minZ, maxZ);
                    break;
            }

            return new float3(x, grid.WorldOrigin.y, z);
        }
    }
}
