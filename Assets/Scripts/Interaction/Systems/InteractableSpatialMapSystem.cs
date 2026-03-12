using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Interaction.Jobs;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 1 / EPIC 15.23: Populates the interactable spatial hash grid.
    ///
    /// Runs BEFORE InteractableDetectionSystem to prepare the grid for O(1) queries.
    ///
    /// EPIC 15.23 optimization: Tracks per-entity cell indices via InteractableSpatialIdx.
    /// On frames where no entity has changed cell and no new entities exist, the hash map
    /// rebuild is skipped entirely. A safety full rebuild runs every MaxFramesBetweenRebuilds
    /// frames to clean up stale entries from destroyed entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InteractableDetectionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct InteractableSpatialMapSystem : ISystem
    {
        private EntityQuery _interactableQuery;
        private EntityQuery _untrackedQuery;
        private bool _initialized;
        private ComponentTypeHandle<LocalTransform> _transformHandle;
        private EntityTypeHandle _entityHandle;
        private int _framesSinceRebuild;

        /// <summary>
        /// Safety full rebuild interval. Cleans up stale entries from destroyed entities.
        /// </summary>
        private const int MaxFramesBetweenRebuilds = 300;

        public void OnCreate(ref SystemState state)
        {
            _interactableQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Interactable>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            // Entities with Interactable + LocalTransform but WITHOUT InteractableSpatialIdx
            _untrackedQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Interactable>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<InteractableSpatialIdx>()
            );

            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(isReadOnly: true);
            _entityHandle = state.GetEntityTypeHandle();
        }

        // Not Burst-compiled: accesses InteractableSpatialGridData static fields.
        // The actual insertion work is Burst-compiled via InteractableSpatialInsertJob.
        public void OnUpdate(ref SystemState state)
        {
            // Initialize grid singleton and static data on first update
            if (!_initialized)
            {
                InitializeGrid(ref state);
                _initialized = true;
            }

            // Get grid singleton
            if (!SystemAPI.TryGetSingleton<InteractableSpatialGrid>(out var grid))
                return;

            // Ensure static grid data is initialized
            if (!InteractableSpatialGridData.IsInitialized)
            {
                InteractableSpatialGridData.Initialize(256);
            }

            // Check for new untracked entities (no InteractableSpatialIdx yet)
            int untrackedCount = _untrackedQuery.CalculateEntityCount();

            // Determine if we can skip the rebuild
            bool needsRebuild = untrackedCount > 0 || _framesSinceRebuild >= MaxFramesBetweenRebuilds;

            // If no new entities and not at safety interval, check if any tracked entity moved
            if (!needsRebuild)
            {
                foreach (var (transform, idx) in
                         SystemAPI.Query<RefRO<LocalTransform>, RefRO<InteractableSpatialIdx>>()
                         .WithAll<Interactable>())
                {
                    int currentCell = grid.GetCellIndex(transform.ValueRO.Position);
                    if (currentCell != idx.ValueRO.LastCellIndex)
                    {
                        needsRebuild = true;
                        break;
                    }
                }
            }

            if (!needsRebuild)
            {
                // Map is still valid from last rebuild — skip
                _framesSinceRebuild++;
                return;
            }

            // --- Full rebuild ---

            // Clear previous frame's data
            InteractableSpatialGridData.Clear();

            // Count interactables for capacity check
            int entityCount = _interactableQuery.CalculateEntityCount();

            // Ensure capacity (2x for hash map load factor)
            InteractableSpatialGridData.EnsureCapacity(entityCount * 2);

            // Update type handles for this frame
            _transformHandle.Update(ref state);
            _entityHandle.Update(ref state);

            // Schedule parallel insertion job
            var insertJob = new InteractableSpatialInsertJob
            {
                TransformHandle = _transformHandle,
                EntityHandle = _entityHandle,
                CellSize = grid.CellSize,
                GridWidth = grid.GridWidth,
                GridHeight = grid.GridHeight,
                WorldOffset = grid.WorldOffset,
                CellToEntitiesWriter = InteractableSpatialGridData.CellToEntities.AsParallelWriter()
            };

            var jobHandle = insertJob.ScheduleParallel(_interactableQuery, state.Dependency);
            jobHandle.Complete();

            // Update InteractableSpatialIdx for all tracked entities
            foreach (var (transform, idx) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<InteractableSpatialIdx>>()
                     .WithAll<Interactable>())
            {
                idx.ValueRW.LastCellIndex = grid.GetCellIndex(transform.ValueRO.Position);
            }

            // Add InteractableSpatialIdx to new untracked entities via ECB
            if (untrackedCount > 0)
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (transform, entity) in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<Interactable>()
                         .WithNone<InteractableSpatialIdx>()
                         .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new InteractableSpatialIdx
                    {
                        LastCellIndex = grid.GetCellIndex(transform.ValueRO.Position)
                    });
                }
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }

            // Update grid state
            grid.IsPopulated = true;
            grid.EntityCount = entityCount;
            SystemAPI.SetSingleton(grid);

            _framesSinceRebuild = 0;
        }

        private void InitializeGrid(ref SystemState state)
        {
            // Check if grid singleton already exists
            if (SystemAPI.TryGetSingletonEntity<InteractableSpatialGrid>(out _))
                return;

            // Create grid singleton entity
            var gridEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(gridEntity, "InteractableSpatialGrid");
            state.EntityManager.AddComponentData(gridEntity, InteractableSpatialGrid.CreateDefault());

            // Initialize static grid data
            InteractableSpatialGridData.Initialize(256);
        }

        public void OnDestroy(ref SystemState state)
        {
            InteractableSpatialGridData.Dispose();
        }
    }
}
