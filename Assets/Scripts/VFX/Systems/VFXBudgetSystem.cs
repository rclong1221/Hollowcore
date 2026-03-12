using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;

namespace DIG.VFX.Systems
{
    /// <summary>
    /// EPIC 16.7: Per-category budget throttling for VFX requests.
    /// Counts requests per VFXCategory, sorts by priority, culls excess via VFXCulled tag.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct VFXBudgetSystem : ISystem
    {
        private EntityQuery _requestQuery;
        static readonly ProfilerMarker k_Marker = new("VFXBudgetSystem.Cull");

        public void OnCreate(ref SystemState state)
        {
            _requestQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<VFXRequest>(),
                ComponentType.ReadWrite<VFXCulled>()
            );
            state.RequireForUpdate<VFXBudgetConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            k_Marker.Begin();

            var config = SystemAPI.GetSingleton<VFXBudgetConfig>();

            // Phase 3: Dynamic budget multiplier
            float multiplier = 1f;
            if (SystemAPI.HasSingleton<VFXDynamicBudget>())
            {
                var dynBudget = SystemAPI.GetSingleton<VFXDynamicBudget>();
                if (dynBudget.Enabled)
                {
                    float frameTimeMs = SystemAPI.Time.DeltaTime * 1000f;
                    float targetRatio = dynBudget.TargetFrameTimeMs / math.max(frameTimeMs, 0.001f);
                    float targetMult = math.clamp(targetRatio, dynBudget.MinBudgetMultiplier, dynBudget.MaxBudgetMultiplier);
                    float alpha = 2f / (dynBudget.SmoothingFrames + 1f);
                    float newMult = math.lerp(dynBudget.CurrentMultiplier, targetMult, alpha);
                    dynBudget.CurrentMultiplier = newMult;
                    SystemAPI.SetSingleton(dynBudget);
                    multiplier = newMult;
                }
            }

            int entityCount = _requestQuery.CalculateEntityCount();
            if (entityCount == 0)
            {
                k_Marker.End();
                return;
            }

            var entities = _requestQuery.ToEntityArray(Allocator.Temp);
            var requests = _requestQuery.ToComponentDataArray<VFXRequest>(Allocator.Temp);

            // Count per category and build per-category entity lists
            const int categoryCount = 7;
            var categoryCounts = new NativeArray<int>(categoryCount, Allocator.Temp);
            var categoryBudgets = new NativeArray<int>(categoryCount, Allocator.Temp);

            for (int c = 0; c < categoryCount; c++)
                categoryBudgets[c] = (int)(config.GetBudget((VFXCategory)c) * multiplier);

            // Group entities by category (parallel arrays of indices)
            var categoryIndices = new NativeList<int>(entityCount, Allocator.Temp);
            var categoryOffsets = new NativeArray<int>(categoryCount + 1, Allocator.Temp);

            // First pass: count
            for (int i = 0; i < entityCount; i++)
            {
                int cat = (int)requests[i].Category;
                if (cat >= 0 && cat < categoryCount)
                    categoryCounts[cat]++;
            }

            // Compute offsets (exclusive scan)
            categoryOffsets[0] = 0;
            for (int c = 0; c < categoryCount; c++)
                categoryOffsets[c + 1] = categoryOffsets[c] + categoryCounts[c];

            categoryIndices.Resize(entityCount, NativeArrayOptions.UninitializedMemory);
            var writePos = new NativeArray<int>(categoryCount, Allocator.Temp);
            for (int c = 0; c < categoryCount; c++)
                writePos[c] = categoryOffsets[c];

            // Second pass: distribute
            for (int i = 0; i < entityCount; i++)
            {
                int cat = (int)requests[i].Category;
                if (cat >= 0 && cat < categoryCount)
                {
                    categoryIndices[writePos[cat]] = i;
                    writePos[cat]++;
                }
            }

            // Per-category: sort by priority descending, cull excess
            int totalKept = 0;
            int globalMax = (int)(config.GlobalMaxPerFrame * multiplier);

            for (int c = 0; c < categoryCount; c++)
            {
                int start = categoryOffsets[c];
                int count = categoryCounts[c];
                int budget = categoryBudgets[c];

                if (count <= budget)
                {
                    totalKept += count;
                    continue;
                }

                // Sort by priority descending: build sortable key array
                var sortKeys = new NativeArray<SortablePriority>(count, Allocator.Temp);
                for (int i = 0; i < count; i++)
                {
                    int idx = categoryIndices[start + i];
                    sortKeys[i] = new SortablePriority { Priority = -requests[idx].Priority, OriginalIndex = idx };
                }
                sortKeys.Sort();

                // Cull lowest priority (after budget)
                for (int i = budget; i < count; i++)
                {
                    int idx = sortKeys[i].OriginalIndex;
                    state.EntityManager.SetComponentEnabled<VFXCulled>(entities[idx], true);
                }

                sortKeys.Dispose();
                totalKept += budget;
            }

            // Global cap enforcement
            if (totalKept > globalMax)
            {
                // Build list of non-culled, sort globally by priority, cull excess
                var nonCulled = new NativeList<SortablePriority>(totalKept, Allocator.Temp);
                for (int i = 0; i < entityCount; i++)
                {
                    if (!state.EntityManager.IsComponentEnabled<VFXCulled>(entities[i]))
                    {
                        nonCulled.Add(new SortablePriority
                        {
                            Priority = -requests[i].Priority,
                            OriginalIndex = i
                        });
                    }
                }
                nonCulled.AsArray().Sort();

                for (int i = globalMax; i < nonCulled.Length; i++)
                {
                    int idx = nonCulled[i].OriginalIndex;
                    state.EntityManager.SetComponentEnabled<VFXCulled>(entities[idx], true);
                }
                nonCulled.Dispose();
            }

            entities.Dispose();
            requests.Dispose();
            categoryCounts.Dispose();
            categoryBudgets.Dispose();
            categoryIndices.Dispose();
            categoryOffsets.Dispose();
            writePos.Dispose();

            k_Marker.End();
        }

        private struct SortablePriority : System.IComparable<SortablePriority>
        {
            public int Priority; // negated for descending sort
            public int OriginalIndex;

            public int CompareTo(SortablePriority other) => Priority.CompareTo(other.Priority);
        }
    }
}
