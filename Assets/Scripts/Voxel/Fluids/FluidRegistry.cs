using UnityEngine;

namespace DIG.Voxel.Fluids
{
    /// <summary>
    /// Registry for all available fluid definitions in the game.
    /// Used by FluidService to initialize global fluid data.
    /// </summary>
    [CreateAssetMenu(fileName = "FluidRegistry", menuName = "DIG/Voxel/Fluids/Fluid Registry")]
    public class FluidRegistry : ScriptableObject
    {
        [Tooltip("List of all defined fluids. The index in this array generally corresponds to the FluidID.")]
        public FluidDefinition[] Fluids;
        
        [Tooltip("Global water level for world generation.")]
        public float GlobalWaterLevel = 0f;
    }
}
