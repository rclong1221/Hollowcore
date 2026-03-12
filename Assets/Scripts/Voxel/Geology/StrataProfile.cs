using UnityEngine;
using Unity.Mathematics;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Defines geological strata (rock layers) based on depth.
    /// Used by the generation system to determine base rock types.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Strata Profile")]
    public class StrataProfile : ScriptableObject
    {
        [System.Serializable]
        public struct Layer
        {
            [Tooltip("Material ID from VoxelMaterialRegistry")]
            public byte MaterialID;
            
            [Tooltip("Minimum depth where this layer starts (meters below ground)")]
            public float MinDepth;
            
            [Tooltip("Maximum depth where this layer ends")]
            public float MaxDepth;
            
            [Tooltip("Transition zone width for smooth blending")]
            public float BlendWidth;
            
            [Tooltip("How much noise affects layer boundaries (0-1)")]
            [Range(0, 1)] public float NoiseInfluence;
            
            [Tooltip("Display name for editor tools")]
            public string DisplayName;
            
            [Tooltip("Color for visualization")]
            public Color DebugColor;
        }
        
        [Header("Layer Configuration")]
        [Tooltip("Layers ordered from surface to deep. Overlapping depths use first match.")]
        public Layer[] Layers = new Layer[]
        {
            new Layer { MaterialID = 2, MinDepth = 0, MaxDepth = 5, BlendWidth = 2, DisplayName = "Topsoil", DebugColor = new Color(0.4f, 0.3f, 0.2f) },
            new Layer { MaterialID = 1, MinDepth = 5, MaxDepth = 50, BlendWidth = 3, DisplayName = "Stone", DebugColor = Color.gray },
            new Layer { MaterialID = 10, MinDepth = 50, MaxDepth = 150, BlendWidth = 5, NoiseInfluence = 0.3f, DisplayName = "Granite", DebugColor = new Color(0.6f, 0.5f, 0.5f) },
            new Layer { MaterialID = 11, MinDepth = 150, MaxDepth = 999, BlendWidth = 10, NoiseInfluence = 0.4f, DisplayName = "Basalt", DebugColor = new Color(0.2f, 0.2f, 0.25f) }
        };
        
        [Header("Noise Settings")]
        [Tooltip("Seed for layer boundary noise")]
        public uint NoiseSeed = 42;
        
        [Tooltip("Scale of noise for layer boundaries")]
        public float NoiseScale = 0.05f;
        
        /// <summary>
        /// Get the material ID for a given depth and world position.
        /// Uses noise to create natural-looking layer boundaries.
        /// </summary>
        public byte GetMaterialAtDepth(float depth, float3 worldPos)
        {
            // Iterate through layers (first match wins)
            foreach (var layer in Layers)
            {
                float noiseOffset = GetNoiseOffset(worldPos, layer.NoiseInfluence);
                float adjustedMin = layer.MinDepth + noiseOffset;
                float adjustedMax = layer.MaxDepth + noiseOffset;
                
                // Check if depth falls within adjusted layer bounds
                if (depth >= adjustedMin && depth < adjustedMax)
                    return layer.MaterialID;
            }
            
            // Default fallback
            return 1; // Stone
        }
        
        /// <summary>
        /// Check if we're in a blend zone between layers.
        /// Returns blend factor (0 = fully layer A, 1 = fully layer B).
        /// </summary>
        public float GetBlendFactor(float depth, float3 worldPos, out byte materialA, out byte materialB)
        {
            materialA = 1;
            materialB = 1;
            
            for (int i = 0; i < Layers.Length - 1; i++)
            {
                var currentLayer = Layers[i];
                var nextLayer = Layers[i + 1];
                
                float noiseOffset = GetNoiseOffset(worldPos, currentLayer.NoiseInfluence);
                float boundary = currentLayer.MaxDepth + noiseOffset;
                float blendStart = boundary - currentLayer.BlendWidth * 0.5f;
                float blendEnd = boundary + currentLayer.BlendWidth * 0.5f;
                
                if (depth >= blendStart && depth < blendEnd)
                {
                    materialA = currentLayer.MaterialID;
                    materialB = nextLayer.MaterialID;
                    return (depth - blendStart) / currentLayer.BlendWidth;
                }
            }
            
            return 0f;
        }
        
        private float GetNoiseOffset(float3 worldPos, float influence)
        {
            if (influence <= 0) return 0;
            
            float noiseValue = noise.snoise(worldPos * NoiseScale + NoiseSeed);
            return noiseValue * influence * 10f; // Up to ±10 voxels variation
        }
        
        /// <summary>
        /// Get maximum depth defined in this profile.
        /// </summary>
        public float GetMaxDepth()
        {
            float max = 0;
            foreach (var layer in Layers)
            {
                if (layer.MaxDepth > max) max = layer.MaxDepth;
            }
            return max;
        }
        
        private void OnValidate()
        {
            // Sort layers by MinDepth
            if (Layers != null && Layers.Length > 1)
            {
                System.Array.Sort(Layers, (a, b) => a.MinDepth.CompareTo(b.MinDepth));
            }
        }
    }
}
