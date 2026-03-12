using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

namespace Audio.Systems
{
    /// <summary>
    /// EPIC 13.18.2: Decal Manager
    /// 
    /// Manages a ring-buffer pool of URP DecalProjectors for efficient decal spawning.
    /// Oldest decals are recycled when the pool is full.
    /// 
    /// Usage:
    ///   DecalManager.Instance.SpawnDecal(decalData, position, rotation, lifetime);
    /// </summary>
    public class DecalManager : MonoBehaviour
    {
        private static DecalManager s_Instance;
        public static DecalManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindFirstObjectByType<DecalManager>();
                }
                return s_Instance;
            }
        }

        [Header("Pool Settings")]
        [Tooltip("Maximum number of active decals. Oldest decals recycled when exceeded.")]
        public int MaxDecals = 100;

        [Header("Defaults")]
        [Tooltip("Fallback decal data if none specified.")]
        public DecalData FallbackDecal;

        // Pool of decal projectors
        private List<DecalProjectorInstance> _pool = new List<DecalProjectorInstance>();
        private int _nextIndex = 0;

        // Track active decals for fading
        private class DecalProjectorInstance
        {
            public GameObject GameObject;
            public DecalProjector Projector;
            public float SpawnTime;
            public float Lifetime;
            public float FadeDuration;
            public bool IsFading;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            
            // Pre-allocate pool
            InitializePool();
        }

        private void InitializePool()
        {
            for (int i = 0; i < MaxDecals; i++)
            {
                var instance = CreateDecalInstance(i);
                _pool.Add(instance);
            }
        }

        private DecalProjectorInstance CreateDecalInstance(int index)
        {
            var go = new GameObject($"Decal_{index}");
            go.transform.SetParent(transform, false);
            go.SetActive(false);

            var projector = go.AddComponent<DecalProjector>();
            projector.pivot = new Vector3(0, 0, 0.5f); // Project from surface
            projector.fadeFactor = 1f;

            return new DecalProjectorInstance
            {
                GameObject = go,
                Projector = projector,
                SpawnTime = -1000f,
                Lifetime = 0f,
                FadeDuration = 1f,
                IsFading = false
            };
        }

        /// <summary>
        /// Spawn a decal at the specified position and rotation.
        /// </summary>
        /// <param name="data">DecalData defining appearance.</param>
        /// <param name="position">World position of the decal.</param>
        /// <param name="rotation">Rotation of the decal (forward = projection direction).</param>
        /// <param name="lifetimeOverride">Override lifetime (0 = use DecalData.Lifetime).</param>
        public void SpawnDecal(DecalData data, float3 position, quaternion rotation, float lifetimeOverride = 0f)
        {
            if (data == null) data = FallbackDecal;
            if (data == null) return;

            // Get next instance from ring buffer
            var instance = _pool[_nextIndex];
            _nextIndex = (_nextIndex + 1) % _pool.Count;

            // Configure the decal
            instance.GameObject.SetActive(true);
            instance.GameObject.transform.position = position;
            
            // Apply random rotation around the forward axis
            float randomAngle = UnityEngine.Random.Range(0f, data.RandomRotation);
            var finalRotation = math.mul(rotation, quaternion.AxisAngle(new float3(0, 0, 1), math.radians(randomAngle)));
            instance.GameObject.transform.rotation = finalRotation;

            // Apply size with variation
            float sizeVariation = UnityEngine.Random.Range(-data.SizeVariation, data.SizeVariation);
            float finalSize = data.Size * (1f + sizeVariation);
            instance.Projector.size = new Vector3(finalSize, finalSize, data.ProjectionDepth);

            // Apply material
            instance.Projector.material = data.DecalMaterial;
            instance.Projector.fadeFactor = 1f;

            // Track lifetime
            instance.SpawnTime = Time.time;
            instance.Lifetime = lifetimeOverride > 0 ? lifetimeOverride : data.Lifetime;
            instance.FadeDuration = data.FadeDuration;
            instance.IsFading = false;
        }

        /// <summary>
        /// Spawn a decal from a raycast hit.
        /// </summary>
        public void SpawnDecalFromHit(DecalData data, RaycastHit hit, float lifetimeOverride = 0f)
        {
            var rotation = Quaternion.LookRotation(-hit.normal);
            SpawnDecal(data, hit.point, rotation, lifetimeOverride);
        }

        /// <summary>
        /// Clear all active decals.
        /// </summary>
        public void ClearAllDecals()
        {
            foreach (var instance in _pool)
            {
                instance.GameObject.SetActive(false);
                instance.SpawnTime = -1000f;
            }
            _nextIndex = 0;
        }

        /// <summary>
        /// Get the current number of active (visible) decals.
        /// </summary>
        public int GetActiveDecalCount()
        {
            int count = 0;
            foreach (var instance in _pool)
            {
                if (instance.GameObject.activeSelf) count++;
            }
            return count;
        }

        private void Update()
        {
            float time = Time.time;

            foreach (var instance in _pool)
            {
                if (!instance.GameObject.activeSelf) continue;
                if (instance.Lifetime <= 0f) continue; // Lifetime 0 = permanent

                float age = time - instance.SpawnTime;

                // Start fading
                if (age >= instance.Lifetime && !instance.IsFading)
                {
                    instance.IsFading = true;
                }

                // Apply fade
                if (instance.IsFading)
                {
                    float fadeProgress = (age - instance.Lifetime) / instance.FadeDuration;
                    instance.Projector.fadeFactor = Mathf.Clamp01(1f - fadeProgress);

                    // Deactivate when fully faded
                    if (fadeProgress >= 1f)
                    {
                        instance.GameObject.SetActive(false);
                        instance.IsFading = false;
                    }
                }
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
