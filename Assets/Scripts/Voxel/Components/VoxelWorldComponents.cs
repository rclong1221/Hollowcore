using Unity.Entities;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// Singleton gate component. When present, voxel systems run.
    /// When absent, all voxel generation/streaming/meshing/LOD systems are dormant.
    /// Baked by VoxelWorldAuthoring.
    /// </summary>
    public struct VoxelWorldEnabled : IComponentData { }

    /// <summary>
    /// Optional per-scene configuration overrides for voxel world generation.
    /// When present, generation systems use these values instead of Resources/ defaults.
    /// Baked by VoxelWorldAuthoring when overrides are enabled.
    /// </summary>
    public struct VoxelWorldSettings : IComponentData
    {
        /// <summary>World generation seed for deterministic terrain.</summary>
        public uint Seed;

        /// <summary>Y coordinate of the ground surface.</summary>
        public float GroundLevel;

        /// <summary>Horizontal streaming distance in chunks.</summary>
        public int ViewDistance;

        /// <summary>Scale of terrain height Perlin noise.</summary>
        public float TerrainNoiseScale;

        /// <summary>Amplitude of terrain height variation in voxels.</summary>
        public float TerrainNoiseAmplitude;

        /// <summary>Enable ore generation.</summary>
        public bool EnableOres;

        /// <summary>Enable strata (rock layer) variation.</summary>
        public bool EnableStrata;

        /// <summary>Enable cave/hollow earth generation.</summary>
        public bool EnableCaves;

        /// <summary>Enable biome system.</summary>
        public bool EnableBiomes;
    }
}
