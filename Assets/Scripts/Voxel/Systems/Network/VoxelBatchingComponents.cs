using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace DIG.Voxel.Systems.Network
{
    /// <summary>
    /// Represents a single voxel modification.
    /// </summary>
    public struct PendingModification
    {
        public int3 ChunkPos;
        public int3 LocalPos;
        public byte Density;
        public byte Material;
        public uint Tick; // For history tracking
    }

    /// <summary>
    /// Singleton component holding the queue of modifications waiting to be batched and sent.
    /// </summary>
    public struct VoxelBatchingQueue : IComponentData
    {
        public NativeList<PendingModification> Value;
    }

    /// <summary>
    /// Singleton component holding synchronization history.
    /// </summary>
    public struct VoxelHistory : IComponentData
    {
        public NativeList<PendingModification> Value;
    }

    /// <summary>
    /// Singleton component holding network statistics for the VoxelNetworkStatsWindow.
    /// </summary>
    public struct VoxelBatchingStats : IComponentData
    {
        public float RollingModsPerSec;
        public float RollingBatchesPerSec;
        public int ModificationsThisSecond;
        public int TotalBatchesSent;
        public int TotalModificationsSent;
    }
}
