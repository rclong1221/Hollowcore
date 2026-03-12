// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · DamageNumberConfig
// EPIC 15.22 · Extended with defensive/execute prefabs and culling settings
// Configuration for Damage Numbers Pro integration styling
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using DamageNumbersPro;
using DIG.Targeting.Theming;

namespace DIG.Combat.UI.Config
{
    /// <summary>
    /// EPIC 15.9/15.22: Configuration for damage number prefabs and styling.
    /// Links to Damage Numbers Pro prefabs for each damage/hit type.
    /// Create via: Assets > Create > DIG > Combat > Damage Number Config
    /// </summary>
    [CreateAssetMenu(fileName = "DamageNumberConfig", menuName = "DIG/Combat/Damage Number Config")]
    public class DamageNumberConfig : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────
        // Prefab References (Damage Numbers Pro)
        // ─────────────────────────────────────────────────────────────────
        [Header("Damage Number Prefabs")]

        [Tooltip("Default damage number prefab")]
        public DamageNumber DefaultPrefab;

        [Tooltip("Critical/headshot damage prefab (larger, golden)")]
        public DamageNumber CriticalPrefab;

        [Tooltip("Healing number prefab (green, rising)")]
        public DamageNumber HealPrefab;

        [Tooltip("Shield damage prefab (blue/cyan)")]
        public DamageNumber ShieldPrefab;

        [Tooltip("Blocked damage prefab (gray, crossed out)")]
        public DamageNumber BlockedPrefab;

        [Tooltip("Status effect tick damage prefab (smaller)")]
        public DamageNumber DotPrefab;

        // ─────────────────────────────────────────────────────────────────
        // EPIC 15.22: Extended Prefabs
        // ─────────────────────────────────────────────────────────────────
        [Header("EPIC 15.22: Defensive & Execute Prefabs")]

        [Tooltip("Parry text prefab (gold, dramatic)")]
        public DamageNumber ParriedPrefab;

        [Tooltip("Immune text prefab (white/silver)")]
        public DamageNumber ImmunePrefab;

        [Tooltip("Execute/killing blow prefab (large, dramatic red-gold)")]
        public DamageNumber ExecutePrefab;

        // ─────────────────────────────────────────────────────────────────
        // Elemental Damage Prefabs
        // ─────────────────────────────────────────────────────────────────
        [Header("Elemental Prefabs")]

        [Tooltip("Fire damage prefab (orange/red with flame icon)")]
        public DamageNumber FirePrefab;

        [Tooltip("Ice damage prefab (cyan/blue with frost icon)")]
        public DamageNumber IcePrefab;

        [Tooltip("Lightning damage prefab (yellow with bolt icon)")]
        public DamageNumber LightningPrefab;

        [Tooltip("Poison damage prefab (green with skull icon)")]
        public DamageNumber PoisonPrefab;

        // ─────────────────────────────────────────────────────────────────
        // Style Configuration
        // ─────────────────────────────────────────────────────────────────
        [Header("Text Styling")]

        [Tooltip("Enable elemental suffixes")]
        public bool UseElementalSuffixes = true;

        [Tooltip("Format string for normal damage")]
        public string NormalFormat = "{0:F0}";

        [Tooltip("Format string for critical damage")]
        public string CriticalFormat = "{0:F0}!";

        [Tooltip("Format string for healing")]
        public string HealFormat = "+{0:F0}";

        [Tooltip("Format string for blocked damage")]
        public string BlockedFormat = "({0:F0})";

        [Tooltip("Format string for parried damage")]
        public string ParriedFormat = "PARRY!";

        [Tooltip("Format string for immune")]
        public string ImmuneFormat = "IMMUNE";

        [Tooltip("Format string for execute/killing blow")]
        public string ExecuteFormat = "{0:F0} EXECUTE!";

        // ─────────────────────────────────────────────────────────────────
        // Animation Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Animation")]

        [Tooltip("Scale multiplier for critical hits")]
        [Range(1.1f, 2f)]
        public float CriticalScaleMultiplier = 1.5f;

        [Tooltip("Scale multiplier for execute/killing blow")]
        [Range(1.5f, 3f)]
        public float ExecuteScaleMultiplier = 1.8f;

        [Tooltip("Random horizontal offset range")]
        public Vector2 SpawnOffsetX = new Vector2(-0.5f, 0.5f);

        [Tooltip("Vertical offset above hit point")]
        [Range(0f, 1f)]
        public float SpawnOffsetY = 0.3f;

        // ─────────────────────────────────────────────────────────────────
        // EPIC 15.22: Culling Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("EPIC 15.22: Culling")]

        [Tooltip("Maximum distance from camera to spawn damage numbers")]
        public float CullDistance = 50f;

        [Tooltip("Maximum number of active damage numbers at once")]
        public int MaxActiveNumbers = 50;

        // ─────────────────────────────────────────────────────────────────
        // Methods
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Get the elemental suffix emoji for a damage type</summary>
        public string GetElementalSuffix(DamageType damageType)
        {
            if (!UseElementalSuffixes) return "";

            return damageType switch
            {
                DamageType.Fire => " \ud83d\udd25",
                DamageType.Ice => " \u2744",
                DamageType.Lightning => " \u26a1",
                DamageType.Poison => " \u2620",
                _ => ""
            };
        }

        /// <summary>
        /// Get the appropriate prefab for a hit/damage type combination.
        /// EPIC 15.22: Extended with Blocked, Parried, Immune, Execute.
        /// </summary>
        public DamageNumber GetPrefab(HitType hitType, DamageType damageType)
        {
            // First check special hit types
            switch (hitType)
            {
                case HitType.Critical:
                    return CriticalPrefab != null ? CriticalPrefab : DefaultPrefab;
                case HitType.Execute:
                    return ExecutePrefab != null ? ExecutePrefab : (CriticalPrefab != null ? CriticalPrefab : DefaultPrefab);
                case HitType.Blocked:
                    return BlockedPrefab != null ? BlockedPrefab : DefaultPrefab;
                case HitType.Parried:
                    return ParriedPrefab != null ? ParriedPrefab : (BlockedPrefab != null ? BlockedPrefab : DefaultPrefab);
                case HitType.Immune:
                    return ImmunePrefab != null ? ImmunePrefab : DefaultPrefab;
            }

            // Then check elemental types
            switch (damageType)
            {
                case DamageType.Fire:
                    return FirePrefab != null ? FirePrefab : DefaultPrefab;

                case DamageType.Ice:
                    return IcePrefab != null ? IcePrefab : DefaultPrefab;

                case DamageType.Lightning:
                    return LightningPrefab != null ? LightningPrefab : DefaultPrefab;

                case DamageType.Poison:
                    return PoisonPrefab != null ? PoisonPrefab : DefaultPrefab;
            }

            return DefaultPrefab;
        }

        /// <summary>
        /// Get the format string for a hit type.
        /// EPIC 15.22: Extended with new types.
        /// </summary>
        public string GetFormat(HitType hitType)
        {
            return hitType switch
            {
                HitType.Critical => CriticalFormat,
                HitType.Blocked => BlockedFormat,
                HitType.Parried => ParriedFormat,
                HitType.Immune => ImmuneFormat,
                HitType.Execute => ExecuteFormat,
                _ => NormalFormat
            };
        }
    }
}
