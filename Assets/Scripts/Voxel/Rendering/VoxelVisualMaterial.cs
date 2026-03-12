using UnityEngine;

namespace DIG.Voxel.Rendering
{
    /// <summary>
    /// Per-material visual properties for voxel rendering.
    /// Used to configure how each material type appears in the world.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Voxel/Visual Material")]
    public class VoxelVisualMaterial : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Must match the MaterialID in VoxelMaterialDefinition")]
        public byte MaterialID;
        public string DisplayName;
        
        [Header("Base Textures")]
        [Tooltip("Main color/albedo texture")]
        public Texture2D Albedo;
        
        [Tooltip("Normal map for surface detail")]
        public Texture2D Normal;
        
        [Tooltip("Height/displacement map for parallax or blending")]
        public Texture2D HeightMap;
        
        [Header("Surface Properties")]
        [Range(0, 1)] public float Smoothness = 0.3f;
        [Range(0, 1)] public float Metallic = 0f;
        [ColorUsage(false, false)]
        public Color Tint = Color.white;
        
        [Header("Detail Textures (for close-up viewing)")]
        [Tooltip("Secondary detail texture that tiles at higher frequency")]
        public Texture2D DetailAlbedo;
        [Tooltip("Detail normal map")]
        public Texture2D DetailNormal;
        [Range(0, 1)] public float DetailStrength = 0.3f;
        [Range(1, 20)] public float DetailScale = 8f;
        
        [Header("Ambient Occlusion")]
        [Range(0, 1)] public float AOStrength = 0.5f;
        
        [Header("Preview")]
        [HideInInspector] public Texture2D PreviewIcon;
        
        /// <summary>
        /// Validates that required textures are assigned.
        /// </summary>
        public bool IsValid => Albedo != null;
        
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(DisplayName) && Albedo != null)
            {
                DisplayName = Albedo.name.Replace("_albedo", "").Replace("_diffuse", "");
            }
        }
    }
}
