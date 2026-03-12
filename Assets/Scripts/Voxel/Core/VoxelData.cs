using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Blob structure for voxel data. Immutable once created.
    /// Each voxel has a density (for surface generation) and material (for loot).
    /// </summary>
    public struct VoxelBlob
    {
        public BlobArray<byte> Densities;   // 32,768 bytes
        public BlobArray<byte> Materials;   // 32,768 bytes
    }
    
    /// <summary>
    /// Helper class to create VoxelBlob assets.
    /// </summary>
    public static class VoxelBlobBuilder
    {
        public static BlobAssetReference<VoxelBlob> Create(
            NativeArray<byte> densities, 
            NativeArray<byte> materials)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<VoxelBlob>();
            
            // Copy densities
            var densityArray = builder.Allocate(ref root.Densities, densities.Length);
            for (int i = 0; i < densities.Length; i++)
                densityArray[i] = densities[i];
            
            // Copy materials
            var materialArray = builder.Allocate(ref root.Materials, materials.Length);
            for (int i = 0; i < materials.Length; i++)
                materialArray[i] = materials[i];
            
            return builder.CreateBlobAssetReference<VoxelBlob>(Allocator.Persistent);
        }
        
        public static BlobAssetReference<VoxelBlob> CreateEmpty()
        {
            var densities = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.Temp);
            var materials = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.Temp);
            
            // All air
            for (int i = 0; i < densities.Length; i++)
            {
                densities[i] = VoxelConstants.DENSITY_AIR;
                materials[i] = VoxelConstants.MATERIAL_AIR;
            }
            
            var result = Create(densities, materials);
            densities.Dispose();
            materials.Dispose();
            return result;
        }
    }
}
