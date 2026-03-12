using UnityEngine;
using System.Collections.Generic;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Central registry for weapon effect prefabs.
    /// Maps effect IDs to actual prefab references for spawning.
    /// Used by FireEffectSpawnerSystem and ImpactEffectSpawnerSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectPrefabRegistry", menuName = "DIG/Weapons/Effect Prefab Registry")]
    public class EffectPrefabRegistry : ScriptableObject
    {
        public static EffectPrefabRegistry Instance { get; private set; }

        [System.Serializable]
        public class EffectEntry
        {
            [Tooltip("Unique ID for this effect")]
            public int EffectId;

            [Tooltip("Display name for editor")]
            public string Name;

            [Tooltip("Effect category")]
            public EffectCategory Category;

            [Tooltip("The prefab to instantiate")]
            public GameObject Prefab;

            [Tooltip("Default lifetime in seconds (0 = use prefab settings)")]
            public float DefaultLifetime = 1f;

            [Tooltip("Whether to pool this effect")]
            public bool UsePooling = true;
        }

        [Header("Muzzle Flash Effects")]
        [SerializeField] private List<EffectEntry> muzzleFlashEffects = new List<EffectEntry>();

        [Header("Shell Ejection Effects")]
        [SerializeField] private List<EffectEntry> shellEjectEffects = new List<EffectEntry>();

        [Header("Tracer Effects")]
        [SerializeField] private List<EffectEntry> tracerEffects = new List<EffectEntry>();

        [Header("Impact Effects")]
        [SerializeField] private List<EffectEntry> impactEffects = new List<EffectEntry>();

        [Header("Decal Effects")]
        [SerializeField] private List<EffectEntry> decalEffects = new List<EffectEntry>();

        [Header("Explosion Effects")]
        [SerializeField] private List<EffectEntry> explosionEffects = new List<EffectEntry>();

        [Header("Surface-Specific Impacts")]
        [SerializeField] private List<SurfaceImpactEntry> surfaceImpacts = new List<SurfaceImpactEntry>();

        // Runtime lookup tables
        private Dictionary<int, EffectEntry> _effectLookup;
        private Dictionary<(Audio.SurfaceMaterialType, ImpactType), EffectEntry> _surfaceImpactLookup;

        public void Initialize()
        {
            Instance = this;
            BuildLookupTables();
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Initialize();
            }
        }

        private void BuildLookupTables()
        {
            _effectLookup = new Dictionary<int, EffectEntry>();
            _surfaceImpactLookup = new Dictionary<(Audio.SurfaceMaterialType, ImpactType), EffectEntry>();

            // Add all effects to lookup
            AddEffectsToLookup(muzzleFlashEffects);
            AddEffectsToLookup(shellEjectEffects);
            AddEffectsToLookup(tracerEffects);
            AddEffectsToLookup(impactEffects);
            AddEffectsToLookup(decalEffects);
            AddEffectsToLookup(explosionEffects);

            // Build surface impact lookup
            foreach (var entry in surfaceImpacts)
            {
                var key = (entry.SurfaceType, entry.ImpactType);
                if (!_surfaceImpactLookup.ContainsKey(key))
                {
                    _surfaceImpactLookup[key] = entry.Effect;
                }
            }
        }

        private void AddEffectsToLookup(List<EffectEntry> effects)
        {
            foreach (var effect in effects)
            {
                if (!_effectLookup.ContainsKey(effect.EffectId))
                {
                    _effectLookup[effect.EffectId] = effect;
                }
                else
                {
                    Debug.LogWarning($"[EffectPrefabRegistry] Duplicate effect ID: {effect.EffectId} ({effect.Name})");
                }
            }
        }

        /// <summary>
        /// Get effect entry by ID.
        /// </summary>
        public EffectEntry GetEffect(int effectId)
        {
            if (_effectLookup == null)
                BuildLookupTables();

            _effectLookup.TryGetValue(effectId, out var entry);
            return entry;
        }

        /// <summary>
        /// Get prefab by effect ID.
        /// </summary>
        public GameObject GetPrefab(int effectId)
        {
            return GetEffect(effectId)?.Prefab;
        }

        /// <summary>
        /// Get impact effect for a specific surface and impact type.
        /// </summary>
        public EffectEntry GetSurfaceImpactEffect(Audio.SurfaceMaterialType surfaceType, ImpactType impactType)
        {
            if (_surfaceImpactLookup == null)
                BuildLookupTables();

            // Try specific combo
            var key = (surfaceType, impactType);
            if (_surfaceImpactLookup.TryGetValue(key, out var entry))
                return entry;

            // Fallback to default surface
            key = (Audio.SurfaceMaterialType.Default, impactType);
            if (_surfaceImpactLookup.TryGetValue(key, out entry))
                return entry;

            return null;
        }

        /// <summary>
        /// Get muzzle flash effect by index (for quick access).
        /// </summary>
        public EffectEntry GetMuzzleFlash(int index)
        {
            if (index >= 0 && index < muzzleFlashEffects.Count)
                return muzzleFlashEffects[index];
            return null;
        }

        /// <summary>
        /// Get shell eject effect by index.
        /// </summary>
        public EffectEntry GetShellEject(int index)
        {
            if (index >= 0 && index < shellEjectEffects.Count)
                return shellEjectEffects[index];
            return null;
        }

        /// <summary>
        /// Get tracer effect by index.
        /// </summary>
        public EffectEntry GetTracer(int index)
        {
            if (index >= 0 && index < tracerEffects.Count)
                return tracerEffects[index];
            return null;
        }

        /// <summary>
        /// Get decal effect by index.
        /// </summary>
        public EffectEntry GetDecal(int index)
        {
            if (index >= 0 && index < decalEffects.Count)
                return decalEffects[index];
            return null;
        }

        #region Effect ID Constants
        // Predefined effect IDs for common effects
        public static class EffectIds
        {
            // Muzzle flashes (0-99)
            public const int MuzzleFlash_Pistol = 0;
            public const int MuzzleFlash_Rifle = 1;
            public const int MuzzleFlash_Shotgun = 2;
            public const int MuzzleFlash_SMG = 3;
            public const int MuzzleFlash_Sniper = 4;
            public const int MuzzleFlash_Suppressed = 5;

            // Shell ejection (100-199)
            public const int Shell_Pistol = 100;
            public const int Shell_Rifle = 101;
            public const int Shell_Shotgun = 102;

            // Tracers (200-299)
            public const int Tracer_Standard = 200;
            public const int Tracer_Green = 201;
            public const int Tracer_Red = 202;

            // Impacts (300-399)
            public const int Impact_Default = 300;
            public const int Impact_Concrete = 301;
            public const int Impact_Metal = 302;
            public const int Impact_Wood = 303;
            public const int Impact_Dirt = 304;
            public const int Impact_Water = 305;
            public const int Impact_Flesh = 306;
            public const int Impact_Glass = 307;

            // Decals (400-499)
            public const int Decal_BulletHole = 400;
            public const int Decal_BulletHole_Metal = 401;
            public const int Decal_BulletHole_Wood = 402;
            public const int Decal_Blood = 403;
            public const int Decal_Scorch = 404;

            // Explosions (500-599)
            public const int Explosion_Small = 500;
            public const int Explosion_Medium = 501;
            public const int Explosion_Large = 502;
            public const int Explosion_Grenade = 503;
        }
        #endregion
    }

    /// <summary>
    /// Effect categories for organization.
    /// </summary>
    public enum EffectCategory
    {
        MuzzleFlash,
        ShellEject,
        Tracer,
        Impact,
        Decal,
        Explosion,
        Other
    }

    /// <summary>
    /// Surface-specific impact mapping.
    /// </summary>
    [System.Serializable]
    public class SurfaceImpactEntry
    {
        public Audio.SurfaceMaterialType SurfaceType;
        public ImpactType ImpactType;
        public EffectPrefabRegistry.EffectEntry Effect;
    }
}
