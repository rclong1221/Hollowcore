using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Weapons
{
    /// <summary>
    /// EPIC 15.5: Entity-based projectile pooling system.
    /// Eliminates GC spikes from projectile instantiation/destruction.
    /// Critical for automatic weapons and high fire-rate scenarios.
    /// </summary>
    public class ProjectilePoolManager : MonoBehaviour
    {
        public static ProjectilePoolManager Instance { get; private set; }

        [Header("Pool Settings")]
        [Tooltip("Initial pool size per projectile type")]
        [SerializeField] private int initialPoolSize = 20;

        [Tooltip("Maximum pool size per projectile type")]
        [SerializeField] private int maxPoolSize = 100;

        [Header("Performance")]
        [Tooltip("Maximum projectiles to spawn per frame")]
        [SerializeField] private int maxSpawnsPerFrame = 10;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Pool storage by projectile prefab index
        private Dictionary<int, Queue<Entity>> _entityPools = new Dictionary<int, Queue<Entity>>();
        private Dictionary<int, int> _poolSizes = new Dictionary<int, int>();

        // Prefab entity cache
        private Dictionary<int, Entity> _prefabEntities = new Dictionary<int, Entity>();

        // Spawn queue for rate limiting
        private struct SpawnRequest
        {
            public int PrefabIndex;
            public float3 Position;
            public quaternion Rotation;
            public float3 Velocity;
            public float Damage;
            public Entity Owner;
            public ProjectileType Type;
        }

        private Queue<SpawnRequest> _spawnQueue = new Queue<SpawnRequest>();
        private int _spawnsThisFrame;

        // World reference
        private World _clientWorld;
        private World _serverWorld;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _spawnsThisFrame = 0;

            // Process spawn queue with rate limiting
            while (_spawnQueue.Count > 0 && _spawnsThisFrame < maxSpawnsPerFrame)
            {
                var request = _spawnQueue.Dequeue();
                SpawnProjectileImmediate(request);
                _spawnsThisFrame++;
            }
        }

        /// <summary>
        /// Request a projectile spawn. May be queued if rate limit is reached.
        /// </summary>
        public void SpawnProjectile(
            int prefabIndex,
            float3 position,
            quaternion rotation,
            float3 velocity,
            float damage,
            Entity owner,
            ProjectileType type = ProjectileType.Bullet)
        {
            var request = new SpawnRequest
            {
                PrefabIndex = prefabIndex,
                Position = position,
                Rotation = rotation,
                Velocity = velocity,
                Damage = damage,
                Owner = owner,
                Type = type
            };

            if (_spawnsThisFrame < maxSpawnsPerFrame)
            {
                SpawnProjectileImmediate(request);
                _spawnsThisFrame++;
            }
            else
            {
                _spawnQueue.Enqueue(request);

                if (debugLogging && _spawnQueue.Count > maxSpawnsPerFrame * 2)
                {
                    Debug.LogWarning($"[ProjectilePool] Spawn queue backing up: {_spawnQueue.Count} pending");
                }
            }
        }

        /// <summary>
        /// Get a pooled projectile entity or create a new one.
        /// </summary>
        public Entity GetPooledEntity(int prefabIndex, World world)
        {
            if (!_entityPools.TryGetValue(prefabIndex, out var pool))
            {
                pool = new Queue<Entity>();
                _entityPools[prefabIndex] = pool;
                _poolSizes[prefabIndex] = 0;
            }

            // Try to get from pool
            while (pool.Count > 0)
            {
                var entity = pool.Dequeue();

                // Validate entity still exists
                if (world.EntityManager.Exists(entity))
                {
                    return entity;
                }
            }

            // Pool exhausted, need to create new
            return Entity.Null;
        }

        /// <summary>
        /// Return a projectile entity to the pool.
        /// </summary>
        public void ReturnToPool(int prefabIndex, Entity entity, World world)
        {
            if (!world.EntityManager.Exists(entity))
            {
                return;
            }

            if (!_entityPools.TryGetValue(prefabIndex, out var pool))
            {
                pool = new Queue<Entity>();
                _entityPools[prefabIndex] = pool;
            }

            // Disable and reset the entity
            ResetProjectileEntity(entity, world);

            // Return to pool if under max size
            if (!_poolSizes.TryGetValue(prefabIndex, out int currentSize))
            {
                currentSize = 0;
            }

            if (pool.Count < maxPoolSize)
            {
                pool.Enqueue(entity);
            }
            else
            {
                // Pool full, destroy entity
                world.EntityManager.DestroyEntity(entity);
            }
        }

        /// <summary>
        /// Pre-warm the pool with entities.
        /// </summary>
        public void PreWarmPool(int prefabIndex, Entity prefabEntity, World world, int count = -1)
        {
            if (count < 0) count = initialPoolSize;

            if (!_entityPools.ContainsKey(prefabIndex))
            {
                _entityPools[prefabIndex] = new Queue<Entity>();
                _poolSizes[prefabIndex] = 0;
            }

            _prefabEntities[prefabIndex] = prefabEntity;

            var pool = _entityPools[prefabIndex];
            var em = world.EntityManager;

            for (int i = 0; i < count && pool.Count < maxPoolSize; i++)
            {
                var entity = em.Instantiate(prefabEntity);
                ResetProjectileEntity(entity, world);
                pool.Enqueue(entity);
                _poolSizes[prefabIndex]++;
            }

            if (debugLogging)
            {
                Debug.Log($"[ProjectilePool] Pre-warmed {count} entities for prefab {prefabIndex}");
            }
        }

        /// <summary>
        /// Get pool statistics.
        /// </summary>
        public (int available, int total, int queued) GetPoolStats(int prefabIndex)
        {
            int available = 0;
            int total = 0;

            if (_entityPools.TryGetValue(prefabIndex, out var pool))
            {
                available = pool.Count;
            }

            if (_poolSizes.TryGetValue(prefabIndex, out int size))
            {
                total = size;
            }

            return (available, total, _spawnQueue.Count);
        }

        private void SpawnProjectileImmediate(SpawnRequest request)
        {
            // Find appropriate world
            var world = GetActiveWorld();
            if (world == null) return;

            var em = world.EntityManager;

            // Try to get pooled entity
            var entity = GetPooledEntity(request.PrefabIndex, world);

            if (entity == Entity.Null)
            {
                // Need to instantiate new
                if (_prefabEntities.TryGetValue(request.PrefabIndex, out var prefab) && em.Exists(prefab))
                {
                    entity = em.Instantiate(prefab);
                    
                    if (!_poolSizes.ContainsKey(request.PrefabIndex))
                    {
                        _poolSizes[request.PrefabIndex] = 0;
                    }
                    _poolSizes[request.PrefabIndex]++;
                }
                else
                {
                    if (debugLogging)
                    {
                        Debug.LogWarning($"[ProjectilePool] No prefab registered for index {request.PrefabIndex}");
                    }
                    return;
                }
            }

            // Configure the projectile
            ConfigureProjectileEntity(entity, world, request);
        }

        private void ConfigureProjectileEntity(Entity entity, World world, SpawnRequest request)
        {
            var em = world.EntityManager;

            // Set transform
            if (em.HasComponent<LocalTransform>(entity))
            {
                em.SetComponentData(entity, new LocalTransform
                {
                    Position = request.Position,
                    Rotation = request.Rotation,
                    Scale = 1f
                });
            }

            // Set projectile data
            if (em.HasComponent<Projectile>(entity))
            {
                var proj = em.GetComponentData<Projectile>(entity);
                proj.Damage = request.Damage;
                proj.Owner = request.Owner;
                proj.ElapsedTime = 0f;
                proj.Type = request.Type;
                em.SetComponentData(entity, proj);
            }

            // Set movement
            if (em.HasComponent<ProjectileMovement>(entity))
            {
                var move = em.GetComponentData<ProjectileMovement>(entity);
                move.Velocity = request.Velocity;
                em.SetComponentData(entity, move);
            }

            // Set impact
            if (em.HasComponent<ProjectileImpact>(entity))
            {
                var impact = em.GetComponentData<ProjectileImpact>(entity);
                impact.CurrentBounces = 0;
                em.SetComponentData(entity, impact);
            }

            // Remove impacted tag if present
            if (em.HasComponent<ProjectileImpacted>(entity))
            {
                em.RemoveComponent<ProjectileImpacted>(entity);
            }

            // Enable Simulate component for NetCode prediction
            if (em.HasComponent<Simulate>(entity))
            {
                em.SetComponentEnabled<Simulate>(entity, true);
            }
        }

        private void ResetProjectileEntity(Entity entity, World world)
        {
            var em = world.EntityManager;

            // Move far away and disable
            if (em.HasComponent<LocalTransform>(entity))
            {
                em.SetComponentData(entity, new LocalTransform
                {
                    Position = new float3(0, -10000, 0),
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
            }

            // Reset projectile state
            if (em.HasComponent<Projectile>(entity))
            {
                var proj = em.GetComponentData<Projectile>(entity);
                proj.ElapsedTime = 0f;
                proj.Owner = Entity.Null;
                em.SetComponentData(entity, proj);
            }

            // Stop movement
            if (em.HasComponent<ProjectileMovement>(entity))
            {
                var move = em.GetComponentData<ProjectileMovement>(entity);
                move.Velocity = float3.zero;
                em.SetComponentData(entity, move);
            }

            // Disable simulation
            if (em.HasComponent<Simulate>(entity))
            {
                em.SetComponentEnabled<Simulate>(entity, false);
            }
        }

        private World GetActiveWorld()
        {
            // Prefer client world for visual projectiles
            foreach (var world in World.All)
            {
                if (world.IsClient())
                {
                    return world;
                }
            }

            // Fall back to server
            foreach (var world in World.All)
            {
                if (world.IsServer())
                {
                    return world;
                }
            }

            return World.DefaultGameObjectInjectionWorld;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Cleanup pools
            _entityPools.Clear();
            _poolSizes.Clear();
            _prefabEntities.Clear();
            _spawnQueue.Clear();
        }

#if UNITY_EDITOR
        [Header("Editor Debug")]
        [SerializeField] private bool showPoolStats = true;

        private void OnGUI()
        {
            if (!showPoolStats || !debugLogging) return;

            GUILayout.BeginArea(new Rect(10, 300, 250, 200));
            GUILayout.Label("Projectile Pool Stats:");

            foreach (var kvp in _entityPools)
            {
                var stats = GetPoolStats(kvp.Key);
                GUILayout.Label($"  [{kvp.Key}] Available: {stats.available}/{stats.total}");
            }

            GUILayout.Label($"  Queued: {_spawnQueue.Count}");
            GUILayout.EndArea();
        }
#endif
    }
}
