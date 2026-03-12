using UnityEngine;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Designer-facing configuration for the minimap and fog-of-war systems.
    /// Load from Resources/MinimapConfig by MinimapBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Map/Minimap Config")]
    public class MinimapConfigSO : ScriptableObject
    {
        [Header("Minimap Camera")]
        [Tooltip("Starting orthographic camera size (world units visible)")]
        public float DefaultZoom = 40f;
        [Tooltip("Closest zoom level (most detail)")]
        public float MinZoom = 20f;
        [Tooltip("Farthest zoom level (widest view)")]
        public float MaxZoom = 80f;
        [Tooltip("Zoom increment per scroll")]
        public float ZoomStep = 5f;
        [Tooltip("Minimap rotates with player facing (false = north-up)")]
        public bool RotateWithPlayer = true;
        [Tooltip("Minimap render texture resolution (power of 2)")]
        public int RenderTextureSize = 512;

        [Header("Icons")]
        [Tooltip("Base scale multiplier for all map icons")]
        public float IconScale = 1f;
        [Tooltip("Frame-spread K — update 1/K icons per frame")]
        [Range(1, 16)]
        public int UpdateFrameSpread = 4;
        [Tooltip("World units — icons beyond this distance are hidden")]
        public float MaxIconRange = 150f;
        [Tooltip("World units — compass POI max distance")]
        public float CompassRange = 500f;

        [Header("Minimap Appearance")]
        public MinimapMaskShape MaskShape = MinimapMaskShape.Circle;
        public Color MinimapBorderColor = Color.white;
        public Color PlayerIconColor = Color.green;

        [Header("Fog of War")]
        [Tooltip("Fog texture resolution width")]
        public int FogTextureWidth = 1024;
        [Tooltip("Fog texture resolution height")]
        public int FogTextureHeight = 1024;
        [Tooltip("World units revealed per step")]
        public float RevealRadius = 15f;
        [Tooltip("Min movement before new reveal circle (avoids redundant draws)")]
        public float RevealMoveThreshold = 2f;
        [Tooltip("World XZ minimum for fog UV mapping")]
        public Vector2 WorldBoundsMin = new Vector2(-500, -500);
        [Tooltip("World XZ maximum for fog UV mapping")]
        public Vector2 WorldBoundsMax = new Vector2(500, 500);
        [Tooltip("Unexplored area overlay color")]
        public Color FogUnexploredColor = new Color(0, 0, 0, 0.9f);
        [Tooltip("Fully explored area (transparent)")]
        public Color FogExploredColor = Color.clear;
    }

    public enum MinimapMaskShape : byte
    {
        Circle = 0,
        Square = 1
    }
}
