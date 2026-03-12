using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DIG.Voxel.Systems.Meshing
{
    /// <summary>
    /// Static Object Pool for Chunk GameObjects.
    /// Eliminates GC allocation overhead from string names and GameObject instantiation.
    /// </summary>
    public static class ChunkMeshPool
    {
        private static Queue<GameObject> _pool = new Queue<GameObject>();
        private static int _totalCreated = 0;
        private static GameObject _poolContainer;
        private static bool _isShuttingDown = false;

        /// <summary>
        /// Get a pooled GameObject.
        /// </summary>
        public static GameObject Get(Vector3 position, Material sharedMaterial, bool shadowCasting)
        {
            if (_isShuttingDown) return null;
            
            GameObject go;
            if (_pool.Count > 0)
            {
                go = _pool.Dequeue();
                if (go == null) // Handle destroyed externally
                {
                    return Get(position, sharedMaterial, shadowCasting);
                }
            }
            else
            {
                go = CreateNewGameObject();
            }

            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.SetActive(true);
            
            // Reset Components
            var mf = go.GetComponent<MeshFilter>(); // Assumed to exist
            var mr = go.GetComponent<MeshRenderer>();
            
            mr.sharedMaterial = sharedMaterial;
            mr.shadowCastingMode = shadowCasting ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;

            return go;
        }

        /// <summary>
        /// Return a GameObject to the pool.
        /// </summary>
        public static void Return(GameObject go)
        {
            if (go == null) return;
            
            // During shutdown, just destroy immediately instead of pooling
            if (_isShuttingDown)
            {
                Object.DestroyImmediate(go);
                return;
            }
            
            // Optimization: Don't Destroy mesh here, just clear reference.
            // The mesh should be destroyed by the caller if it's unique.
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = null; 

            go.SetActive(false);
            
            if (_poolContainer == null)
            {
                _poolContainer = new GameObject("Chunk_Pool_Container");
            }
            go.transform.SetParent(_poolContainer.transform);
            
            _pool.Enqueue(go);
        }

        private static GameObject CreateNewGameObject()
        {
            _totalCreated++;
            var go = new GameObject("Chunk_Pooled");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            // FIX: Add MeshCollider for Unity Physics (ragdoll collision with terrain)
            // This is separate from ECS Unity.Physics.MeshCollider used for character controller
            go.AddComponent<MeshCollider>();
            return go;
        }
        
        public static void Clear()
        {
            // Set shutdown flag FIRST to prevent recreation
            _isShuttingDown = true;
            
            // Use DestroyImmediate during cleanup since Object.Destroy is deferred
            // and may not work during scene teardown
            while (_pool.Count > 0)
            {
                var go = _pool.Dequeue();
                if (go != null)
                {
                    go.transform.SetParent(null); // Unparent before destroying
                    Object.DestroyImmediate(go);
                }
            }
            if (_poolContainer != null)
            {
                Object.DestroyImmediate(_poolContainer);
                _poolContainer = null;
            }
        }
        
        /// <summary>
        /// Reset the pool state for domain reload (Editor play mode restart).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pool = new Queue<GameObject>();
            _totalCreated = 0;
            _poolContainer = null;
            _isShuttingDown = false;
        }
    }
}
