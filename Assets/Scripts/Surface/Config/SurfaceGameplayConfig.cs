using System.Collections.Generic;
using UnityEngine;
using Player.Components;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 2: Maps SurfaceID enum values to gameplay modifiers.
    /// Loaded from Resources. Designers tune this independently of SurfaceMaterial audio/VFX.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Surface/Surface Gameplay Config")]
    public class SurfaceGameplayConfig : ScriptableObject
    {
        [System.Serializable]
        public struct SurfaceGameplayEntry
        {
            public SurfaceID SurfaceId;

            [Header("Stealth")]
            [Tooltip("Multiplier on noise level. 1.0 = normal. >1 = louder. <1 = quieter.")]
            [Range(0f, 3f)]
            public float NoiseMultiplier;

            [Header("Movement")]
            [Tooltip("Multiplier on movement speed. 1.0 = normal. <1 = slower. >1 = faster.")]
            [Range(0.1f, 2f)]
            public float SpeedMultiplier;

            [Tooltip("Slip factor. 0 = full control. 1 = no control (pure ice).")]
            [Range(0f, 1f)]
            public float SlipFactor;

            [Header("Fall Damage")]
            [Tooltip("Multiplier on fall damage. 1.0 = normal. <1 = soft landing. >1 = hard landing.")]
            [Range(0f, 3f)]
            public float FallDamageMultiplier;

            [Header("Hazard")]
            [Tooltip("Damage per second when standing on this surface inside a SurfaceDamageZone. 0 = no damage.")]
            public float DamagePerSecond;

            [Tooltip("DamageType for surface DOT.")]
            public DamageType DamageType;
        }

        public List<SurfaceGameplayEntry> Entries = new();

        private Dictionary<SurfaceID, SurfaceGameplayEntry> _cache;

        public bool TryGetEntry(SurfaceID id, out SurfaceGameplayEntry entry)
        {
            if (_cache == null) RebuildCache();
            return _cache.TryGetValue(id, out entry);
        }

        public SurfaceGameplayEntry GetEntryOrDefault(SurfaceID id)
        {
            if (TryGetEntry(id, out var entry)) return entry;
            return DefaultEntry;
        }

        public static SurfaceGameplayEntry DefaultEntry => new()
        {
            SurfaceId = SurfaceID.Default,
            NoiseMultiplier = 1.0f,
            SpeedMultiplier = 1.0f,
            SlipFactor = 0f,
            FallDamageMultiplier = 1.0f,
            DamagePerSecond = 0f,
            DamageType = DamageType.Physical
        };

        private void OnEnable() => RebuildCache();
        private void OnValidate() => RebuildCache();

        private void RebuildCache()
        {
            _cache = new Dictionary<SurfaceID, SurfaceGameplayEntry>();
            foreach (var e in Entries)
                _cache[e.SurfaceId] = e;
        }
    }
}
