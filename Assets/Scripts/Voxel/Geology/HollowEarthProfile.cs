using UnityEngine;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Lighting source type for hollow earth layers.
    /// </summary>
    public enum HollowLightingType
    {
        Bioluminescence,    // Glowing mushrooms, moss, creatures
        CrystalLight,       // Luminescent crystal formations
        LavaGlow,           // Volcanic lighting from lava
        ArtificialSun,      // Magical/technological light source
        Darkness,           // Player must bring their own light
        MixedSources        // Combination of multiple sources
    }
    
    /// <summary>
    /// Configures a hollow earth layer - a massive underground biome.
    /// Heights range from 500m to 1500m+, with floor areas up to 5km x 5km.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Hollow Earth Profile")]
    public class HollowEarthProfile : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Name of this hollow earth biome")]
        public string BiomeName = "Unknown Cavern";
        
        [Tooltip("Color for debug visualization")]
        public Color BiomeColor = Color.cyan;
        
        [Tooltip("Biome definition for this hollow layer")]
        public Biomes.BiomeDefinition BiomeType;
        
        [Header("Dimensions")]
        [Tooltip("Average height from floor to ceiling (meters). 500m minimum for 'hollow world' feel.")]
        [Range(100f, 2000f)]
        public float AverageHeight = 500f;
        
        [Tooltip("Height variation from noise (meters)")]
        [Range(0f, 200f)]
        public float HeightVariation = 50f;
        
        [Tooltip("Floor area width (meters). 2000m = 2km.")]
        public float FloorWidth = 2000f;
        
        [Tooltip("Floor area length (meters). 2000m = 2km.")]
        public float FloorLength = 2000f;
        
        /// <summary>
        /// Total floor area in square kilometers.
        /// </summary>
        public float AreaKm2 => (FloorWidth * FloorLength) / 1_000_000f;
        
        /// <summary>
        /// Approximate volume in cubic kilometers.
        /// </summary>
        public float VolumeKm3 => (FloorWidth * FloorLength * AverageHeight) / 1_000_000_000f;
        
        [Header("Ceiling Configuration")]
        [Tooltip("Scale of noise for ceiling shape (smaller = larger features)")]
        public float CeilingNoiseScale = 0.005f;
        
        [Tooltip("Generate stalactites hanging from ceiling")]
        public bool HasStalactites = true;
        
        [Tooltip("Stalactite density (0-1)")]
        [Range(0f, 1f)]
        public float StalactiteDensity = 0.3f;
        
        [Tooltip("Maximum stalactite length (meters)")]
        public float MaxStalactiteLength = 50f;
        
        [Header("Floor Configuration")]
        [Tooltip("Scale of noise for floor terrain")]
        public float FloorNoiseScale = 0.01f;
        
        [Tooltip("Floor terrain amplitude (meters) - how hilly the floor is")]
        public float FloorAmplitude = 30f;
        
        [Tooltip("Generate stalagmites rising from floor")]
        public bool HasStalagmites = true;
        
        [Tooltip("Primary floor material ID")]
        public byte FloorMaterialID = 1;  // Stone default
        
        [Header("Pillars")]
        [Tooltip("Generate natural support pillars connecting floor to ceiling")]
        public bool GeneratePillars = true;
        
        [Tooltip("Pillar frequency (lower = more pillars per km)")]
        [Range(0.001f, 0.1f)]
        public float PillarFrequency = 0.01f;
        
        [Tooltip("Minimum pillar radius (meters)")]
        public float MinPillarRadius = 10f;
        
        [Tooltip("Maximum pillar radius (meters)")]
        public float MaxPillarRadius = 50f;
        
        [Header("Wall Material")]
        [Tooltip("Material ID for hollow walls")]
        public byte WallMaterialID = 1;  // Stone default
        
        [Header("Lighting")]
        [Tooltip("Primary light source in this hollow")]
        public HollowLightingType LightSource = HollowLightingType.Bioluminescence;
        
        [Tooltip("Ambient light color")]
        [ColorUsage(true, true)]
        public Color AmbientColor = new Color(0.1f, 0.2f, 0.3f);
        
        [Tooltip("Ambient light intensity")]
        [Range(0f, 1f)]
        public float AmbientIntensity = 0.3f;
        
        [Header("Features")]
        [Tooltip("Has underground lakes/water bodies")]
        public bool HasUndergroundLakes = true;
        
        [Tooltip("Water level above floor base (meters)")]
        public float LakeElevation = 10f;
        
        [Tooltip("Has glowing crystal formations")]
        public bool HasCrystalFormations = false;
        
        [Tooltip("Has lava flows/rivers")]
        public bool HasLavaFlows = false;
        
        [Tooltip("Has floating islands (for very tall hollows)")]
        public bool HasFloatingIslands = false;
        
        [Header("Fluid Configuration")]
        [Tooltip("Primary fluid type (water, lava, etc.)")]
        public Fluids.FluidType PrimaryFluidType = Fluids.FluidType.Water;
        
        [Tooltip("Percentage of floor area covered by primary fluid")]
        [Range(0f, 1f)]
        public float FluidCoverage = 0.3f;
        
        [Tooltip("Has fluid rivers/channels")]
        public bool HasFluidRivers = false;
        
        [Tooltip("River width if enabled")]
        public float RiverWidth = 20f;
        
        [Header("Environment")]
        [Tooltip("Fog color for this hollow")]
        public Color FogColor = new Color(0.05f, 0.08f, 0.12f);
        
        [Tooltip("Fog density")]
        [Range(0f, 0.1f)]
        public float FogDensity = 0.02f;
        
        [Tooltip("Temperature in this hollow (affects player)")]
        public float Temperature = 20f;
        
        private void OnValidate()
        {
            // Enforce minimum height for hollow earth feel
            if (AverageHeight < 100f)
            {
                AverageHeight = 100f;
                UnityEngine.Debug.LogWarning($"[HollowEarthProfile] {BiomeName}: Height increased to minimum 100m");
            }
            
            // Stalactites can't be longer than half the height
            if (MaxStalactiteLength > AverageHeight * 0.5f)
            {
                MaxStalactiteLength = AverageHeight * 0.5f;
            }
            
            // Floor amplitude shouldn't exceed a quarter of height
            if (FloorAmplitude > AverageHeight * 0.25f)
            {
                FloorAmplitude = AverageHeight * 0.25f;
            }
        }
    }
}
