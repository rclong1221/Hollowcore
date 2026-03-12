using System.Collections.Generic;
using UnityEngine;

namespace Audio.Systems
{
    [CreateAssetMenu(menuName = "DIG/SurfaceMaterialRegistry", fileName = "SurfaceMaterialRegistry")]
    public class SurfaceMaterialRegistry : ScriptableObject
    {
        public SurfaceMaterial DefaultMaterial;

        public List<SurfaceMaterial> Materials = new List<SurfaceMaterial>();

        // Runtime cache for fast id -> SurfaceMaterial lookup to avoid linear string/id scans in hot loops.
        // Rebuilt on OnEnable / OnValidate so runtime callers can rely on O(1) lookups.
        private Dictionary<int, SurfaceMaterial> _idCache = new Dictionary<int, SurfaceMaterial>();

        /// <summary>
        /// Resolve id -> SurfaceMaterial, or DefaultMaterial when not found.
        /// Runtime callers should cache results where possible.
        /// </summary>
        public SurfaceMaterial GetById(int id)
        {
            if (TryGetById(id, out var mat)) return mat;
            return DefaultMaterial;
        }

        /// <summary>
        /// Fast, allocation-free attempt to resolve id -> SurfaceMaterial.
        /// Returns true when found in the registry cache.
        /// </summary>
        public bool TryGetById(int id, out SurfaceMaterial material)
        {
            if (_idCache != null && _idCache.Count > 0)
            {
                return _idCache.TryGetValue(id, out material);
            }
            // Fallback: rebuild cache on-the-fly if somehow empty
            RebuildCache();
            return _idCache.TryGetValue(id, out material);
        }

        /// <summary>
        /// Helper to find id by display name (editor only convenience)
        /// </summary>
        public int FindIdByName(string name)
        {
            var m = Materials.Find(x => x != null && x.DisplayName == name);
            return m != null ? m.Id : (DefaultMaterial != null ? DefaultMaterial.Id : -1);
        }

        private void OnEnable()
        {
            RebuildCache();
        }

        private void OnValidate()
        {
            RebuildCache();
        }

        private void RebuildCache()
        {
            _idCache.Clear();
            for (int i = 0; i < Materials.Count; ++i)
            {
                var m = Materials[i];
                if (m == null) continue;
                // If duplicate IDs are present, the later entry wins.
                _idCache[m.Id] = m;
            }
#if UNITY_EDITOR
            // Ensure DefaultMaterial exists in cache for its id.
            if (DefaultMaterial != null)
            {
                _idCache[DefaultMaterial.Id] = DefaultMaterial;
            }
#endif
        }
    }
}
