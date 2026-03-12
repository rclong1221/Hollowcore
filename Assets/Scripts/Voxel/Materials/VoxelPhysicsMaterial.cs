using UnityEngine;

namespace DIG.Voxel.Materials
{
    /// <summary>
    /// Manages the PhysicMaterial asset for voxel terrain.
    /// Singleton pattern ensures all chunks share the same material.
    /// </summary>
    public static class VoxelPhysicsMaterial
    {
        private static PhysicsMaterial _material;
        
        /// <summary>
        /// Gets the shared PhysicMaterial for all voxel chunks.
        /// Creates it on first access with configured properties.
        /// </summary>
        public static PhysicsMaterial Material
        {
            get
            {
                if (_material == null)
                {
                    _material = new PhysicsMaterial("VoxelTerrain");
                    
                    // Natural rock/dirt friction values
                    _material.dynamicFriction = 0.6f;
                    _material.staticFriction = 0.6f;
                    
                    // No bounciness - terrain should feel solid
                    _material.bounciness = 0.0f;
                    
                    // Standard combine modes
                    _material.frictionCombine = PhysicsMaterialCombine.Average;
                    _material.bounceCombine = PhysicsMaterialCombine.Minimum;
                }
                
                return _material;
            }
        }
    }
}
