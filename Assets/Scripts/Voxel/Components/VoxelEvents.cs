using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// Event raised when voxels are destroyed (mined).
    /// Used by VoxelLootSystem to spawn drops.
    /// Added to a singleton entity or processed per-frame.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct VoxelDestroyedEvent : IBufferElementData
    {
        public float3 Position;
        public byte MaterialID;
        public int Amount; // Approximate volume destroyed (1 for single voxel)
    }
    public struct VoxelModificationRequest : IComponentData
    {
        public int3 ChunkPos;
        public int3 LocalVoxelPos;
        public byte TargetDensity;
        public byte TargetMaterial;
    }

    /// <summary>
    /// Tag for the singleton entity hosting VoxelDestroyedEvents.
    /// </summary>
    public struct VoxelEventsSingleton : IComponentData {}
}
