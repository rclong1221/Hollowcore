using UnityEngine;
using DIG.Weapons.Audio;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Controls impact particle effects.
    /// Attach to impact effect prefabs for automatic behavior.
    /// </summary>
    public class ImpactEffectController : MonoBehaviour
    {
        [Header("Particle Systems")]
        [Tooltip("Main impact particles (dust, sparks, etc.)")]
        [SerializeField] private ParticleSystem mainParticles;

        [Tooltip("Secondary particles (debris, fragments)")]
        [SerializeField] private ParticleSystem secondaryParticles;

        [Tooltip("Smoke/dust trail particles")]
        [SerializeField] private ParticleSystem smokeParticles;

        [Header("Surface Variations")]
        [Tooltip("Override colors based on surface type")]
        [SerializeField] private bool useSurfaceColors = true;

        [SerializeField] private SurfaceColorOverride[] surfaceColors;

        [System.Serializable]
        public class SurfaceColorOverride
        {
            public SurfaceMaterialType SurfaceType;
            public Color ParticleColor = Color.white;
            public Color SmokeColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        [Header("Debris")]
        [Tooltip("Spawn debris chunks")]
        [SerializeField] private bool spawnDebris = false;

        [Tooltip("Debris prefab to spawn")]
        [SerializeField] private GameObject debrisPrefab;

        [Tooltip("Number of debris pieces")]
        [SerializeField] private int debrisCount = 3;

        [Tooltip("Debris spread force")]
        [SerializeField] private float debrisForce = 2f;

        [Header("Ricochet")]
        [Tooltip("Spawn ricochet sparks for hard surfaces")]
        [SerializeField] private bool enableRicochetSparks = true;

        [Tooltip("Ricochet spark prefab")]
        [SerializeField] private GameObject ricochetSparkPrefab;

        private SurfaceMaterialType _currentSurfaceType = SurfaceMaterialType.Default;

        private void OnEnable()
        {
            // Auto-play particles when spawned
            PlayAllParticles();
        }

        /// <summary>
        /// Set the surface type for appropriate visual effects.
        /// </summary>
        public void SetSurfaceType(SurfaceMaterialType surfaceType)
        {
            _currentSurfaceType = surfaceType;

            if (useSurfaceColors)
            {
                ApplySurfaceColors(surfaceType);
            }

            // Spawn ricochet sparks for hard surfaces
            if (enableRicochetSparks && IsHardSurface(surfaceType))
            {
                SpawnRicochetSparks();
            }

            // Spawn debris for destructible surfaces
            if (spawnDebris && CanSpawnDebris(surfaceType))
            {
                SpawnDebrisChunks();
            }
        }

        private void PlayAllParticles()
        {
            if (mainParticles != null && !mainParticles.isPlaying)
                mainParticles.Play();

            if (secondaryParticles != null && !secondaryParticles.isPlaying)
                secondaryParticles.Play();

            if (smokeParticles != null && !smokeParticles.isPlaying)
                smokeParticles.Play();
        }

        private void ApplySurfaceColors(SurfaceMaterialType surfaceType)
        {
            if (surfaceColors == null) return;

            foreach (var colorOverride in surfaceColors)
            {
                if (colorOverride.SurfaceType == surfaceType)
                {
                    // Apply main particle color
                    if (mainParticles != null)
                    {
                        var main = mainParticles.main;
                        main.startColor = colorOverride.ParticleColor;
                    }

                    // Apply smoke color
                    if (smokeParticles != null)
                    {
                        var main = smokeParticles.main;
                        main.startColor = colorOverride.SmokeColor;
                    }

                    return;
                }
            }
        }

        private bool IsHardSurface(SurfaceMaterialType surfaceType)
        {
            return surfaceType switch
            {
                SurfaceMaterialType.Metal => true,
                SurfaceMaterialType.Concrete => true,
                SurfaceMaterialType.Glass => true,
                _ => false
            };
        }

        private bool CanSpawnDebris(SurfaceMaterialType surfaceType)
        {
            return surfaceType switch
            {
                SurfaceMaterialType.Wood => true,
                SurfaceMaterialType.Concrete => true,
                SurfaceMaterialType.Glass => true,
                SurfaceMaterialType.Plastic => true,
                _ => false
            };
        }

        private void SpawnRicochetSparks()
        {
            if (ricochetSparkPrefab == null) return;

            // Spawn spark at impact point going in reflected direction
            var spark = Instantiate(ricochetSparkPrefab, transform.position, transform.rotation);

            // Add random velocity
            var rb = spark.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 reflectDir = Vector3.Reflect(-transform.forward, transform.up);
                reflectDir += Random.insideUnitSphere * 0.3f;
                rb.linearVelocity = reflectDir.normalized * Random.Range(5f, 15f);
            }

            Destroy(spark, 0.5f);
        }

        private void SpawnDebrisChunks()
        {
            if (debrisPrefab == null) return;

            for (int i = 0; i < debrisCount; i++)
            {
                Vector3 offset = Random.insideUnitSphere * 0.1f;
                var debris = Instantiate(debrisPrefab, transform.position + offset, Random.rotation);

                var rb = debris.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 force = (transform.up + Random.insideUnitSphere).normalized * debrisForce;
                    rb.AddForce(force, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * debrisForce, ForceMode.Impulse);
                }

                // Scale variation
                float scale = Random.Range(0.5f, 1.5f);
                debris.transform.localScale = Vector3.one * scale;

                Destroy(debris, Random.Range(2f, 4f));
            }
        }
    }
}
