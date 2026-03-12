using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Registry of all permanent points of interest for compass, world map,
    /// fog-of-war discovery, and fast travel.
    /// Load from Resources/POIRegistry by MinimapBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Map/POI Registry")]
    public class POIRegistrySO : ScriptableObject
    {
        [Tooltip("All permanent points of interest")]
        public POIDefinition[] POIs = Array.Empty<POIDefinition>();

        [Tooltip("Distance at which POIs are auto-discovered")]
        public float AutoDiscoverRadius = 30f;

        [Tooltip("Exploration XP per discovery (ties to EPIC 16.14 XPGrantAPI)")]
        public float DiscoverXPReward = 25f;

        // O(1) lookup cache, built on first access
        private Dictionary<int, POIDefinition> _cache;

        /// <summary>Find POI definition by stable ID. Returns null if not found.</summary>
        public POIDefinition? GetPOI(int poiId)
        {
            if (_cache == null) RebuildCache();
            return _cache.TryGetValue(poiId, out var poi) ? poi : (POIDefinition?)null;
        }

        private void RebuildCache()
        {
            _cache = new Dictionary<int, POIDefinition>(POIs.Length);
            for (int i = 0; i < POIs.Length; i++)
                _cache[POIs[i].POIId] = POIs[i];
        }

        private void OnValidate()
        {
            _cache = null; // Invalidate cache when POIs change in inspector
        }
    }

    [Serializable]
    public struct POIDefinition
    {
        [Tooltip("Stable unique ID (never changes)")]
        public int POIId;
        public string Label;
        public POIType Type;
        [Tooltip("Used for compass + world map (authoring may override)")]
        public Vector3 WorldPosition;
        public bool IsFastTravel;
        [Tooltip("Must be visited before showing on world map")]
        public bool RequiresDiscovery;
        [TextArea]
        public string Description;
        [Tooltip("null = use theme default for POIType")]
        public Sprite OverrideIcon;
    }
}
