using Unity.Burst;
using Unity.Mathematics;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Utility for calculating gradient density values.
    /// This is CRITICAL for Marching Cubes to work!
    /// </summary>
    [BurstCompile]
    public static class VoxelDensity
    {
        /// <summary>
        /// Calculate density based on signed distance to surface.
        /// Positive = below surface (solid)
        /// Negative = above surface (air)
        /// </summary>
        [BurstCompile]
        public static byte CalculateGradient(float signedDistance)
        {
            // Gradient width determines how smooth the transition is
            float halfWidth = VoxelConstants.GRADIENT_WIDTH * 0.5f;
            
            // Clamp to gradient range
            float normalized = math.clamp(signedDistance / halfWidth, -1f, 1f);
            
            // Map from [-1, 1] to [0, 255]
            // -1 (far above surface) → 0 (air)
            // 0 (at surface) → 128 (IsoLevel)
            // +1 (far below surface) → 255 (solid)
            float density = (normalized + 1f) * 0.5f * 255f;
            
            return (byte)math.clamp(density, 0f, 255f);
        }
        
        /// <summary>
        /// Check if density represents solid material.
        /// </summary>
        [BurstCompile]
        public static bool IsSolid(byte density)
        {
            return density > VoxelConstants.DENSITY_SURFACE;
        }
        
        /// <summary>
        /// Check if density represents air.
        /// </summary>
        [BurstCompile]
        public static bool IsAir(byte density)
        {
            return density <= VoxelConstants.DENSITY_SURFACE;
        }
    }
}
