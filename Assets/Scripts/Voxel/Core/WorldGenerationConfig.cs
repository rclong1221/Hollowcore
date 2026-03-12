using UnityEngine;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Master configuration for world generation.
    /// Links strata profiles, ore definitions, and depth curves.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/World Generation Config")]
    public class WorldGenerationConfig : ScriptableObject
    {
        [Header("Terrain")]
        [Tooltip("Ground level (Y coordinate where surface is)")]
        public float GroundLevel = 0f;
        
        [Tooltip("World generation seed")]
        public uint Seed = 12345;
        
        [Header("Geology")]
        [Tooltip("Strata profile defining rock layers by depth")]
        public StrataProfile StrataProfile;
        
        [Tooltip("Depth-based ore rarity curves")]
        public DepthValueCurve DepthCurve;
        
        [Header("Ores")]
        [Tooltip("All ore types that can spawn in this world")]
        public OreDefinition[] OreDefinitions;
        
        [Header("Terrain Noise")]
        [Tooltip("Scale of terrain height noise")]
        public float TerrainNoiseScale = 0.02f;
        
        [Tooltip("Amplitude of terrain height variation (voxels)")]
        public float TerrainNoiseAmplitude = 10f;
        
        [Header("Performance")]
        [Tooltip("Enable ore generation (can be disabled for testing)")]
        public bool EnableOres = true;
        
        [Tooltip("Enable strata variation (can be disabled for flat terrain testing)")]
        public bool EnableStrata = true;
        
        /// <summary>
        /// Initialize the geology service with this config's data.
        /// </summary>
        public void InitializeGeologyService()
        {
            GeologyService.Initialize(
                StrataProfile,
                EnableOres ? OreDefinitions : null,
                DepthCurve,
                Seed
            );
        }
        
        private void OnValidate()
        {
            if (TerrainNoiseScale <= 0) TerrainNoiseScale = 0.02f;
            if (TerrainNoiseAmplitude < 0) TerrainNoiseAmplitude = 0;
        }
    }
}
