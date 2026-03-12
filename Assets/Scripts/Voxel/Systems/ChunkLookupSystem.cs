using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Components;
using DIG.Voxel.Debug;

namespace DIG.Voxel.Systems
{
    public struct ChunkLookup : IComponentData
    {
        public NativeHashMap<int3, Entity> ChunkMap;
        public bool IsInitialized;
        
        public bool TryGetChunk(int3 position, out Entity entity)
        {
            if (!IsInitialized || !ChunkMap.IsCreated)
            {
                entity = Entity.Null;
                return false;
            }
            return ChunkMap.TryGetValue(position, out entity);
        }
    }
    
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ChunkLookupSystem : ISystem
    {
        private NativeHashMap<int3, Entity> _chunkMap;
        
        public void OnCreate(ref SystemState state)
        {
            _chunkMap = new NativeHashMap<int3, Entity>(2048, Allocator.Persistent);
            
            var lookupEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(lookupEntity, new ChunkLookup
            {
                ChunkMap = _chunkMap,
                IsInitialized = true
            });
            
            state.RequireForUpdate<ChunkPosition>();
        }
        
        public void OnDestroy(ref SystemState state)
        {
            if (_chunkMap.IsCreated) _chunkMap.Dispose();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var _ = VoxelProfilerMarkers.ChunkLookup.Auto();

            // Clear map at start of frame
            _chunkMap.Clear();
            
            // Job 1: Rebuild HashMap (Single Threaded Background Job)
            // Note: NativeHashMap.ParallelWriter might not exist in some versions, and collisions are risky.
            // 2000 chunks insertion is fast enough for a single background thread.
            var buildHandle = new BuildChunkMapJob
            {
                ChunkMap = _chunkMap
            }.Schedule(state.Dependency);
            
            // Job 2: Update ChunkNeighbors based on the map
            // Note: We need a specialized job that can read the map.
            // Since Map is being written in Job 1, Job 2 must depend on it.
            // AND map must be ReadOnly in Job 2. (NativeHashMap limitation: cannot read/write same time).
            // But Job 1 finishes, then Job 2 starts. Safe.
            
            var updateHandle = new UpdateNeighborsJob
            {
                ChunkMap = _chunkMap
            }.ScheduleParallel(buildHandle);
            
            // Task 8.14.9: Ensure Dependency is passed out, NOT completed.
            // Systems in SimulationSystemGroup that read ChunkNeighbors will automatically wait for this handle.
            state.Dependency = updateHandle;
            
            // FIX: VoxelInteractionSystem accesses ChunkMap on main thread.
            // We must complete the job to avoid Race Conditions/InvalidOperationException during Raycasts.
            // 2000 chunks insertion is very fast (<0.2ms), so strict sync is acceptable here to prevent crashes.
            updateHandle.Complete();
        }
        
        [BurstCompile]
        public partial struct BuildChunkMapJob : IJobEntity
        {
            public NativeHashMap<int3, Entity> ChunkMap;
            
            public void Execute(Entity entity, RefRO<ChunkPosition> pos)
            {
                ChunkMap.TryAdd(pos.ValueRO.Value, entity);
            }
        }
        
        // This job reads the map we just built to populate neighbor references
        [BurstCompile]
        public partial struct UpdateNeighborsJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<int3, Entity> ChunkMap;
            
            public void Execute(RefRO<ChunkPosition> pos, RefRW<ChunkNeighbors> neighbors)
            {
                int3 center = pos.ValueRO.Value;
                
                // Lookup 6 neighbors
                // If not found, Entity.Null is safe (default value)
                
                ChunkMap.TryGetValue(center + new int3(1, 0, 0), out neighbors.ValueRW.PosX);
                ChunkMap.TryGetValue(center + new int3(-1, 0, 0), out neighbors.ValueRW.NegX);
                ChunkMap.TryGetValue(center + new int3(0, 1, 0), out neighbors.ValueRW.PosY);
                ChunkMap.TryGetValue(center + new int3(0, -1, 0), out neighbors.ValueRW.NegY);
                ChunkMap.TryGetValue(center + new int3(0, 0, 1), out neighbors.ValueRW.PosZ);
                ChunkMap.TryGetValue(center + new int3(0, 0, -1), out neighbors.ValueRW.NegZ);
            }
        }
    }
}
