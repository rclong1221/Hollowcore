using Unity.Entities;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// OPTIMIZATION 10.2.13: Chunk Density Histogram.
    /// Stores statistical info about the chunk's voxel contents.
    /// Use this to Early-Out of meshing, physics baking, or decorator spawning.
    /// </summary>
    public struct ChunkDensityStats : IComponentData
    {
        public int SolidCount;    // Density == 255
        public int AirCount;      // Density == 0
        public int SurfaceCount;  // 0 < Density < 255
        
        /// <summary>
        /// True if the chunk is completely empty (all Air).
        /// Meshing and Physics can be skipped.
        /// </summary>
        public bool IsEmpty => SolidCount == 0 && SurfaceCount == 0;
        
        /// <summary>
        /// True if the chunk is completely solid (all Rock).
        /// Meshing logic might be simplified (no internal faces), Physics might be simpler (Box collider).
        /// </summary>
        public bool IsFull => AirCount == 0 && SurfaceCount == 0;
    }
}
