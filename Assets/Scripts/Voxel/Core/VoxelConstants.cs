namespace DIG.Voxel.Core
{
    public static class VoxelConstants
    {
        // Chunk dimensions
        public const int CHUNK_SIZE = 32;
        public const int CHUNK_SIZE_SQ = CHUNK_SIZE * CHUNK_SIZE;
        public const int VOXELS_PER_CHUNK = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE; // 32,768
        
        // World scale
        public const float VOXEL_SIZE = 1.0f;
        public const float CHUNK_SIZE_WORLD = CHUNK_SIZE * VOXEL_SIZE;
        
        // Density values
        public const byte DENSITY_AIR = 0;
        public const byte DENSITY_SURFACE = 128;  // IsoLevel for Marching Cubes
        public const byte DENSITY_SOLID = 255;
        
        // Gradient configuration
        public const float GRADIENT_WIDTH = 2.0f;  // Voxels over which density transitions
        
        // Material IDs
        // Note: These serve as default/fallback IDs. 
        // Actual definitions should come from VoxelMaterialRegistry.
        public const byte MATERIAL_AIR = 0;
        public const byte MATERIAL_STONE = 1;
        public const byte MATERIAL_DIRT = 2;
        public const byte MATERIAL_IRON_ORE = 3;
        public const byte MATERIAL_GOLD_ORE = 4;
        public const byte MATERIAL_COPPER_ORE = 5;
        
        // Padded sizes for boundary meshing
        public const int PADDED_SIZE = CHUNK_SIZE + 2;
        public const int PADDED_VOLUME = PADDED_SIZE * PADDED_SIZE * PADDED_SIZE;
    }
}
