using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Manages pooling for weapon visual effects.
    /// Provides efficient instantiation and recycling of effect GameObjects.
    /// </summary>
    public class EffectPoolManager : MonoBehaviour
    {
        public static EffectPoolManager Instance { get; private set; }

        [Header("Pool Settings")]
        [Tooltip("Default pool size per effect type")]
        [SerializeField] private int defaultPoolSize = 10;

        [Tooltip("Maximum instances per effect type")]
        [SerializeField] private int maxPoolSize = 50;

        [Header("Cleanup")]
        [Tooltip("How often to clean up inactive effects (seconds)")]
        [SerializeField] private float cleanupInterval = 30f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Pool per effect ID
        private Dictionary<int, ObjectPool<GameObject>> _pools = new Dictionary<int, ObjectPool<GameObject>>();

        // Track active effects for cleanup
        private List<ActiveEffect> _activeEffects = new List<ActiveEffect>();

        // Parent transform for pooled objects
        private Transform _poolParent;

        private float _cleanupTimer;

        private struct ActiveEffect
        {
            public GameObject Instance;
            public float SpawnTime;
            public float Lifetime;
            public int EffectId;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Create pool parent
            var poolObj = new GameObject("EffectPool");
            poolObj.transform.SetParent(transform);
            _poolParent = poolObj.transform;
        }

        private void Update()
        {
            float time = Time.time;
            float deltaTime = Time.deltaTime;

            // Update active effects and return expired ones to pool
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];

                if (effect.Instance == null)
                {
                    _activeEffects.RemoveAt(i);
                    continue;
                }

                // Check if lifetime expired
                if (effect.Lifetime > 0 && time - effect.SpawnTime > effect.Lifetime)
                {
                    ReturnToPool(effect.EffectId, effect.Instance);
                    _activeEffects.RemoveAt(i);
                }
            }

            // Periodic cleanup
            _cleanupTimer += deltaTime;
            if (_cleanupTimer >= cleanupInterval)
            {
                _cleanupTimer = 0f;
                CleanupInactivePools();
            }
        }

        /// <summary>
        /// Get an effect instance from the pool.
        /// </summary>
        public GameObject GetEffect(int effectId, Vector3 position, Quaternion rotation, float lifetime = 0f)
        {
            var registry = EffectPrefabRegistry.Instance;
            if (registry == null)
            {
                if (debugLogging)
                    Debug.LogWarning("[EffectPoolManager] No EffectPrefabRegistry found");
                return null;
            }

            var effectEntry = registry.GetEffect(effectId);
            if (effectEntry == null || effectEntry.Prefab == null)
            {
                if (debugLogging)
                    Debug.LogWarning($"[EffectPoolManager] No prefab for effect {effectId}");
                return null;
            }

            // Get or create pool for this effect
            if (!_pools.TryGetValue(effectId, out var pool))
            {
                pool = CreatePool(effectId, effectEntry.Prefab);
                _pools[effectId] = pool;
            }

            // Get instance from pool
            var instance = pool.Get();
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.SetActive(true);

            // Determine lifetime
            float effectLifetime = lifetime > 0 ? lifetime : effectEntry.DefaultLifetime;

            // Track for auto-return
            if (effectLifetime > 0)
            {
                _activeEffects.Add(new ActiveEffect
                {
                    Instance = instance,
                    SpawnTime = Time.time,
                    Lifetime = effectLifetime,
                    EffectId = effectId
                });
            }

            // Trigger particle systems
            var particles = instance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Clear();
                ps.Play();
            }

            if (debugLogging)
                Debug.Log($"[EffectPoolManager] Spawned effect {effectId} at {position}");

            return instance;
        }

        /// <summary>
        /// Get effect by category index (convenience method).
        /// </summary>
        public GameObject GetMuzzleFlash(int index, Vector3 position, Quaternion rotation)
        {
            var entry = EffectPrefabRegistry.Instance?.GetMuzzleFlash(index);
            if (entry == null) return null;
            return GetEffect(entry.EffectId, position, rotation, entry.DefaultLifetime);
        }

        public GameObject GetShellEject(int index, Vector3 position, Quaternion rotation)
        {
            var entry = EffectPrefabRegistry.Instance?.GetShellEject(index);
            if (entry == null) return null;
            return GetEffect(entry.EffectId, position, rotation, entry.DefaultLifetime);
        }

        public GameObject GetTracer(int index, Vector3 position, Quaternion rotation)
        {
            var entry = EffectPrefabRegistry.Instance?.GetTracer(index);
            if (entry == null) return null;
            return GetEffect(entry.EffectId, position, rotation, entry.DefaultLifetime);
        }

        public GameObject GetImpactEffect(Audio.SurfaceMaterialType surfaceType, ImpactType impactType,
            Vector3 position, Quaternion rotation)
        {
            var entry = EffectPrefabRegistry.Instance?.GetSurfaceImpactEffect(surfaceType, impactType);
            if (entry == null) return null;
            return GetEffect(entry.EffectId, position, rotation, entry.DefaultLifetime);
        }

        /// <summary>
        /// Spawn an effect from a prefab directly (not using registry).
        /// Used by presentation systems with direct prefab references.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime = 0f)
        {
            if (prefab == null) return null;

            // Use prefab instance ID as a unique key for pooling
            int prefabId = prefab.GetInstanceID();

            // Get or create pool for this prefab
            if (!_pools.TryGetValue(prefabId, out var pool))
            {
                pool = CreatePool(prefabId, prefab);
                _pools[prefabId] = pool;
            }

            // Get instance from pool
            var instance = pool.Get();
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.SetActive(true);

            // Track for auto-return if lifetime specified
            if (lifetime > 0)
            {
                _activeEffects.Add(new ActiveEffect
                {
                    Instance = instance,
                    SpawnTime = Time.time,
                    Lifetime = lifetime,
                    EffectId = prefabId
                });
            }

            // Trigger particle systems
            var particles = instance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Clear();
                ps.Play();
            }

            if (debugLogging)
                Debug.Log($"[EffectPoolManager] Spawned prefab {prefab.name} at {position}");

            return instance;
        }

        /// <summary>
        /// Return an effect to its pool manually.
        /// </summary>
        public void ReturnToPool(int effectId, GameObject instance)
        {
            if (instance == null) return;

            // Stop particle systems
            var particles = instance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Stop();
                ps.Clear();
            }

            instance.SetActive(false);

            if (_pools.TryGetValue(effectId, out var pool))
            {
                pool.Release(instance);
            }
            else
            {
                // No pool exists, destroy
                Destroy(instance);
            }
        }

        private ObjectPool<GameObject> CreatePool(int effectId, GameObject prefab)
        {
            return new ObjectPool<GameObject>(
                createFunc: () => CreateEffectInstance(prefab),
                actionOnGet: OnGetEffect,
                actionOnRelease: OnReleaseEffect,
                actionOnDestroy: OnDestroyEffect,
                collectionCheck: false,
                defaultCapacity: defaultPoolSize,
                maxSize: maxPoolSize
            );
        }

        private GameObject CreateEffectInstance(GameObject prefab)
        {
            var instance = Instantiate(prefab, _poolParent);
            instance.SetActive(false);
            return instance;
        }

        private void OnGetEffect(GameObject obj)
        {
            // Handled in GetEffect
        }

        private void OnReleaseEffect(GameObject obj)
        {
            obj.SetActive(false);
            obj.transform.SetParent(_poolParent);
        }

        private void OnDestroyEffect(GameObject obj)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        private void CleanupInactivePools()
        {
            // Could implement pool shrinking here if needed
            if (debugLogging)
                Debug.Log($"[EffectPoolManager] Active effects: {_activeEffects.Count}, Pools: {_pools.Count}");
        }

        /// <summary>
        /// Pre-warm a pool with instances.
        /// </summary>
        public void PrewarmPool(int effectId, int count)
        {
            var registry = EffectPrefabRegistry.Instance;
            if (registry == null) return;

            var effectEntry = registry.GetEffect(effectId);
            if (effectEntry == null || effectEntry.Prefab == null) return;

            if (!_pools.TryGetValue(effectId, out var pool))
            {
                pool = CreatePool(effectId, effectEntry.Prefab);
                _pools[effectId] = pool;
            }

            // Get and return instances to warm the pool
            var instances = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                instances.Add(pool.Get());
            }
            foreach (var instance in instances)
            {
                pool.Release(instance);
            }

            if (debugLogging)
                Debug.Log($"[EffectPoolManager] Pre-warmed pool for effect {effectId} with {count} instances");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Clean up all pools
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            _pools.Clear();
        }
    }
}
