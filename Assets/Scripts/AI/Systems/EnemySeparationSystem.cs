using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.AI.Components;
using DIG.AI.Profiling;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.23: Lightweight spatial-hash separation for AI enemies.
    /// Prevents stacking without physics collision (O(n) vs O(n²)).
    ///
    /// Pipeline:
    ///   1. Bulk copy positions from ECS chunks
    ///   2. BuildSpatialHashJob (Burst, parallel) — hash positions into cells
    ///   3. SeparationJob (Burst, parallel IJobEntity) — 3x3 neighborhood push-apart
    ///
    /// Runs every Nth frame (configurable) to save budget.
    /// Modeled after SwarmSeparationSystem (EPIC 16.2).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AICombatBehaviorSystem))]
    [UpdateAfter(typeof(AIIdleBehaviorSystem))]
    [UpdateAfter(typeof(AIReturnHomeBehaviorSystem))]
    [UpdateBefore(typeof(DIG.Combat.Systems.CombatResolutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct EnemySeparationSystem : ISystem
    {
        private int _frameCount;
        private EntityQuery _enemyQuery;

        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<AIBrain, LocalTransform>()
                .WithNone<Disabled>()
                .Build();
            state.RequireForUpdate(_enemyQuery);
            _frameCount = 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Use config singleton if present (baked from SubScene), otherwise defaults
            EnemySeparationConfig config;
            if (SystemAPI.HasSingleton<EnemySeparationConfig>())
            {
                config = SystemAPI.GetSingleton<EnemySeparationConfig>();
            }
            else
            {
                config = new EnemySeparationConfig
                {
                    SeparationRadius = 1.5f,
                    SeparationWeight = 8f,
                    MaxSeparationSpeed = 8f,
                    FrameInterval = 1,
                };
            }

            // Frame skip — power-of-2 interval for bitwise check
            int interval = math.max(1, config.FrameInterval);
            _frameCount++;
            if ((_frameCount & (interval - 1)) != 0)
                return;

            int enemyCount = _enemyQuery.CalculateEntityCount();
            if (enemyCount == 0) return;

            using (AIProfilerMarkers.EnemySeparation.Auto())
            {
                float cellSize = config.SeparationRadius * 2f;
                float dt = SystemAPI.Time.DeltaTime;

                // Bulk copy transform data from chunks
                var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

                var positions = new NativeArray<float3>(enemyCount, Allocator.TempJob);
                var spatialMap = new NativeParallelMultiHashMap<int, int>(enemyCount * 2, Allocator.TempJob);

                // Phase 1: Build positions array + spatial hash (Burst, parallel)
                var buildHandle = new BuildSpatialHashJob
                {
                    Transforms = transforms,
                    Positions = positions,
                    SpatialMapWriter = spatialMap.AsParallelWriter(),
                    CellSize = cellSize,
                }.Schedule(enemyCount, 64, state.Dependency);

                // Phase 2: Apply separation forces (Burst, parallel IJobEntity)
                state.Dependency = new EnemySeparationJob
                {
                    Positions = positions,
                    SpatialMap = spatialMap,
                    CellSize = cellSize,
                    SepRadiusSq = config.SeparationRadius * config.SeparationRadius,
                    SeparationRadius = config.SeparationRadius,
                    SeparationWeight = config.SeparationWeight,
                    MaxDisplacement = config.MaxSeparationSpeed * dt,
                    DeltaTime = dt,
                }.ScheduleParallel(buildHandle);

                // Deferred dispose after all jobs complete
                transforms.Dispose(state.Dependency);
                positions.Dispose(state.Dependency);
                spatialMap.Dispose(state.Dependency);
            }
        }

        [BurstCompile]
        struct BuildSpatialHashJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<LocalTransform> Transforms;
            [WriteOnly] public NativeArray<float3> Positions;
            public NativeParallelMultiHashMap<int, int>.ParallelWriter SpatialMapWriter;
            public float CellSize;

            public void Execute(int idx)
            {
                float3 pos = Transforms[idx].Position;
                Positions[idx] = pos;
                int x = (int)math.floor(pos.x / CellSize);
                int z = (int)math.floor(pos.z / CellSize);
                SpatialMapWriter.Add(x + z * 10000, idx);
            }
        }

        [BurstCompile]
        [WithNone(typeof(Disabled))]
        partial struct EnemySeparationJob : IJobEntity
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> SpatialMap;
            public float CellSize;
            public float SepRadiusSq;
            public float SeparationRadius;
            public float SeparationWeight;
            public float MaxDisplacement;
            public float DeltaTime;

            void Execute(ref LocalTransform transform, in AIBrain brain, [EntityIndexInQuery] int myIdx)
            {
                float3 myPos = transform.Position;
                int cx = (int)math.floor(myPos.x / CellSize);
                int cz = (int)math.floor(myPos.z / CellSize);

                float3 separation = float3.zero;
                int neighborCount = 0;

                // Check 3x3 neighborhood
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

                            float3 diff = myPos - Positions[otherIdx];
                            // Horizontal only — zero Y
                            diff.y = 0f;
                            float distSq = math.lengthsq(diff);

                            if (distSq < SepRadiusSq && distSq > 0.0001f)
                            {
                                float dist = math.sqrt(distSq);
                                // Linear falloff: strongest at center, zero at radius edge
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
                    float3 displacement = separation * SeparationWeight * DeltaTime;

                    // Clamp to max displacement to prevent teleporting
                    float displacementLen = math.length(displacement);
                    if (displacementLen > MaxDisplacement)
                    {
                        displacement = (displacement / displacementLen) * MaxDisplacement;
                    }

                    // Horizontal only
                    transform.Position += new float3(displacement.x, 0f, displacement.z);
                }
            }
        }
    }
}
