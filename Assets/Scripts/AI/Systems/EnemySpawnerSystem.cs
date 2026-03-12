using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.AI.Components;

namespace DIG.AI.Systems
{
    /// <summary>
    /// Server-side enemy spawner with frame-budgeted batch instantiation.
    /// Supports 1 to 1,000,000+ entities by spreading instantiation across frames.
    ///
    /// Uses EntityManager.Instantiate(Entity, NativeArray) for maximum throughput —
    /// a single structural change per batch instead of one per entity.
    ///
    /// Spawning triggers:
    /// - SpawnOnStart: begins automatically when the subscene loads
    /// - EnemySpawnRequest tag: add at runtime to trigger spawning on demand
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct EnemySpawnerSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemySpawner>();
            _transformLookup = state.GetComponentLookup<LocalTransform>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);

            // --- Handle EnemySpawnRequest tags ---
            foreach (var (spawner, entity) in
                     SystemAPI.Query<RefRW<EnemySpawner>>()
                     .WithAll<EnemySpawnRequest>()
                     .WithEntityAccess())
            {
                spawner.ValueRW.IsSpawning = true;
                spawner.ValueRW.SpawnedCount = 0;
                spawner.ValueRW.IsComplete = false;
                state.EntityManager.RemoveComponent<EnemySpawnRequest>(entity);
            }

            // --- Process all spawners in a single pass ---
            foreach (var (spawner, spawnerTransform) in
                     SystemAPI.Query<RefRW<EnemySpawner>, RefRO<LocalTransform>>())
            {
                // SpawnOnStart trigger (once)
                if (spawner.ValueRO.SpawnOnStart && !spawner.ValueRO.IsSpawning && !spawner.ValueRO.IsComplete)
                {
                    spawner.ValueRW.IsSpawning = true;
                    spawner.ValueRW.SpawnOnStart = false;
                }

                if (!spawner.ValueRO.IsSpawning || spawner.ValueRO.IsComplete)
                    continue;

                if (spawner.ValueRO.Prefab == Entity.Null)
                {
                    spawner.ValueRW.IsSpawning = false;
                    spawner.ValueRW.IsComplete = true;
                    continue;
                }

                int remaining = spawner.ValueRO.TotalCount - spawner.ValueRO.SpawnedCount;
                if (remaining <= 0)
                {
                    spawner.ValueRW.IsSpawning = false;
                    spawner.ValueRW.IsComplete = true;
                    continue;
                }

                int batchCount = math.min(remaining, spawner.ValueRO.BatchSize);
                float3 origin = spawnerTransform.ValueRO.Position;
                float radius = spawner.ValueRO.SpawnRadius;
                float gridSpacing = spawner.ValueRO.GridSpacing;
                float yOffset = spawner.ValueRO.YOffset;
                int startIndex = spawner.ValueRO.SpawnedCount;
                uint baseSeed = spawner.ValueRO.Seed != 0 ? spawner.ValueRO.Seed : 42u;

                // Batch instantiate — single structural change
                var spawnedEntities = new NativeArray<Entity>(batchCount, Allocator.Temp);
                state.EntityManager.Instantiate(spawner.ValueRO.Prefab, spawnedEntities);

                // Position all entities — direct ComponentLookup writes (no per-call entity lookup)
                if (gridSpacing > 0)
                {
                    int gridWidth = math.max(1, (int)math.ceil(radius * 2f / gridSpacing));
                    float halfExtent = gridWidth * gridSpacing * 0.5f;

                    for (int i = 0; i < batchCount; i++)
                    {
                        int idx = startIndex + i;
                        int col = idx % gridWidth;
                        int row = idx / gridWidth;
                        float x = -halfExtent + col * gridSpacing + gridSpacing * 0.5f;
                        float z = -halfExtent + row * gridSpacing + gridSpacing * 0.5f;

                        // Clamp to radius
                        float dist = math.sqrt(x * x + z * z);
                        if (dist > radius & radius > 0)
                        {
                            float scale = radius / dist;
                            x *= scale;
                            z *= scale;
                        }

                        _transformLookup[spawnedEntities[i]] = LocalTransform.FromPosition(
                            origin + new float3(x, yOffset, z));
                    }
                }
                else
                {
                    // Random scatter — single RNG, sequential calls (no per-entity CreateFromIndex)
                    var rng = Random.CreateFromIndex(baseSeed + (uint)startIndex);

                    for (int i = 0; i < batchCount; i++)
                    {
                        float3 pos;
                        if (radius > 0)
                        {
                            float angle = rng.NextFloat() * math.PI * 2f;
                            float r = math.sqrt(rng.NextFloat()) * radius;
                            pos = origin + new float3(math.cos(angle) * r, yOffset, math.sin(angle) * r);
                        }
                        else
                        {
                            pos = origin + new float3(0, yOffset, 0);
                        }

                        _transformLookup[spawnedEntities[i]] = LocalTransform.FromPosition(pos);
                    }
                }

                spawnedEntities.Dispose();
                spawner.ValueRW.SpawnedCount += batchCount;

                if (spawner.ValueRO.SpawnedCount >= spawner.ValueRO.TotalCount)
                {
                    spawner.ValueRW.IsSpawning = false;
                    spawner.ValueRW.IsComplete = true;
                }
            }
        }
    }
}
