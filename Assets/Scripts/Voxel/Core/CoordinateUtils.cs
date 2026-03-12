using Unity.Burst;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace DIG.Voxel.Core
{
    // [BurstCompile] // Not strictly needed on the class if just used by jobs, but acceptable.
    public static class CoordinateUtils
    {
        /// <summary>
        /// Convert 3D local position to flat array index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int VoxelPosToIndex(int3 localPos)
        {
            return localPos.x + 
                   localPos.y * VoxelConstants.CHUNK_SIZE + 
                   localPos.z * VoxelConstants.CHUNK_SIZE_SQ;
        }
        
        /// <summary>
        /// Convert flat array index to 3D local position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 IndexToVoxelPos(int index)
        {
            int z = index / VoxelConstants.CHUNK_SIZE_SQ;
            int remainder = index % VoxelConstants.CHUNK_SIZE_SQ;
            int y = remainder / VoxelConstants.CHUNK_SIZE;
            int x = remainder % VoxelConstants.CHUNK_SIZE;
            return new int3(x, y, z);
        }
        
        /// <summary>
        /// Convert world position to chunk grid position.
        /// Handles negative coordinates correctly via math.floor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 WorldToChunkPos(float3 worldPos)
        {
            return new int3(
                (int)math.floor(worldPos.x / VoxelConstants.CHUNK_SIZE_WORLD),
                (int)math.floor(worldPos.y / VoxelConstants.CHUNK_SIZE_WORLD),
                (int)math.floor(worldPos.z / VoxelConstants.CHUNK_SIZE_WORLD)
            );
        }
        
        /// <summary>
        /// Convert chunk grid position to world origin (min corner of chunk).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ChunkToWorldPos(int3 chunkPos)
        {
            return new float3(chunkPos) * VoxelConstants.CHUNK_SIZE_WORLD;
        }
        
        /// <summary>
        /// Convert world position to local voxel position within its chunk (0-31).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 WorldToLocalVoxelPos(float3 worldPos)
        {
            // Get chunk position
            int3 chunkPos = WorldToChunkPos(worldPos);
            
            // Get chunk world origin
            float3 chunkOrigin = ChunkToWorldPos(chunkPos);
            
            // Calculate local position
            float3 localFloat = (worldPos - chunkOrigin) / VoxelConstants.VOXEL_SIZE;
            
            return new int3(
                (int)math.floor(localFloat.x),
                (int)math.floor(localFloat.y),
                (int)math.floor(localFloat.z)
            );
        }
        
        /// <summary>
        /// Check if local position is within chunk bounds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInBounds(int3 localPos)
        {
            return localPos.x >= 0 && localPos.x < VoxelConstants.CHUNK_SIZE &&
                   localPos.y >= 0 && localPos.y < VoxelConstants.CHUNK_SIZE &&
                   localPos.z >= 0 && localPos.z < VoxelConstants.CHUNK_SIZE;
        }
        
        /// <summary>
        /// Returns the center world position of a chunk.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetChunkCenter(int3 chunkPos)
        {
            return ChunkToWorldPos(chunkPos) + (VoxelConstants.CHUNK_SIZE_WORLD * 0.5f);
        }
    }
}
