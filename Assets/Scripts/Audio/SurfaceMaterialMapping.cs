using System.Collections.Generic;
using UnityEngine;
using Audio.Systems;

namespace Audio.Systems
{
    /// <summary>
    /// Maps material names, tags, and layers to SurfaceMaterial assets.
    /// Used by SurfaceDetectionService for priority-based surface lookup.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Audio/Surface Material Mapping", fileName = "SurfaceMaterialMapping")]
    public class SurfaceMaterialMapping : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string materialName;
            public string tag;
            public int layer = -1; // -1 = not set
            public SurfaceMaterial surfaceMaterial;
        }

        public List<Entry> mappings = new List<Entry>();
        
        // Runtime caches for O(1) lookups
        private Dictionary<string, Entry> _materialNameCache;
        private Dictionary<string, Entry> _tagCache;
        private Dictionary<int, Entry> _layerCache;
        
        private void OnEnable() => RebuildCaches();
        private void OnValidate() => RebuildCaches();
        
        private void RebuildCaches()
        {
            _materialNameCache = new Dictionary<string, Entry>();
            _tagCache = new Dictionary<string, Entry>();
            _layerCache = new Dictionary<int, Entry>();
            
            foreach (var entry in mappings)
            {
                if (entry == null) continue;
                
                if (!string.IsNullOrEmpty(entry.materialName))
                {
                    _materialNameCache[entry.materialName] = entry;
                }
                if (!string.IsNullOrEmpty(entry.tag))
                {
                    _tagCache[entry.tag] = entry;
                }
                if (entry.layer >= 0)
                {
                    _layerCache[entry.layer] = entry;
                }
            }
        }
        
        public Entry FindByMaterialName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_materialNameCache == null) RebuildCaches();
            _materialNameCache.TryGetValue(name, out var entry);
            return entry;
        }
        
        public Entry FindByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            if (_tagCache == null) RebuildCaches();
            _tagCache.TryGetValue(tag, out var entry);
            return entry;
        }
        
        public Entry FindByLayer(int layer)
        {
            if (layer < 0) return null;
            if (_layerCache == null) RebuildCaches();
            _layerCache.TryGetValue(layer, out var entry);
            return entry;
        }
    }
}
