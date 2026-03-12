using UnityEngine;
using Unity.Mathematics;
using DIG.Surface;

namespace Audio.Systems
{
    /// <summary>
    /// EPIC 13.18.1 / EPIC 15.24: Centralized Surface Manager
    ///
    /// Singleton facade for spawning surface effects.
    /// SpawnEffect() now routes through SurfaceImpactQueue for unified processing.
    /// Footprint and footstep methods still delegate directly (Phase 4 migration).
    /// </summary>
    public class SurfaceManager : MonoBehaviour
    {
        private static SurfaceManager s_Instance;
        public static SurfaceManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindFirstObjectByType<SurfaceManager>();
                    if (s_Instance == null)
                    {
                        var go = new GameObject("SurfaceManager");
                        s_Instance = go.AddComponent<SurfaceManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return s_Instance;
            }
        }

        [Header("Dependencies")]
        [Tooltip("Reference to AudioManager for audio/VFX spawning.")]
        public AudioManager AudioManager;
        
        [Tooltip("Reference to DecalManager for decal spawning (optional, created in Phase 2).")]
        public DecalManager DecalManager;

        [Header("Impact Settings")]
        [Tooltip("Minimum velocity required to spawn impact effects.")]
        public float MinImpactVelocity = 1f;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            
            // Auto-find dependencies if not assigned
            if (AudioManager == null)
            {
                AudioManager = FindFirstObjectByType<AudioManager>();
            }
            if (DecalManager == null)
            {
                DecalManager = FindFirstObjectByType<DecalManager>();
            }
        }

        /// <summary>
        /// Spawn impact effect (VFX + Audio + Decal) at the specified location.
        /// </summary>
        /// <param name="position">World position of the impact.</param>
        /// <param name="normal">Surface normal at impact point.</param>
        /// <param name="surfaceMaterialId">Surface material ID from SurfaceDetectionService.</param>
        /// <param name="intensity">Effect intensity (0-1), affects volume and VFX scale.</param>
        public void SpawnEffect(float3 position, float3 normal, int surfaceMaterialId, float intensity = 1f)
        {
            if (intensity < 0.01f) return;

            // EPIC 15.24: Route through unified SurfaceImpactQueue
            SurfaceImpactQueue.Enqueue(new SurfaceImpactData
            {
                Position = position,
                Normal = normal,
                Velocity = float3.zero,
                SurfaceId = SurfaceID.Default,
                ImpactClass = ImpactClass.Bullet_Medium,
                SurfaceMaterialId = surfaceMaterialId,
                Intensity = intensity,
                LODTier = EffectLODTier.Full
            });
        }

        /// <summary>
        /// Spawn impact effect from a Unity Physics RaycastHit.
        /// Automatically resolves surface material from the hit collider.
        /// </summary>
        public void SpawnEffectFromHit(RaycastHit hit, float intensity = 1f)
        {
            int materialId = SurfaceDetectionService.ResolveMaterialIdFromUnityHit(hit);
            SpawnEffect(hit.point, hit.normal, materialId, intensity);
        }

        /// <summary>
        /// Spawn a footprint decal at the specified location.
        /// </summary>
        /// <param name="position">World position of the footprint.</param>
        /// <param name="footRotation">Rotation of the foot (forward = walking direction).</param>
        /// <param name="surfaceMaterialId">Surface material ID.</param>
        /// <param name="flipFootprint">True if this is the right foot (mirror the decal).</param>
        public void SpawnFootprint(float3 position, quaternion footRotation, int surfaceMaterialId, bool flipFootprint = false)
        {
            if (DecalManager == null) return;

            var surfaceMaterial = SurfaceDetectionService.GetMaterial(surfaceMaterialId);
            if (surfaceMaterial == null) return;
            if (surfaceMaterial.FootprintDecal == null || !surfaceMaterial.AllowFootprints) return;

            // Apply flip for right foot by mirroring the rotation
            var finalRotation = footRotation;
            if (flipFootprint)
            {
                finalRotation = math.mul(footRotation, quaternion.AxisAngle(new float3(0, 1, 0), math.PI));
            }

            DecalManager.SpawnDecal(surfaceMaterial.FootprintDecal, position, finalRotation, 30f);
        }

        /// <summary>
        /// Play footstep sound for a specific surface.
        /// </summary>
        public void PlayFootstep(int surfaceMaterialId, float3 position, int stance)
        {
            if (AudioManager != null)
            {
                AudioManager.PlayFootstep(surfaceMaterialId, position, stance);
            }
        }

        /// <summary>
        /// Play landing sound for a specific surface.
        /// </summary>
        public void PlayLanding(int surfaceMaterialId, float3 position, float intensity)
        {
            if (AudioManager != null)
            {
                AudioManager.PlayImpact(surfaceMaterialId, position, intensity);
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }
    }
}
