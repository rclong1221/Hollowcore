using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Per-icon-type visual configuration for minimap, world map, and compass.
    /// Load from Resources/MapIconTheme by MinimapBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Map/Icon Theme")]
    public class MapIconThemeSO : ScriptableObject
    {
        [Tooltip("Visual config per MapIconType")]
        public MapIconThemeEntry[] Entries = Array.Empty<MapIconThemeEntry>();

        // O(1) lookup cache, built on first access
        private Dictionary<MapIconType, MapIconThemeEntry> _cache;

        /// <summary>Resolve icon type to theme entry. Returns null if not found.</summary>
        public MapIconThemeEntry? GetEntry(MapIconType type)
        {
            if (_cache == null) RebuildCache();
            return _cache.TryGetValue(type, out var entry) ? entry : (MapIconThemeEntry?)null;
        }

        private void RebuildCache()
        {
            _cache = new Dictionary<MapIconType, MapIconThemeEntry>(Entries.Length);
            for (int i = 0; i < Entries.Length; i++)
                _cache[Entries[i].IconType] = Entries[i];
        }

        private void OnValidate()
        {
            _cache = null; // Invalidate cache when entries change in inspector
        }
    }

    [Serializable]
    public struct MapIconThemeEntry
    {
        public MapIconType IconType;
        [Tooltip("Minimap + world map sprite")]
        public Sprite Icon;
        [Tooltip("Compass variant (may be smaller)")]
        public Sprite CompassIcon;
        public Color DefaultColor;
        [Tooltip("Per-type scale (Boss=1.5, Loot=0.8)")]
        public float ScaleMultiplier;
        public bool ShowOnCompass;
        public bool ShowOnWorldMap;
        public bool ShowDistance;
        [Tooltip("Z-order for overlap (higher = on top)")]
        public int SortOrder;
    }
}
