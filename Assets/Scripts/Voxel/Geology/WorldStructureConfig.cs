using UnityEngine;
using System.Linq;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Master configuration for the multi-layer world structure.
    /// Defines all solid and hollow layers from surface to core.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/World Structure Config")]
    public class WorldStructureConfig : ScriptableObject
    {
        [Header("Layer Definitions")]
        [Tooltip("All layers from surface to core, in order by depth")]
        public WorldLayerDefinition[] Layers;
        
        [Header("Global Settings")]
        [Tooltip("World generation seed")]
        public uint WorldSeed = 12345;
        
        [Tooltip("Ground level (Y=0 is surface)")]
        public float GroundLevel = 0f;
        
        [Header("Inter-Layer Connections")]
        [Tooltip("Use guaranteed spline-based connectors between layers")]
        public bool UseSplineConnectors = true;
        
        [Tooltip("Minimum connector tunnels per layer transition")]
        public int MinConnectorsPerTransition = 3;
        
        [Tooltip("Maximum connector tunnels per layer transition")]
        public int MaxConnectorsPerTransition = 8;
        
        [Header("Performance / Streaming")]
        [Tooltip("Layers above current player position to keep loaded")]
        public int LayersAboveToLoad = 1;
        
        [Tooltip("Layers below current player position to keep loaded")]
        public int LayersBelowToLoad = 1;
        
        [Tooltip("Horizontal view distance for chunk loading (meters)")]
        public float HorizontalViewDistance = 256f;
        
        /// <summary>
        /// Get total world depth (distance from surface to bottom of deepest layer).
        /// </summary>
        public float GetTotalDepth()
        {
            if (Layers == null || Layers.Length == 0) return 0;
            
            float minDepth = 0;
            foreach (var layer in Layers)
            {
                if (layer != null && layer.BottomDepth < minDepth)
                    minDepth = layer.BottomDepth;
            }
            return Mathf.Abs(minDepth);
        }
        
        /// <summary>
        /// Count of hollow earth layers.
        /// </summary>
        public int HollowLayerCount => Layers?.Count(l => l != null && l.Type == LayerType.Hollow) ?? 0;
        
        /// <summary>
        /// Count of solid layers.
        /// </summary>
        public int SolidLayerCount => Layers?.Count(l => l != null && l.Type == LayerType.Solid) ?? 0;
        
        /// <summary>
        /// Estimated total playtime across all layers (minutes).
        /// </summary>
        public float TotalPlaytimeMinutes => Layers?.Sum(l => l?.TargetPlaytimeMinutes ?? 0) ?? 0;
        
        /// <summary>
        /// Get the layer definition at a specific world Y position.
        /// </summary>
        public WorldLayerDefinition GetLayerAtDepth(float worldY)
        {
            if (Layers == null) return null;
            
            foreach (var layer in Layers)
            {
                if (layer != null && layer.ContainsDepth(worldY))
                    return layer;
            }
            return null;
        }
        
        /// <summary>
        /// Get layer index for a world Y position.
        /// </summary>
        public int GetLayerIndex(float worldY)
        {
            if (Layers == null) return -1;
            
            for (int i = 0; i < Layers.Length; i++)
            {
                if (Layers[i] != null && Layers[i].ContainsDepth(worldY))
                    return i;
            }
            return -1;
        }
        
        /// <summary>
        /// Get the layer definition by index.
        /// </summary>
        public WorldLayerDefinition GetLayerByIndex(int index)
        {
            if (Layers == null || index < 0 || index >= Layers.Length)
                return null;
            return Layers[index];
        }
        
        /// <summary>
        /// Check if a layer should be loaded based on player position.
        /// </summary>
        public bool ShouldLayerBeLoaded(int layerIndex, int playerLayerIndex)
        {
            if (Layers == null || layerIndex < 0 || layerIndex >= Layers.Length)
                return false;
            
            int minLayer = Mathf.Max(0, playerLayerIndex - LayersAboveToLoad);
            int maxLayer = Mathf.Min(Layers.Length - 1, playerLayerIndex + LayersBelowToLoad);
            
            return layerIndex >= minLayer && layerIndex <= maxLayer;
        }
        
        private void OnValidate()
        {
            // Validate layer ordering
            if (Layers != null && Layers.Length > 1)
            {
                for (int i = 1; i < Layers.Length; i++)
                {
                    if (Layers[i] != null && Layers[i - 1] != null)
                    {
                        // Each layer's top should be at or below the previous layer's bottom
                        if (Layers[i].TopDepth > Layers[i - 1].BottomDepth)
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[WorldStructureConfig] Layer overlap: {Layers[i - 1].LayerName} and {Layers[i].LayerName}");
                        }
                    }
                }
            }
            
            // Validate streaming settings
            if (LayersAboveToLoad < 0) LayersAboveToLoad = 0;
            if (LayersBelowToLoad < 0) LayersBelowToLoad = 0;
            if (LayersAboveToLoad + LayersBelowToLoad < 1)
            {
                LayersBelowToLoad = 1; // Always load at least one adjacent layer
            }
        }
    }
}
