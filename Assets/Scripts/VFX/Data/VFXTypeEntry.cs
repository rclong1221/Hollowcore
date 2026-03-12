using System;
using UnityEngine;
using DIG.Surface;

namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7: Defines a single VFX type in the registry.
    /// Referenced by VFXTypeId integer in VFXRequest.
    /// </summary>
    [Serializable]
    public struct VFXTypeEntry
    {
        /// <summary>Unique integer ID. Must be stable across sessions.</summary>
        public int TypeId;

        /// <summary>Human-readable name for debug/editor display.</summary>
        public string Name;

        /// <summary>The GameObject prefab with ParticleSystem(s) to spawn.</summary>
        public GameObject Prefab;

        /// <summary>Default category if not overridden by the request.</summary>
        public VFXCategory DefaultCategory;

        /// <summary>Minimum LOD tier at which this VFX is visible.</summary>
        public EffectLODTier MinimumLODTier;

        /// <summary>If true, VFXManager prewarms a pool for this prefab on startup.</summary>
        public bool Prewarm;

        /// <summary>Pool prewarm count.</summary>
        public int PrewarmCount;

        /// <summary>Maximum simultaneous instances. 0 = unlimited.</summary>
        public int MaxInstances;

        /// <summary>LOD-Reduced prefab variant. Null = use main prefab with emission reduction.</summary>
        public GameObject ReducedPrefab;

        /// <summary>LOD-Minimal prefab variant. Null = skip at Minimal tier.</summary>
        public GameObject MinimalPrefab;
    }
}
