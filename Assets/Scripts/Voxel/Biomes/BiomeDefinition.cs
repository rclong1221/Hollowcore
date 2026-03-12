using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Geology;
using DIG.Voxel.Fluids;

namespace DIG.Voxel.Biomes
{
    /// <summary>
    /// Defines a biome's properties, materials, and generation rules.
    /// Used for both solid layer variation and hollow earth distinct zones.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Biome Definition")]
    public class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID for this biome (1-255)")]
        public byte BiomeID;
        public string BiomeName = "Unknown";
        public Color DebugColor = Color.gray;
        
        [Header("Conditions (Solid Layers)")]
        [Tooltip("Minimum Temperature (-1 to 1)")]
        [Range(-1f, 1f)] public float MinTemperature = -1f;
        [Tooltip("Maximum Temperature (-1 to 1)")]
        [Range(-1f, 1f)] public float MaxTemperature = 1f;
        
        [Tooltip("Minimum Humidity (-1 to 1)")]
        [Range(-1f, 1f)] public float MinHumidity = -1f;
        [Tooltip("Maximum Humidity (-1 to 1)")]
        [Range(-1f, 1f)] public float MaxHumidity = 1f;
        
        [Header("Material Overrides")]
        [Tooltip("Primary material for surface/floor")]
        public VoxelMaterialDefinition SurfaceMaterial;
        
        [Tooltip("Material just below surface")]
        public VoxelMaterialDefinition SubsurfaceMaterial;
        
        [Tooltip("Material for walls/stone")]
        public VoxelMaterialDefinition WallMaterial;
        
        [Header("Ore Modifiers")]
        [Tooltip("Multiplier for ore spawn rates")]
        [Range(0f, 5f)] public float OreSpawnMultiplier = 1f;
        
        [Header("Fluid Override")]
        [Tooltip("Override fluid type for this biome (optional)")]
        public FluidDefinition FluidOverride;
        
        [Header("Environment")]
        [ColorUsage(true, true)]
        public Color AmbientLight = new Color(0.1f, 0.1f, 0.15f);
        public Color FogColor = new Color(0.05f, 0.05f, 0.1f);
        [Range(0f, 0.1f)] public float FogDensity = 0.02f;
        
        [Header("Audio")]
        public AudioClip AmbienceLoop;
        
        private void OnValidate()
        {
            if (BiomeID == 0)
                UnityEngine.Debug.LogWarning($"[BiomeDefinition] {name} has ID 0, which is reserved for 'None'");
        }
    }
}
