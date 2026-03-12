using UnityEngine;
using DIG.Core.Input;

namespace DIG.Surface.Config
{
    /// <summary>
    /// EPIC 15.24 Phase 7: Per-paradigm surface effect scaling profile.
    /// Each InputParadigm gets a profile that multiplies LOD thresholds, particle/decal/shake scales,
    /// and toggles features like screen dirt and footprints.
    /// Create via: Assets > Create > DIG/Surface/Paradigm Surface Profile
    /// </summary>
    [CreateAssetMenu(fileName = "NewParadigmSurfaceProfile", menuName = "DIG/Surface/Paradigm Surface Profile")]
    public class ParadigmSurfaceProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which paradigm this profile applies to.")]
        public InputParadigm Paradigm = InputParadigm.Shooter;

        [Header("LOD Distance Multipliers")]
        [Tooltip("Multiplier for LOD_Full distance threshold. >1 = effects stay full-quality further away.")]
        [Range(0.5f, 3f)]
        public float LODFullMultiplier = 1f;

        [Tooltip("Multiplier for LOD_Reduced distance threshold.")]
        [Range(0.5f, 3f)]
        public float LODReducedMultiplier = 1f;

        [Tooltip("Multiplier for LOD_Minimal distance threshold.")]
        [Range(0.5f, 3f)]
        public float LODMinimalMultiplier = 1f;

        [Header("Effect Scale Multipliers")]
        [Tooltip("Particle system scale multiplier. Isometric cameras need larger particles.")]
        [Range(0.25f, 3f)]
        public float ParticleScaleMultiplier = 1f;

        [Tooltip("Decal size multiplier. Isometric cameras need larger decals to be visible.")]
        [Range(0.25f, 3f)]
        public float DecalScaleMultiplier = 1f;

        [Tooltip("Camera shake intensity multiplier. Isometric cameras should shake less.")]
        [Range(0f, 2f)]
        public float CameraShakeMultiplier = 1f;

        [Header("Feature Toggles")]
        [Tooltip("Enable screen dirt overlay on large explosions.")]
        public bool ScreenDirtEnabled = true;

        [Tooltip("Enable footprint decals.")]
        public bool FootprintsEnabled = true;

        [Tooltip("Enable audio occlusion (LOS raycast). Disable for 2D/top-down.")]
        public bool AudioOcclusionEnabled = true;

        [Header("Audio")]
        [Tooltip("3D spatial blend for impact audio (1=full 3D, 0=2D). Top-down games use lower values.")]
        [Range(0f, 1f)]
        public float Audio3DBlend = 1f;

        [Header("Performance")]
        [Tooltip("Max impact events processed per frame. Lower for mobile/isometric.")]
        [Range(4, 64)]
        public int MaxEventsPerFrame = 32;

        [Tooltip("Distance culling multiplier. >1 = cull further away (wider view paradigms).")]
        [Range(0.5f, 3f)]
        public float DistanceCullingMultiplier = 1f;
    }
}
