using UnityEngine;
using Audio.Systems;

namespace Player.Bridges
{
    /// <summary>
    /// Client-side bridge for collision VFX (Epic 7.4.4).
    /// Attach to the player presentation prefab (ghost presentation GameObject).
    /// </summary>
    public class CollisionVFXBridge : MonoBehaviour
    {
        [Header("Particle Prefabs")]
        [Tooltip("Dust impact prefab used in normal (non-EVA) collisions")]
        public ParticleSystem DustImpact;

        [Tooltip("Spark impact prefab used in EVA collisions")]
        public ParticleSystem SparkImpact;

        [Header("Tuning")]
        [Range(0f, 2f)]
        public float BaseScale = 1.0f;

        [Range(0f, 64f)]
        public float MaxExtraParticles = 24f;

        [Header("Debug")]
        public bool DebugLogging = false;

        [Header("Pooling")]
        [Tooltip("Optional pooled spawner. If unset, will auto-find one in the scene; if none exists, falls back to Instantiate/Destroy.")]
        public VFXManager VFXManager;

        private void Awake()
        {
            if (VFXManager == null)
            {
                VFXManager = Object.FindAnyObjectByType<VFXManager>();
            }
        }

        public void PlayImpactVFX(Vector3 contactPoint, float intensity, bool isEVA)
        {
            intensity = Mathf.Clamp01(intensity);

            // Skip tiny hits to avoid noise.
            if (intensity <= 0.01f)
                return;

            var prefab = isEVA ? SparkImpact : DustImpact;
            if (prefab == null)
                return;

            GameObject go;
            if (VFXManager != null)
            {
                go = VFXManager.SpawnVFX(prefab.gameObject, contactPoint, Quaternion.identity);
            }
            else
            {
                go = Instantiate(prefab.gameObject, contactPoint, Quaternion.identity);
            }

            if (go == null)
                return;

            // Reset transform so pooled instances don't accumulate scale/rotation.
            go.transform.position = contactPoint;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = prefab.transform.localScale * (BaseScale * Mathf.Lerp(0.75f, 1.35f, intensity));

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);

                // Try to scale emission a bit with intensity.
                int extra = Mathf.RoundToInt(MaxExtraParticles * intensity);
                ps.Play(true);
                if (extra > 0)
                {
                    ps.Emit(extra);
                }
            }

            // If pooling isn't available, we still need to clean up.
            if (VFXManager == null)
            {
                float lifetime = 1f;
                if (ps != null)
                {
                    var main = ps.main;
                    lifetime = main.duration + main.startLifetime.constantMax;
                }
                Destroy(go, Mathf.Max(0.25f, lifetime));
            }

            if (DebugLogging)
            {
                Debug.Log($"[CollisionVFXBridge] PlayImpactVFX intensity={intensity:F2} isEVA={isEVA}");
            }
        }
    }
}
