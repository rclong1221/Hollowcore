using System.Collections.Generic;
using UnityEngine;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// Object pool for decorator GameObjects.
    /// Eliminates costly Object.Instantiate() calls by reusing pooled instances.
    /// 
    /// OPTIMIZATION 10.5.9: Pre-instantiate decorator prefabs and reuse them.
    /// </summary>
    public class DecoratorPool : MonoBehaviour
    {
        private static DecoratorPool _instance;
        public static DecoratorPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("DecoratorPool");
                    _instance = go.AddComponent<DecoratorPool>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        // Pool per decorator ID
        private Dictionary<byte, Stack<GameObject>> _pools = new Dictionary<byte, Stack<GameObject>>();
        
        // Track active decorators per chunk for cleanup
        private Dictionary<Unity.Mathematics.int3, List<PooledDecorator>> _activePerChunk = 
            new Dictionary<Unity.Mathematics.int3, List<PooledDecorator>>();
        
        // Prefab references
        private Dictionary<byte, GameObject> _prefabs = new Dictionary<byte, GameObject>();
        
        // Pool settings
        private const int INITIAL_POOL_SIZE = 10;
        private const int MAX_POOL_SIZE = 100;
        
        /// <summary>
        /// Register a decorator prefab for pooling.
        /// </summary>
        public void RegisterPrefab(byte decoratorID, GameObject prefab)
        {
            if (prefab == null) return;
            
            _prefabs[decoratorID] = prefab;
            
            if (!_pools.ContainsKey(decoratorID))
            {
                _pools[decoratorID] = new Stack<GameObject>(INITIAL_POOL_SIZE);
            }
        }
        
        /// <summary>
        /// Pre-warm the pool with instances.
        /// </summary>
        public void PreWarm(byte decoratorID, int count)
        {
            if (!_prefabs.TryGetValue(decoratorID, out var prefab)) return;
            
            if (!_pools.TryGetValue(decoratorID, out var pool))
            {
                pool = new Stack<GameObject>(count);
                _pools[decoratorID] = pool;
            }
            
            for (int i = 0; i < count && pool.Count < MAX_POOL_SIZE; i++)
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                pool.Push(go);
            }
        }
        
        /// <summary>
        /// Get a decorator instance from the pool.
        /// </summary>
        public GameObject Get(byte decoratorID, Unity.Mathematics.int3 chunkPos, 
            Vector3 position, Quaternion rotation, float scale)
        {
            GameObject instance;
            
            if (_pools.TryGetValue(decoratorID, out var pool) && pool.Count > 0)
            {
                instance = pool.Pop();
            }
            else if (_prefabs.TryGetValue(decoratorID, out var prefab))
            {
                instance = Instantiate(prefab);
            }
            else
            {
                return null;
            }
            
            // Configure instance
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * scale;
            instance.SetActive(true);
            
            // Track for cleanup
            var tracker = instance.GetComponent<PooledDecorator>();
            if (tracker == null)
            {
                tracker = instance.AddComponent<PooledDecorator>();
            }
            tracker.DecoratorID = decoratorID;
            tracker.ChunkPos = chunkPos;
            
            if (!_activePerChunk.TryGetValue(chunkPos, out var activeList))
            {
                activeList = new List<PooledDecorator>(16);
                _activePerChunk[chunkPos] = activeList;
            }
            activeList.Add(tracker);
            
            return instance;
        }
        
        /// <summary>
        /// Return a decorator to the pool.
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            
            var tracker = instance.GetComponent<PooledDecorator>();
            if (tracker == null) 
            {
                Destroy(instance);
                return;
            }
            
            byte id = tracker.DecoratorID;
            
            // Remove from active tracking
            if (_activePerChunk.TryGetValue(tracker.ChunkPos, out var activeList))
            {
                activeList.Remove(tracker);
            }
            
            // Return to pool or destroy
            if (_pools.TryGetValue(id, out var pool) && pool.Count < MAX_POOL_SIZE)
            {
                instance.SetActive(false);
                instance.transform.SetParent(transform);
                pool.Push(instance);
            }
            else
            {
                Destroy(instance);
            }
        }
        
        /// <summary>
        /// Return all decorators for a chunk to the pool.
        /// Call when chunk is unloaded.
        /// </summary>
        public void ReturnChunk(Unity.Mathematics.int3 chunkPos)
        {
            if (!_activePerChunk.TryGetValue(chunkPos, out var activeList))
                return;
            
            // Return all decorators for this chunk
            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                var tracker = activeList[i];
                if (tracker != null && tracker.gameObject != null)
                {
                    byte id = tracker.DecoratorID;
                    
                    if (_pools.TryGetValue(id, out var pool) && pool.Count < MAX_POOL_SIZE)
                    {
                        tracker.gameObject.SetActive(false);
                        tracker.transform.SetParent(transform);
                        pool.Push(tracker.gameObject);
                    }
                    else
                    {
                        Destroy(tracker.gameObject);
                    }
                }
            }
            
            activeList.Clear();
            _activePerChunk.Remove(chunkPos);
        }
        
        /// <summary>
        /// Clear all pools. Call on scene unload.
        /// </summary>
        public void ClearAll()
        {
            // Destroy all pooled objects
            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    var go = pool.Pop();
                    if (go != null) Destroy(go);
                }
            }
            _pools.Clear();
            
            // Clear active tracking
            _activePerChunk.Clear();
        }
        
        /// <summary>
        /// Get pool statistics.
        /// </summary>
        public (int pooled, int active) GetStats()
        {
            int pooled = 0;
            int active = 0;
            
            foreach (var pool in _pools.Values)
                pooled += pool.Count;
            
            foreach (var list in _activePerChunk.Values)
                active += list.Count;
            
            return (pooled, active);
        }
        
        private void OnDestroy()
        {
            ClearAll();
            _instance = null;
        }
    }
    
    /// <summary>
    /// Component attached to pooled decorator instances for tracking.
    /// </summary>
    public class PooledDecorator : MonoBehaviour
    {
        public byte DecoratorID;
        public Unity.Mathematics.int3 ChunkPos;
    }
}
