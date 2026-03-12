using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Player.Utilities
{
    // Blob layout for job-friendly voxel anchor provider.
    public struct VoxelAnchorBlob
    {
        public BlobArray<float3> Positions;
        public BlobArray<float3> Normals;
    }

    // Component storing a blob reference to anchor data.
    public struct VoxelAnchorProvider : IComponentData
    {
        public BlobAssetReference<VoxelAnchorBlob> Blob;
    }
}
