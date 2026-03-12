using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Biomes;

namespace DIG.Voxel.Jobs
{
    /// <summary>
    /// Pre-pass job to generate XZ-dependent data (Terrain Height, Biome).
    /// Runs on a 2D grid (32x32) per chunk.
    /// Hoisting prevents re-calculating 2D noise for every vertical voxel.
    /// </summary>
    [BurstCompile]
    public struct GenerateColumnDataJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkWorldOrigin;
        [ReadOnly] public uint Seed;
        [ReadOnly] public float TerrainNoiseScale;
        [ReadOnly] public float TerrainNoiseAmplitude;
        [ReadOnly] public float GroundLevel;
        
        // Biome inputs
        [ReadOnly] public bool UseBiomes;
        [ReadOnly] public NativeArray<BiomeService.BiomeParams> SolidLayerBiomes;

        [ReadOnly] public float BiomeNoiseScale;
        [ReadOnly] public bool IsBiomeHomogeneous; // Optimization 10.4.11
        [ReadOnly] public byte HomogeneousBiomeID; // Optimization 10.4.11
        
        [ReadOnly] public int VoxelStep;
        
        // Outputs (Size: 1024)
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<float> TerrainHeights;
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<byte> BiomeIDs;
        
        public void Execute(int index)
        {
            // Map index to coarse grid
            int step = VoxelStep;
            int width = 32 / step;
            
            // Reconstruct (x, z) for the LOD grid
            int x = (index % width) * step;
            int z = (index / width) * step;
            
            float3 worldPos = new float3(ChunkWorldOrigin.x + x, 0, ChunkWorldOrigin.z + z);
            
            // 1. Terrain Height (Surface)
            float2 noisePos = new float2(worldPos.x * TerrainNoiseScale + Seed, worldPos.z * TerrainNoiseScale + Seed);
            float noiseVal = noise.cnoise(noisePos) * TerrainNoiseAmplitude;
            float height = GroundLevel + noiseVal;
            
            // 2. Biome ID
            byte biomeID = 0;
            if (IsBiomeHomogeneous)
            {
                biomeID = HomogeneousBiomeID;
            }
            else if (UseBiomes && SolidLayerBiomes.Length > 0)
            {
                biomeID = BiomeLookup.GetBiomeAt(worldPos, BiomeNoiseScale, Seed, SolidLayerBiomes);
            }
            
            // Fill block
            for (int dz = 0; dz < step; dz++)
            {
                for (int dx = 0; dx < step; dx++)
                {
                    int finalX = x + dx;
                    int finalZ = z + dz;
                    int finalIndex = finalX + finalZ * 32;
                    
                    TerrainHeights[finalIndex] = height;
                    BiomeIDs[finalIndex] = biomeID;
                }
            }
        }
    }
}
