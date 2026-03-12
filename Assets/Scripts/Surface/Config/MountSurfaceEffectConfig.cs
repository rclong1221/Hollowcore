using UnityEngine;
using Audio.Systems;

namespace DIG.Surface.Config
{
    /// <summary>
    /// EPIC 15.24 Phase 10: Per-mount-type surface effect configuration.
    /// Defines track decals, spray VFX, skid thresholds, and track spacing.
    /// Create via: Assets > Create > DIG/Surface/Mount Surface Effect Config
    /// </summary>
    [CreateAssetMenu(fileName = "MountSurfaceEffectConfig", menuName = "DIG/Surface/Mount Surface Effect Config")]
    public class MountSurfaceEffectConfig : ScriptableObject
    {
        [Header("Track Marks")]
        [Tooltip("Decal used for tire tracks, hoof prints, etc.")]
        public DecalData TrackDecal;

        [Tooltip("Distance between track decal spawns (meters).")]
        [Range(0.5f, 5f)]
        public float TrackSpacing = 1.5f;

        [Tooltip("Track decal lifetime in seconds.")]
        public float TrackLifetime = 30f;

        [Header("Skid Marks")]
        [Tooltip("Decal used for skid marks on sudden deceleration.")]
        public DecalData SkidDecal;

        [Tooltip("Deceleration threshold (m/s^2) to trigger skid marks.")]
        [Range(5f, 50f)]
        public float SkidDecelThreshold = 15f;

        [Header("Surface Spray")]
        [Tooltip("Minimum speed for surface spray VFX (dust, mud splash, snow spray).")]
        [Range(1f, 20f)]
        public float SpraySpeedThreshold = 5f;

        [Tooltip("Surfaces that produce spray at speed.")]
        public SurfaceID[] SpraySurfaces = new[]
        {
            SurfaceID.Dirt, SurfaceID.Mud, SurfaceID.Sand,
            SurfaceID.Gravel, SurfaceID.Snow
        };

        public bool IsSpraySurface(SurfaceID surface)
        {
            if (SpraySurfaces == null) return false;
            for (int i = 0; i < SpraySurfaces.Length; i++)
            {
                if (SpraySurfaces[i] == surface) return true;
            }
            return false;
        }
    }
}
