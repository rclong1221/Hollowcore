using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Core;

namespace DIG.Voxel.Jobs
{
    /// <summary>
    /// Optimization 10.1.11: Ore Noise Caching
    /// Generates a single 3D noise field for the entire chunk to be reused by multiple ore types.
    /// This avoids recalculating complex simplex noise for every ore type per voxel.
    /// </summary>
    [BurstCompile]
    public struct GenerateOreNoiseJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkWorldOrigin;
        [ReadOnly] public uint Seed;
        [ReadOnly] public float NoiseScale;
        [ReadOnly] public int VoxelStep;
        
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<float> OreNoiseCache;

        public void Execute(int index)
        {
            int step = VoxelStep;
            int size = 32 / step;
            
            // Map index to coarse grid position (Same mapping as VoxelDataJob)
            int x = (index % size) * step;
            int z = ((index / size) % size) * step;
            int y = (index / (size * size)) * step;
            
            int voxelIndex = x + y * 32 + z * 1024;
            // No CoordinateUtils.IndexToVoxelPos used here for LOD path
            
            int3 localPos = new int3(x, y, z);
            float3 worldPos = new float3(ChunkWorldOrigin + localPos);
            
            // Standardize on specific noise settings for the cache
            float3 noisePos = worldPos + Seed;
            
            // Base 3D noise (0 to 1 range)
            float rawNoise = noise.snoise(noisePos * NoiseScale);
            float normalizedNoise = (rawNoise + 1f) * 0.5f;
            
            OreNoiseCache[voxelIndex] = normalizedNoise;
        }
    }
}
