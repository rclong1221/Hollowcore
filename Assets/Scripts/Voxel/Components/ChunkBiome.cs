using Unity.Entities;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// OPTIMIZATION 10.4.11: Biome Lookup Caching.
    /// Stores the primary biome for this chunk to avoid per-voxel noise lookups.
    /// Used by Generation to optimize, and by Audio/Fog systems for ambience.
    /// </summary>
    public struct ChunkBiome : IComponentData
    {
        public byte BiomeID; // The predominant or homogeneous biome
        public bool IsHomogeneous; // If true, the entire chunk (solid layers) is this biome
    }
}
