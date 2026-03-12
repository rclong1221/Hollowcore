using UnityEngine;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Type of world layer.
    /// </summary>
    public enum LayerType
    {
        Solid,      // Rock with caves - uses StrataProfile and CaveProfile
        Hollow,     // Open biome space - uses HollowEarthProfile
        Transition  // Thin connecting layer between solid and hollow
    }
    
    /// <summary>
    /// Defines a single layer in the multi-layer world.
    /// Each layer can be solid (rock with caves) or hollow (underground biome).
    /// Designers configure depth ranges, floor areas, and layer-specific profiles.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/World Layer Definition")]
    public class WorldLayerDefinition : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Display name for this layer")]
        public string LayerName = "Unnamed Layer";
        
        [Tooltip("Index in the world structure (0 = first layer below surface)")]
        public int LayerIndex;
        
        [Tooltip("Type of layer: Solid (rock with caves) or Hollow (open biome)")]
        public LayerType Type = LayerType.Solid;
        
        [Header("Depth Configuration")]
        [Tooltip("Depth where this layer starts (Y coordinate, negative = below ground)")]
        public float TopDepth = 0f;
        
        [Tooltip("Depth where this layer ends (should be more negative than TopDepth)")]
        public float BottomDepth = -400f;
        
        /// <summary>
        /// Calculate layer thickness in meters.
        /// </summary>
        public float Thickness => Mathf.Abs(BottomDepth - TopDepth);
        
        [Header("Horizontal Extent")]
        [Tooltip("Width of this layer in meters (X axis)")]
        public float AreaWidth = 2000f;
        
        [Tooltip("Length of this layer in meters (Z axis)")]
        public float AreaLength = 2000f;
        
        /// <summary>
        /// Total floor area in square kilometers.
        /// </summary>
        public float AreaKm2 => (AreaWidth * AreaLength) / 1_000_000f;
        
        [Header("Solid Layer Configuration")]
        [Tooltip("Strata profile for rock layers (solid layers only)")]
        public StrataProfile StrataProfile;
        
        [Tooltip("Cave generation settings (solid layers only)")]
        public CaveProfile CaveProfile;
        
        [Header("Hollow Layer Configuration")]
        [Tooltip("Hollow earth profile (hollow layers only)")]
        public HollowEarthProfile HollowProfile;
        
        [Header("Connectivity")]
        [Tooltip("Allow tunnels/caves from layer above to connect here")]
        public bool HasEntriesFromAbove = true;
        
        [Tooltip("Allow tunnels/caves from here to connect to layer below")]
        public bool HasExitsBelow = true;
        
        [Tooltip("Density of connector tunnels (0 = none, 1 = many)")]
        [Range(0f, 1f)]
        public float ConnectorDensity = 0.5f;
        
        [Header("Gameplay")]
        [Tooltip("Expected exploration time for this layer (minutes)")]
        public float TargetPlaytimeMinutes = 45f;
        
        [Tooltip("Difficulty multiplier (1 = normal)")]
        [Range(0.5f, 3f)]
        public float DifficultyMultiplier = 1f;
        
        [Header("Debug")]
        [Tooltip("Color for layer visualization in editor")]
        public Color DebugColor = Color.gray;
        
        /// <summary>
        /// Check if a world Y position is within this layer.
        /// </summary>
        public bool ContainsDepth(float worldY)
        {
            return worldY <= TopDepth && worldY > BottomDepth;
        }
        
        /// <summary>
        /// Get relative position within layer (0 = top, 1 = bottom).
        /// </summary>
        public float GetRelativeDepth(float worldY)
        {
            if (!ContainsDepth(worldY)) return -1f;
            return (TopDepth - worldY) / Thickness;
        }
        
        private void OnValidate()
        {
            // Ensure bottom is below top
            if (BottomDepth > TopDepth)
            {
                BottomDepth = TopDepth - 100f;
            }
            
            // Minimum layer thickness
            if (Thickness < 50f)
            {
                BottomDepth = TopDepth - 50f;
            }
            
            // Ensure hollow layers have hollow profile
            if (Type == LayerType.Hollow && HollowProfile == null)
            {
                UnityEngine.Debug.LogWarning($"[WorldLayerDefinition] {LayerName}: Hollow layer missing HollowEarthProfile");
            }
        }
    }
}
