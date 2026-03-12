using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Critical optimization: Pools NativeLists and NativeArrays to prevent allocation churn and GC spikes.
    /// Used primarily by ChunkMeshingSystem.
    /// 
    /// Thread Safety: Uses reference counting for multi-world support (Client/Server).
    /// </summary>
    public static class NativeCollectionPool
    {
        // Central registry for cleanup
        private static List<Action> _disposeActions = new List<Action>();
        
        // Reference counting for multi-world support (e.g., NetCode Client + Server)
        private static int _userCount = 0;
        private static readonly object _lock = new object();
        
        // Stats tracking
        private static int _activeAllocations = 0;
        private static int _pooledAllocations = 0;
        private static long _totalBytesAllocated = 0;

        public static string GetStats()
        {
            return $"Active: {_activeAllocations}, Pooled: {_pooledAllocations}, Total Bytes: {_totalBytesAllocated}, Users: {_userCount}";
        }
        
        /// <summary>
        /// Call this in OnCreate of systems that use the pool.
        /// </summary>
        public static void RegisterUser()
        {
            lock (_lock)
            {
                _userCount++;
            }
        }
        
        /// <summary>
        /// Call this in OnDestroy of systems that use the pool.
        /// Disposes the pool only when the last user unregisters.
        /// </summary>
        public static void UnregisterUser()
        {
            lock (_lock)
            {
                _userCount--;
                if (_userCount <= 0)
                {
                    _userCount = 0;
                    DisposeAllInternal();
                }
            }
        }

        public static NativeList<T> GetList<T>(int capacity) where T : unmanaged
        {
            return NativeListPool<T>.Get(capacity);
        }

        public static void ReturnList<T>(NativeList<T> list) where T : unmanaged
        {
            NativeListPool<T>.Return(list);
        }

        public static NativeArray<T> GetArray<T>(int length) where T : unmanaged
        {
            return NativeArrayPool<T>.Get(length);
        }

        public static void ReturnArray<T>(NativeArray<T> array) where T : unmanaged
        {
            NativeArrayPool<T>.Return(array);
        }

        /// <summary>
        /// Force dispose all pooled collections. Use UnregisterUser() instead for proper ref counting.
        /// </summary>
        public static void DisposeAll()
        {
            lock (_lock)
            {
                DisposeAllInternal();
            }
        }
        
        private static void DisposeAllInternal()
        {
            foreach (var action in _disposeActions)
            {
                try { action.Invoke(); } catch { }
            }
            _disposeActions.Clear();
            
            // Clear static pools (re-registering happens on next access)
            NativeListPoolClear();
            NativeArrayPoolClear();
            
            _activeAllocations = 0;
            _pooledAllocations = 0;
            _totalBytesAllocated = 0;
        }
        
        // Hooks to clear static generic fields if needed (simulated via actions for now)
        private static void NativeListPoolClear() { } 
        private static void NativeArrayPoolClear() { }

        public static void RegisterDispose(Action action)
        {
            _disposeActions.Add(action);
        }


        // --- Generic Pools ---

        private static class NativeListPool<T> where T : unmanaged
        {
            private static Stack<NativeList<T>> _pool = new Stack<NativeList<T>>();
            private static bool _registered = false;

            public static NativeList<T> Get(int capacity)
            {
                if (!_registered)
                {
                    NativeCollectionPool.RegisterDispose(Dispose);
                    _registered = true;
                }

                if (_pool.Count > 0)
                {
                    var list = _pool.Pop();
                    list.Clear();
                    if (list.Capacity < capacity) list.Capacity = capacity;
                    return list;
                }

                return new NativeList<T>(capacity, Allocator.Persistent);
            }

            public static void Return(NativeList<T> list)
            {
                if (!list.IsCreated) return;
                list.Clear();
                _pool.Push(list);
            }

            public static void Dispose()
            {
                while (_pool.Count > 0)
                {
                    var list = _pool.Pop();
                    if (list.IsCreated) list.Dispose();
                }
                _registered = false;
            }
        }

        private static class NativeArrayPool<T> where T : unmanaged
        {
            // Dictionary Key: Length. Value: Stack of arrays
            private static Dictionary<int, Stack<NativeArray<T>>> _pools = new Dictionary<int, Stack<NativeArray<T>>>();
            private static bool _registered = false;

            public static NativeArray<T> Get(int length)
            {
                if (!_registered)
                {
                    NativeCollectionPool.RegisterDispose(Dispose);
                    _registered = true;
                }

                if (!_pools.ContainsKey(length))
                {
                    _pools[length] = new Stack<NativeArray<T>>();
                }

                var stack = _pools[length];
                if (stack.Count > 0)
                {
                    var array = stack.Pop();
                    // NativeArray content is NOT cleared automatically, caller must handle if needed.
                    // But for Voxel Meshing, we overwrite anyway.
                    return array;
                }

                return new NativeArray<T>(length, Allocator.Persistent);
            }

            public static void Return(NativeArray<T> array)
            {
                if (!array.IsCreated) return;
                
                int length = array.Length;
                if (!_pools.ContainsKey(length)) _pools[length] = new Stack<NativeArray<T>>();
                
                _pools[length].Push(array);
            }

            public static void Dispose()
            {
                foreach (var stack in _pools.Values)
                {
                    while (stack.Count > 0)
                    {
                        var array = stack.Pop();
                        if (array.IsCreated) array.Dispose();
                    }
                }
                _pools.Clear();
                _registered = false;
            }
        }
    }
}
