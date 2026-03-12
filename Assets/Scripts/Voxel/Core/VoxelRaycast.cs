using Unity.Mathematics;
using Unity.Entities;
using DIG.Voxel.Core;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// DDA (Digital Differential Analyzer) raycast through voxel grid.
    /// Returns exact voxel position hit, not just collision point.
    /// </summary>
    public static class VoxelRaycast
    {
        public struct HitResult
        {
            public bool Hit;
            public int3 ChunkPos;
            public int3 LocalVoxelPos;
            public float3 WorldHitPoint;
            public float3 Normal;
            public byte Material;
            public float Distance;
        }
        
        /// <summary>
        /// Cast ray through voxel world, returning first solid voxel hit.
        /// </summary>
        public static HitResult Cast(
            float3 origin, 
            float3 direction, 
            float maxDistance,
            System.Func<int3, BlobAssetReference<VoxelBlob>?> getChunkData)
        {
            var result = new HitResult { Hit = false };
            
            direction = math.normalize(direction);
            float3 pos = origin;
            
            // DDA setup
            // Convert world space origin to voxel grid space
            float3 voxelSpacePos = pos / VoxelConstants.VOXEL_SIZE;
            
            int3 voxelPos = new int3(
                (int)math.floor(voxelSpacePos.x),
                (int)math.floor(voxelSpacePos.y),
                (int)math.floor(voxelSpacePos.z)
            );
            
            int3 step = new int3(
                direction.x >= 0 ? 1 : -1,
                direction.y >= 0 ? 1 : -1,
                direction.z >= 0 ? 1 : -1
            );
            
            float3 tDelta = math.abs(1f / direction); // Distance to travel 1 unit in voxel space along ray
            
            // Initial T values to next voxel boundary
            float3 tMax = new float3(
                direction.x >= 0 ? (voxelPos.x + 1 - voxelSpacePos.x) / direction.x : (voxelSpacePos.x - voxelPos.x) / -direction.x,
                direction.y >= 0 ? (voxelPos.y + 1 - voxelSpacePos.y) / direction.y : (voxelSpacePos.y - voxelPos.y) / -direction.y,
                direction.z >= 0 ? (voxelPos.z + 1 - voxelSpacePos.z) / direction.z : (voxelSpacePos.z - voxelPos.z) / -direction.z
            );
            
            // Adjust distance for voxel size to get world distance
            tDelta *= VoxelConstants.VOXEL_SIZE;
            tMax *= VoxelConstants.VOXEL_SIZE;
            
            float distance = 0;
            int3 lastNormal = int3.zero;
            
            // DDA loop
            int maxSteps = (int)((maxDistance / VoxelConstants.VOXEL_SIZE) * 2);  // Scale steps
            
            for (int i = 0; i < maxSteps && distance < maxDistance; i++)
            {
                // Convert voxel grid position back to world space for Chunk lookup
                // Note: WorldToChunkPos likely expects World Coordinate, but voxelPos is now an index in the global voxel grid.
                // We must multiply back by VOXEL_SIZE effectively to get world "block" coords.
                // BUT ChunkToWorldPos usually assumes input is chunk coord. 
                // Let's rely on CoordinateUtils logic if possible, or do it manually.
                
                // Manual approach to match VoxelConstants.CHUNK_SIZE logic:
                // Global Voxel To Chunk:
                int3 chunkPos = new int3(
                    (int)math.floor((float)voxelPos.x / VoxelConstants.CHUNK_SIZE),
                    (int)math.floor((float)voxelPos.y / VoxelConstants.CHUNK_SIZE),
                    (int)math.floor((float)voxelPos.z / VoxelConstants.CHUNK_SIZE)
                );

                // Local Voxel Pos
                int3 localPos = new int3(
                    ((voxelPos.x % VoxelConstants.CHUNK_SIZE) + VoxelConstants.CHUNK_SIZE) % VoxelConstants.CHUNK_SIZE,
                    ((voxelPos.y % VoxelConstants.CHUNK_SIZE) + VoxelConstants.CHUNK_SIZE) % VoxelConstants.CHUNK_SIZE,
                    ((voxelPos.z % VoxelConstants.CHUNK_SIZE) + VoxelConstants.CHUNK_SIZE) % VoxelConstants.CHUNK_SIZE
                );
                
                var chunkData = getChunkData(chunkPos);
                if (chunkData.HasValue && chunkData.Value.IsCreated)
                {
                    ref var blob = ref chunkData.Value.Value;
                    int index = CoordinateUtils.VoxelPosToIndex(localPos);
                    
                    if (index >= 0 && index < VoxelConstants.VOXELS_PER_CHUNK)
                    {
                        byte density = blob.Densities[index];
                        
                        if (VoxelDensity.IsSolid(density))
                        {
                            result.Hit = true;
                            result.ChunkPos = chunkPos;
                            result.LocalVoxelPos = localPos;
                            result.WorldHitPoint = origin + direction * distance;
                            result.Normal = (float3)lastNormal;
                            result.Material = blob.Materials[index];
                            result.Distance = distance;
                            return result;
                        }
                    }
                }
                
                // Step to next voxel
                if (tMax.x < tMax.y && tMax.x < tMax.z)
                {
                    distance = tMax.x;
                    tMax.x += tDelta.x;
                    voxelPos.x += step.x;
                    lastNormal = new int3(-step.x, 0, 0);
                }
                else if (tMax.y < tMax.z)
                {
                    distance = tMax.y;
                    tMax.y += tDelta.y;
                    voxelPos.y += step.y;
                    lastNormal = new int3(0, -step.y, 0);
                }
                else
                {
                    distance = tMax.z;
                    tMax.z += tDelta.z;
                    voxelPos.z += step.z;
                    lastNormal = new int3(0, 0, -step.z);
                }
            }
            
            return result;
        }
    }
}
