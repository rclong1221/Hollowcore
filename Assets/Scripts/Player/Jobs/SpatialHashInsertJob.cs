using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Player.Components;

namespace DIG.Player.Jobs
{
    /// <summary>
    /// Epic 7.7.5: Parallel job for inserting players into spatial hash grid.
    /// 
    /// Each chunk of players is processed in parallel, calculating cell indices
    /// and writing to a thread-safe parallel writer for the cell→entity map.
    /// 
    /// Performance: O(N/threads) per thread for embarrassingly parallel insertion.
    /// 
    /// Epic 7.7.7: FloatMode.Fast enables fused multiply-add and approximate sqrt for SIMD.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SpatialHashInsertJob : IJobChunk
    {
        // Read-only components
        // Epic 7.7.5: [NoAlias] enables Burst auto-vectorization
        [ReadOnly, NoAlias] public ComponentTypeHandle<LocalTransform> TransformHandle;
        [ReadOnly, NoAlias] public EntityTypeHandle EntityHandle;
        
        // Grid configuration (passed from singleton)
        public float CellSize;
        public int GridWidth;
        public int GridHeight;
        public float2 WorldOffset;
        
        // Output: parallel writer for thread-safe multi-hashmap insertion
        [WriteOnly, NoAlias]
        public NativeParallelMultiHashMap<int, Entity>.ParallelWriter CellToEntitiesWriter;
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var transforms = chunk.GetNativeArray(ref TransformHandle);
            var entities = chunk.GetNativeArray(EntityHandle);
            
            // Process each entity in the chunk
            for (int i = 0; i < chunk.Count; i++)
            {
                float3 position = transforms[i].Position;
                
                // Calculate cell index
                int cellIndex = GetCellIndex(position);
                
                if (cellIndex >= 0)
                {
                    CellToEntitiesWriter.Add(cellIndex, entities[i]);
                }
            }
        }
        
        /// <summary>
        /// Calculate cell index from world position (inline for performance).
        /// </summary>
        private int GetCellIndex(float3 worldPosition)
        {
            float offsetX = worldPosition.x + WorldOffset.x;
            float offsetZ = worldPosition.z + WorldOffset.y;
            
            int cellX = (int)(offsetX / CellSize);
            int cellZ = (int)(offsetZ / CellSize);
            
            if (cellX < 0 || cellX >= GridWidth || cellZ < 0 || cellZ >= GridHeight)
                return -1;
            
            return cellX + cellZ * GridWidth;
        }
    }
}
