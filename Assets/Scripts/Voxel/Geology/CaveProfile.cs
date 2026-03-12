using UnityEngine;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Configures cave generation for solid layers.
    /// Supports multiple cave types: swiss cheese, spaghetti, noodle, and caverns.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Cave Profile")]
    public class CaveProfile : ScriptableObject
    {
        [Header("Swiss Cheese Caves")]
        [Tooltip("Small random air pockets throughout rock")]
        public bool EnableSwissCheese = true;
        
        [Tooltip("Scale of swiss cheese noise (larger = bigger pockets)")]
        public float CheeseScale = 0.1f;
        
        [Tooltip("Threshold for air (higher = less caves)")]
        [Range(0f, 1f)]
        public float CheeseThreshold = 0.6f;
        
        [Tooltip("Minimum depth for swiss cheese caves")]
        public float CheeseMinDepth = 10f;
        
        [Tooltip("Maximum depth for swiss cheese caves")]
        public float CheeseMaxDepth = 300f;
        
        [Header("Spaghetti Tunnels")]
        [Tooltip("Winding narrow passages - main exploration tunnels")]
        public bool EnableSpaghetti = true;
        
        [Tooltip("Scale of spaghetti noise")]
        public float SpaghettiScale = 0.05f;
        
        [Tooltip("Width of spaghetti tunnels (larger = wider tunnels)")]
        [Range(0f, 0.5f)]
        public float SpaghettiWidth = 0.1f;
        
        [Tooltip("Minimum depth for spaghetti tunnels")]
        public float SpaghettiMinDepth = 30f;
        
        [Tooltip("Maximum depth for spaghetti tunnels")]
        public float SpaghettiMaxDepth = 400f;
        
        [Header("Noodle Caves")]
        [Tooltip("Larger winding tunnels - major traversal routes")]
        public bool EnableNoodles = true;
        
        [Tooltip("Scale of noodle noise")]
        public float NoodleScale = 0.03f;
        
        [Tooltip("Width of noodle caves")]
        [Range(0f, 0.5f)]
        public float NoodleWidth = 0.15f;
        
        [Tooltip("Minimum depth for noodle caves")]
        public float NoodleMinDepth = 50f;
        
        [Tooltip("Maximum depth for noodle caves")]
        public float NoodleMaxDepth = 500f;
        
        [Header("Large Caverns")]
        [Tooltip("Large open spaces within solid rock")]
        public bool EnableCaverns = true;
        
        [Tooltip("Scale of cavern noise")]
        public float CavernScale = 0.02f;
        
        [Tooltip("Threshold for caverns (higher = less common)")]
        [Range(0f, 1f)]
        public float CavernThreshold = 0.7f;
        
        [Tooltip("Minimum depth for large caverns")]
        public float CavernMinDepth = 80f;
        
        [Header("Vertical Shafts")]
        [Tooltip("Generate vertical drops and climbs")]
        public bool EnableVerticalShafts = true;
        
        [Tooltip("Shaft frequency (lower = more shafts)")]
        public float ShaftFrequency = 0.005f;
        
        [Tooltip("Average shaft radius (meters)")]
        public float ShaftRadius = 5f;
        
        [Header("Connectivity")]
        [Tooltip("Ensure caves form connected networks")]
        public bool EnforceConnectivity = true;
        
        [Tooltip("Width of connecting tunnels between cave systems (meters)")]
        public float ConnectionTunnelWidth = 3f;
        
        [Header("Debug")]
        [Tooltip("Color for cave visualization")]
        public Color CaveDebugColor = Color.yellow;
        
        /// <summary>
        /// Check if any cave type should generate at this depth.
        /// </summary>
        public bool ShouldGenerateCavesAtDepth(float depth)
        {
            if (EnableSwissCheese && depth >= CheeseMinDepth && depth <= CheeseMaxDepth)
                return true;
            if (EnableSpaghetti && depth >= SpaghettiMinDepth && depth <= SpaghettiMaxDepth)
                return true;
            if (EnableNoodles && depth >= NoodleMinDepth && depth <= NoodleMaxDepth)
                return true;
            if (EnableCaverns && depth >= CavernMinDepth)
                return true;
            return false;
        }
        
        private void OnValidate()
        {
            // Ensure min < max for all depth ranges
            if (CheeseMaxDepth < CheeseMinDepth) CheeseMaxDepth = CheeseMinDepth + 100;
            if (SpaghettiMaxDepth < SpaghettiMinDepth) SpaghettiMaxDepth = SpaghettiMinDepth + 100;
            if (NoodleMaxDepth < NoodleMinDepth) NoodleMaxDepth = NoodleMinDepth + 100;
            
            // Ensure scales are reasonable
            if (CheeseScale <= 0) CheeseScale = 0.1f;
            if (SpaghettiScale <= 0) SpaghettiScale = 0.05f;
            if (NoodleScale <= 0) NoodleScale = 0.03f;
            if (CavernScale <= 0) CavernScale = 0.02f;
        }
    }
}
