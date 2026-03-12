using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Player.Components;
using DIG.Player.Jobs;
using DIG.Performance;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Epic 7.7.3: Populates the spatial hash grid with player positions each frame.
    /// Epic 7.7.5: Refactored to use IJobChunk for parallel insertion.
    /// 
    /// This system runs BEFORE PlayerProximityCollisionSystem to prepare the grid
    /// for O(N) collision detection. It clears the previous frame's data and inserts
    /// all players into their corresponding grid cells.
    /// 
    /// Performance: O(N/threads) with parallel job scheduling.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerProximityCollisionSystem))]
    [BurstCompile]
    public partial struct PlayerSpatialHashSystem : ISystem
    {
        private EntityQuery _playerQuery;
        private bool _initialized;
        private ComponentTypeHandle<LocalTransform> _transformHandle;
        private EntityTypeHandle _entityHandle;
        
        // Note: OnCreate cannot be Burst-compiled because GetEntityQuery creates managed arrays
        public void OnCreate(ref SystemState state)
        {
            // Require network time for prediction
            state.RequireForUpdate<NetworkTime>();
            
            // Query for all players with position
            _playerQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            
            // Cache type handles
            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(isReadOnly: true);
            _entityHandle = state.GetEntityTypeHandle();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            // Epic 7.7.3: Profile spatial hash population
            CollisionProfilerMarkers.SpatialHash_PopulateGrid.Begin();
            
            // Initialize grid singleton and static data on first update
            if (!_initialized)
            {
                InitializeGrid(ref state);
                _initialized = true;
            }
            
            // Get grid singleton
            if (!SystemAPI.TryGetSingleton<SpatialHashGrid>(out var grid))
            {
                CollisionProfilerMarkers.SpatialHash_PopulateGrid.End();
                return;
            }
            
            // Ensure static grid data is initialized
            if (!SpatialHashGridData.IsInitialized)
            {
                SpatialHashGridData.Initialize(MemoryOptimizationUtility.MaxPlayerCount * 2);
            }
            
            // Clear previous frame's data
            SpatialHashGridData.Clear();
            
            // Count players for capacity check
            int playerCount = _playerQuery.CalculateEntityCount();
            
            // Ensure capacity
            SpatialHashGridData.EnsureCapacity(playerCount * 2);
            
            // Epic 7.7.5: Update type handles for this frame
            _transformHandle.Update(ref state);
            _entityHandle.Update(ref state);
            
            // Epic 7.7.5: Schedule parallel insertion job
            var insertJob = new SpatialHashInsertJob
            {
                TransformHandle = _transformHandle,
                EntityHandle = _entityHandle,
                CellSize = grid.CellSize,
                GridWidth = grid.GridWidth,
                GridHeight = grid.GridHeight,
                WorldOffset = grid.WorldOffset,
                CellToEntitiesWriter = SpatialHashGridData.CellToEntities.AsParallelWriter()
            };
            
            // Schedule with ScheduleParallel for multi-core parallelism
            var jobHandle = insertJob.ScheduleParallel(_playerQuery, state.Dependency);
            
            // Complete immediately for now - in future, pass handle to collision system
            // This is temporary until full job dependency chain is implemented
            jobHandle.Complete();
            
            // Epic 7.7.7: Build sorted occupied cell list for scan-line iteration
            // This enables cache-friendly cell traversal in collision detection
            SpatialHashGridData.BuildOccupiedCellList();
            
            // Update grid state
            grid.IsPopulated = true;
            grid.PlayerCount = playerCount;
            SystemAPI.SetSingleton(grid);
            
            CollisionProfilerMarkers.SpatialHash_PopulateGrid.End();
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick.TickIndexForValidTick;
            if ((currentTick % 300) == 0)
            {
                // UnityEngine.Debug.Log($"[SpatialHash] Populated grid with {playerCount} players (parallel job)");
            }
            #endif
        }
        
        private void InitializeGrid(ref SystemState state)
        {
            // Check if grid singleton already exists
            if (SystemAPI.TryGetSingletonEntity<SpatialHashGrid>(out _))
                return;
            
            // Create grid singleton entity
            var gridEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(gridEntity, "SpatialHashGrid");
            
            // Add grid configuration
            state.EntityManager.AddComponentData(gridEntity, SpatialHashGrid.CreateDefault());
            
            // Initialize static grid data
            SpatialHashGridData.Initialize(MemoryOptimizationUtility.MaxPlayerCount * 2);
            

        }
        
        public void OnDestroy(ref SystemState state)
        {
            // Dispose the static grid data
            SpatialHashGridData.Dispose();
        }
    }
}
