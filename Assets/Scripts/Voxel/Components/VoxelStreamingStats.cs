using Unity.Entities;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// Singleton component containing real-time statistics about the streaming system.
    /// Used by debug visualizers.
    /// </summary>
    public struct VoxelStreamingStats : IComponentData
    {
        public int LoadedChunks;
        public int ChunksToSpawnQueue;
        public int ChunksToUnloadQueue;
        public float EstimatedMemoryMB;
    }
}
