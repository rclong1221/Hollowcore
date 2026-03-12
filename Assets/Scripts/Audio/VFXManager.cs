using System.Collections.Generic;
using UnityEngine;

namespace Audio.Systems
{
    /// <summary>
    /// VFX pooling manager for particle prefabs with surface-aware playback.
    /// Supports both direct prefab spawning and material-id-based VFX resolution.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Pool Settings")]
        public int DefaultPoolSize = 8;

        [Header("Throttling")]
        [Tooltip("Max VFX spawns per second (0 = unlimited)")]
        public float MaxSpawnsPerSecond = 30f;

        [Tooltip("Minimum distance for non-local VFX spawning")]
        public float DistanceCulling = 50f;

        [Header("Debug")]
        public bool EnableDebugLogging = false;

        private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new Dictionary<GameObject, Queue<GameObject>>();
        private SurfaceMaterialRegistry _registry;
        private float _lastSpawnTime;
        private int _frameSpawnCount;
        private int _lastSpawnFrame;

        // Telemetry
        public int TotalSpawnsThisSession { get; private set; }
        public int PoolHitsThisSession { get; private set; }
        public int CulledThisSession { get; private set; }

        void Awake()
        {
            Instance = this;
            _registry = Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Play VFX for a given surface material ID at a position.
        /// </summary>
        public void PlayVFXForMaterial(int materialId, Vector3 position, Quaternion rotation = default)
        {
            if (_registry == null)
            {
                if (EnableDebugLogging) Debug.LogWarning("[VFXManager] No registry loaded");
                return;
            }

            if (!_registry.TryGetById(materialId, out var surfaceMat) || surfaceMat.VFXPrefab == null)
            {
                if (EnableDebugLogging) Debug.Log($"[VFXManager] No VFX for material ID {materialId}");
                return;
            }

            SpawnVFX(surfaceMat.VFXPrefab, position, rotation == default ? Quaternion.identity : rotation);
        }

        /// <summary>
        /// Play VFX by prefab at a position (legacy/direct API).
        /// </summary>
        public void PlayVFX(GameObject prefab, Vector3 position)
        {
            SpawnVFX(prefab, position, Quaternion.identity);
        }

        /// <summary>
        /// Play VFX by prefab with rotation.
        /// </summary>
        public void PlayVFX(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            SpawnVFX(prefab, position, rotation);
        }

        /// <summary>
        /// Core spawn method with pooling, throttling, and culling.
        /// </summary>
        public GameObject SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation, bool bypassThrottle = false)
        {
            if (prefab == null) return null;

            // Throttle check
            if (!bypassThrottle && MaxSpawnsPerSecond > 0)
            {
                // Reset frame counter
                if (Time.frameCount != _lastSpawnFrame)
                {
                    _frameSpawnCount = 0;
                    _lastSpawnFrame = Time.frameCount;
                }

                float minInterval = 1f / MaxSpawnsPerSecond;
                if (Time.time - _lastSpawnTime < minInterval)
                {
                    if (EnableDebugLogging) Debug.Log("[VFXManager] Spawn throttled");
                    return null;
                }
            }

            // Distance culling (relative to main camera)
            if (DistanceCulling > 0 && Camera.main != null)
            {
                float dist = Vector3.Distance(Camera.main.transform.position, position);
                if (dist > DistanceCulling)
                {
                    CulledThisSession++;
                    if (EnableDebugLogging) Debug.Log($"[VFXManager] VFX culled at distance {dist:F1}m");
                    return null;
                }
            }

            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }

            GameObject go = null;
            while (queue.Count > 0)
            {
                var candidate = queue.Dequeue();
                if (candidate != null)
                {
                    go = candidate;
                    PoolHitsThisSession++;
                    break;
                }
            }

            if (go == null)
            {
                go = Instantiate(prefab, position, rotation, transform);
            }
            else
            {
                go.transform.SetParent(transform, false);
                go.transform.position = position;
                go.transform.rotation = rotation;
                go.SetActive(true);
            }

            // Telemetry
            TotalSpawnsThisSession++;
            _lastSpawnTime = Time.time;
            _frameSpawnCount++;

            // Determine duration from ParticleSystem if present
            var ps = go.GetComponentInChildren<ParticleSystem>();
            float duration = 1f;
            if (ps != null)
            {
                duration = ps.main.duration + ps.main.startLifetime.constantMax;
                ps.Play();
            }

            StartCoroutine(ReturnAfter(go, prefab, duration));
            return go;
        }

        System.Collections.IEnumerator ReturnAfter(GameObject go, GameObject prefab, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go == null) yield break;

            // Stop particle systems before pooling
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            go.SetActive(false);
            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }
            queue.Enqueue(go);
        }

        /// <summary>
        /// Prewarm pools for a prefab.
        /// </summary>
        public void PrewarmPool(GameObject prefab, int count)
        {
            if (prefab == null) return;

            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
                go.SetActive(false);
                queue.Enqueue(go);
            }

            if (EnableDebugLogging)
                Debug.Log($"[VFXManager] Prewarmed {count} instances of {prefab.name}");
        }

        /// <summary>
        /// Get current pool sizes for debugging.
        /// </summary>
        public Dictionary<string, int> GetPoolStats()
        {
            var stats = new Dictionary<string, int>();
            foreach (var kvp in _pools)
            {
                if (kvp.Key != null)
                    stats[kvp.Key.name] = kvp.Value.Count;
            }
            return stats;
        }
    }
}
