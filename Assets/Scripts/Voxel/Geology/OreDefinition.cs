using UnityEngine;
using Unity.Mathematics;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Defines the spawn conditions and shape for an ore type.
    /// Ore veins are generated using 3D noise for coherent deposits.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Ore Definition")]
    public class OreDefinition : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Material ID from VoxelMaterialRegistry")]
        public byte MaterialID;
        
        [Tooltip("Display name for tools and UI")]
        public string OreName;
        
        [Tooltip("Rarity tier for depth-based probability curves")]
        public OreRarity Rarity = OreRarity.Common;
        
        [Header("Spawn Rules")]
        [Tooltip("Minimum depth where this ore can spawn (meters)")]
        public float MinDepth = 10f;
        
        [Tooltip("Maximum depth where this ore can spawn")]
        public float MaxDepth = 100f;
        
        [Tooltip("Noise threshold (0-1). Higher = rarer. 0.7 means ore only spawns where noise > 0.7")]
        [Range(0f, 1f)]
        public float Threshold = 0.7f;
        
        [Header("Vein Shape")]
        [Tooltip("Noise frequency. Smaller = larger veins, larger = more frequent small pockets")]
        public float NoiseScale = 0.1f;
        
        [Tooltip("Expected vein size in voxels (affects noise interpretation)")]
        public float VeinSize = 3f;
        
        [Tooltip("Apply domain warping for organic, twisted vein shapes")]
        public bool DomainWarping = true;
        
        [Tooltip("Strength of domain warping")]
        [Range(0f, 10f)]
        public float WarpStrength = 5f;
        
        [Header("Host Rock")]
        [Tooltip("Only spawns within these materials. Empty = any solid material.")]
        public byte[] HostMaterials;
        
        [Header("Visualization")]
        public Color DebugColor = Color.yellow;
        
        /// <summary>
        /// Check if ore should spawn at this position.
        /// Returns true if ore noise exceeds threshold and depth is valid.
        /// </summary>
        public bool ShouldSpawnAt(float3 worldPos, float depth, byte hostMaterial, uint seed)
        {
            // Depth check
            if (depth < MinDepth || depth > MaxDepth)
                return false;
            
            // Host material check
            if (HostMaterials != null && HostMaterials.Length > 0)
            {
                bool validHost = false;
                foreach (var host in HostMaterials)
                {
                    if (host == hostMaterial)
                    {
                        validHost = true;
                        break;
                    }
                }
                if (!validHost) return false;
            }
            
            // Calculate ore noise
            float oreNoise = GetOreNoise(worldPos, seed);
            
            return oreNoise > Threshold;
        }
        
        /// <summary>
        /// Get the raw ore noise value for this position (0-1 range roughly).
        /// Used for visualization and debugging.
        /// </summary>
        public float GetOreNoise(float3 worldPos, uint seed)
        {
            float3 samplePos = worldPos + seed;
            
            // Apply domain warping for organic shapes
            if (DomainWarping)
            {
                samplePos += new float3(
                    noise.snoise(worldPos * 0.05f) * WarpStrength,
                    noise.snoise(worldPos * 0.05f + 100) * WarpStrength,
                    noise.snoise(worldPos * 0.05f + 200) * WarpStrength
                );
            }
            
            // Sample 3D noise
            float rawNoise = noise.snoise(samplePos * NoiseScale);
            
            // Normalize from [-1, 1] to [0, 1]
            return (rawNoise + 1f) * 0.5f;
        }
    }
    
    /// <summary>
    /// Ore rarity tiers for depth-based probability curves.
    /// </summary>
    public enum OreRarity
    {
        Common,     // Coal, Iron - abundant at shallow depths
        Uncommon,   // Copper, Tin - moderate depths
        Rare,       // Gold, Silver - deeper
        Legendary   // Diamond, Mythril - very deep only
    }
}
