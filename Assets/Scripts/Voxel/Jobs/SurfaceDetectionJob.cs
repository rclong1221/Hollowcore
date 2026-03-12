using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Core;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// Burst-compiled job to detect surfaces (floor, ceiling, walls) in a chunk.
    /// Outputs surface points that can host decorators.
    /// 
    /// OPTIMIZATION 10.5.6: Uses BlobAssetReference directly instead of copying data.
    /// </summary>
    [BurstCompile]
    public struct SurfaceDetectionJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkWorldOrigin;
        [ReadOnly] public BlobAssetReference<VoxelBlob> VoxelData;
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public float Depth;
        
        // Threshold for solid vs air (byte density < 128 = solid)
        private const byte SOLID_THRESHOLD = 128;
        
        // Output: detected surface points
        public NativeList<DecoratorService.SurfacePoint>.ParallelWriter Surfaces;
        
        public void Execute(int index)
        {
            ref var blob = ref VoxelData.Value;
            
            // Only process air voxels (density >= 128 = air)
            if (blob.Densities[index] < SOLID_THRESHOLD) return;
            
            int3 localPos = IndexToPos(index);
            float3 worldPos = new float3(ChunkWorldOrigin + localPos);
            
            // Check for floor (solid below, air above)
            if (HasSolidNeighbor(ref blob, localPos, new int3(0, -1, 0)))
            {
                float caveRadius = EstimateCaveRadius(ref blob, localPos);
                Surfaces.AddNoResize(new DecoratorService.SurfacePoint
                {
                    Position = worldPos + new float3(0.5f, 0f, 0.5f),
                    Normal = new float3(0, 1, 0),
                    Type = SurfaceType.Floor,
                    BiomeID = GetMaterialAt(ref blob, localPos + new int3(0, -1, 0)),
                    CaveRadius = caveRadius
                });
            }
            
            // Check for ceiling (solid above, air below)
            if (HasSolidNeighbor(ref blob, localPos, new int3(0, 1, 0)))
            {
                float caveRadius = EstimateCaveRadius(ref blob, localPos);
                Surfaces.AddNoResize(new DecoratorService.SurfacePoint
                {
                    Position = worldPos + new float3(0.5f, 1f, 0.5f),
                    Normal = new float3(0, -1, 0),
                    Type = SurfaceType.Ceiling,
                    BiomeID = GetMaterialAt(ref blob, localPos + new int3(0, 1, 0)),
                    CaveRadius = caveRadius
                });
            }
            
            // Check walls
            CheckWall(ref blob, localPos, worldPos, new int3(1, 0, 0), SurfaceType.WallEast, new float3(-1, 0, 0));
            CheckWall(ref blob, localPos, worldPos, new int3(-1, 0, 0), SurfaceType.WallWest, new float3(1, 0, 0));
            CheckWall(ref blob, localPos, worldPos, new int3(0, 0, 1), SurfaceType.WallNorth, new float3(0, 0, -1));
            CheckWall(ref blob, localPos, worldPos, new int3(0, 0, -1), SurfaceType.WallSouth, new float3(0, 0, 1));
        }
        
        private void CheckWall(ref VoxelBlob blob, int3 localPos, float3 worldPos, int3 offset, SurfaceType wallType, float3 normal)
        {
            if (HasSolidNeighbor(ref blob, localPos, offset))
            {
                float caveRadius = EstimateCaveRadius(ref blob, localPos);
                Surfaces.AddNoResize(new DecoratorService.SurfacePoint
                {
                    Position = worldPos + new float3(0.5f + offset.x * 0.5f, 0.5f, 0.5f + offset.z * 0.5f),
                    Normal = normal,
                    Type = wallType,
                    BiomeID = GetMaterialAt(ref blob, localPos + offset),
                    CaveRadius = caveRadius
                });
            }
        }
        
        private bool HasSolidNeighbor(ref VoxelBlob blob, int3 pos, int3 offset)
        {
            int3 neighbor = pos + offset;
            if (!IsInBounds(neighbor)) return false;
            int neighborIdx = PosToIndex(neighbor);
            return blob.Densities[neighborIdx] < SOLID_THRESHOLD;
        }
        
        private byte GetMaterialAt(ref VoxelBlob blob, int3 pos)
        {
            if (!IsInBounds(pos)) return 0;
            return blob.Materials[PosToIndex(pos)];
        }
        
        private float EstimateCaveRadius(ref VoxelBlob blob, int3 pos)
        {
            // Simple estimation: count air voxels in each direction
            float maxDistance = 0;
            int maxCheck = math.min(16, ChunkSize);
            
            // Check cardinal directions - unrolled for performance
            maxDistance = math.max(maxDistance, RaycastDirection(ref blob, pos, new int3(1, 0, 0), maxCheck));
            maxDistance = math.max(maxDistance, RaycastDirection(ref blob, pos, new int3(-1, 0, 0), maxCheck));
            maxDistance = math.max(maxDistance, RaycastDirection(ref blob, pos, new int3(0, 1, 0), maxCheck));
            maxDistance = math.max(maxDistance, RaycastDirection(ref blob, pos, new int3(0, -1, 0), maxCheck));
            maxDistance = math.max(maxDistance, RaycastDirection(ref blob, pos, new int3(0, 0, 1), maxCheck));
            maxDistance = math.max(maxDistance, RaycastDirection(ref blob, pos, new int3(0, 0, -1), maxCheck));
            
            return maxDistance;
        }
        
        private int RaycastDirection(ref VoxelBlob blob, int3 pos, int3 dir, int maxCheck)
        {
            int distance = 0;
            int3 current = pos + dir;
            
            while (distance < maxCheck && IsInBounds(current))
            {
                if (blob.Densities[PosToIndex(current)] < SOLID_THRESHOLD) break;
                current += dir;
                distance++;
            }
            
            return distance;
        }
        
        private int3 IndexToPos(int i)
        {
            return new int3(
                i % ChunkSize,
                (i / ChunkSize) % ChunkSize,
                i / (ChunkSize * ChunkSize));
        }
        
        private int PosToIndex(int3 p)
        {
            return p.x + p.y * ChunkSize + p.z * ChunkSize * ChunkSize;
        }
        
        private bool IsInBounds(int3 p)
        {
            return p.x >= 0 && p.x < ChunkSize &&
                   p.y >= 0 && p.y < ChunkSize &&
                   p.z >= 0 && p.z < ChunkSize;
        }
    }
}
