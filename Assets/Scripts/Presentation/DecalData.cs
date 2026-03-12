using UnityEngine;

namespace Audio.Systems
{
    /// <summary>
    /// EPIC 13.18.2: Decal Data ScriptableObject
    /// 
    /// Defines the visual appearance of a surface decal (bullet hole, scorch mark, footprint, etc.).
    /// Referenced by SurfaceMaterial to specify which decal to spawn on impact.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/DecalData", fileName = "DecalData")]
    public class DecalData : ScriptableObject
    {
        [Tooltip("Decal material (should be compatible with URP Decal Projector).")]
        public Material DecalMaterial;

        [Header("Size")]
        [Tooltip("Base width/height of the decal in world units.")]
        public float Size = 0.2f;
        
        [Tooltip("Random size variation (0-1). Final size = Size * (1 + Random(-Variation, Variation)).")]
        [Range(0f, 0.5f)]
        public float SizeVariation = 0.1f;

        [Header("Projection")]
        [Tooltip("How deep the decal projects into the surface.")]
        public float ProjectionDepth = 0.5f;
        
        [Tooltip("Random rotation applied to the decal (0-360 degrees).")]
        [Range(0f, 360f)]
        public float RandomRotation = 360f;

        [Header("Lifetime")]
        [Tooltip("How long the decal persists before fading. 0 = permanent (relies on pool recycling).")]
        public float Lifetime = 0f;
        
        [Tooltip("Fade duration in seconds before decal is removed.")]
        public float FadeDuration = 1f;

        [Header("Advanced")]
        [Tooltip("Layer mask for decal projection (which surfaces the decal appears on).")]
        public LayerMask ProjectionLayers = ~0;
        
        [Tooltip("Normal angle threshold. Decal won't appear on surfaces angled more than this from impact normal.")]
        [Range(0f, 90f)]
        public float NormalAngleThreshold = 60f;
    }
}
