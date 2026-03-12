// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.22 · DamageFeedbackProfile
// Data-driven configuration for floating damage text visual profiles.
// Maps HitType + DamageType combinations to specific visual treatments.
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using DamageNumbersPro;
using DIG.Targeting.Theming;
using System.Collections.Generic;

namespace DIG.Combat.UI.Config
{
    /// <summary>
    /// EPIC 15.22: Data-driven profile for damage feedback visuals.
    /// Allows designers to configure hit severity, defensive feedback, and elemental
    /// visuals without code changes.
    /// Create via: Assets > Create > DIG > Combat > Damage Feedback Profile
    /// </summary>
    [CreateAssetMenu(fileName = "DamageFeedbackProfile", menuName = "DIG/Combat/Damage Feedback Profile")]
    public class DamageFeedbackProfile : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────
        // Hit Severity Profiles
        // ─────────────────────────────────────────────────────────────────
        [Header("Hit Severity (Motion/Style)")]

        [Tooltip("Normal hit visual profile")]
        public DamageNumberProfile NormalHit = new DamageNumberProfile
        {
            ScaleMultiplier = 1f,
            ColorOverride = Color.white
        };

        [Tooltip("Critical hit visual profile")]
        public DamageNumberProfile CriticalHit = new DamageNumberProfile
        {
            ScaleMultiplier = 1.5f,
            ColorOverride = Color.yellow,
            UseColorOverride = true
        };

        [Tooltip("Graze/glancing blow visual profile")]
        public DamageNumberProfile GrazeHit = new DamageNumberProfile
        {
            ScaleMultiplier = 0.8f,
            ColorOverride = Color.gray
        };

        [Tooltip("Miss visual profile")]
        public DamageNumberProfile MissHit = new DamageNumberProfile
        {
            ScaleMultiplier = 0.9f,
            ColorOverride = new Color(0.5f, 0.5f, 0.5f, 0.7f),
            UseColorOverride = true
        };

        [Tooltip("Execute/killing blow visual profile")]
        public DamageNumberProfile ExecuteHit = new DamageNumberProfile
        {
            ScaleMultiplier = 1.8f,
            ColorOverride = new Color(1f, 0.3f, 0.1f),
            UseColorOverride = true
        };

        // ─────────────────────────────────────────────────────────────────
        // Defensive Feedback Profiles
        // ─────────────────────────────────────────────────────────────────
        [Header("Defensive Feedback")]

        [Tooltip("Block visual profile")]
        public DamageNumberProfile BlockedHit = new DamageNumberProfile
        {
            ScaleMultiplier = 1.1f,
            ColorOverride = new Color(0.3f, 0.5f, 1f),
            UseColorOverride = true
        };

        [Tooltip("Parry visual profile")]
        public DamageNumberProfile ParriedHit = new DamageNumberProfile
        {
            ScaleMultiplier = 1.2f,
            ColorOverride = new Color(1f, 0.85f, 0.2f),
            UseColorOverride = true
        };

        [Tooltip("Immune visual profile")]
        public DamageNumberProfile ImmuneHit = new DamageNumberProfile
        {
            ScaleMultiplier = 1f,
            ColorOverride = new Color(0.9f, 0.9f, 0.9f),
            UseColorOverride = true
        };

        // ─────────────────────────────────────────────────────────────────
        // Utility Prefabs (not tied to hit severity)
        // ─────────────────────────────────────────────────────────────────
        [Header("Utility Prefabs")]

        [Tooltip("Healing number prefab (green, rising)")]
        public DamageNumber HealPrefab;

        [Tooltip("Shield absorb prefab (cyan)")]
        public DamageNumber AbsorbPrefab;

        [Tooltip("DOT/Bleed tick prefab (smaller, periodic)")]
        public DamageNumber DOTPrefab;

        // ─────────────────────────────────────────────────────────────────
        // Damage Type Profiles (Color/Font/Juice)
        // ─────────────────────────────────────────────────────────────────
        [Header("Damage Types (Color/Font)")]
        public List<DamageTypeProfile> DamageTypes = new List<DamageTypeProfile>();

        // ─────────────────────────────────────────────────────────────────
        // Context Event Profiles
        // ─────────────────────────────────────────────────────────────────
        [Header("Context Events")]

        [Tooltip("Headshot context event profile")]
        public DamageNumberProfile HeadshotText = new DamageNumberProfile
        {
            ScaleMultiplier = 1.3f,
            ColorOverride = new Color(1f, 0.8f, 0f),
            UseColorOverride = true
        };

        [Tooltip("Backstab context event profile")]
        public DamageNumberProfile BackstabText = new DamageNumberProfile
        {
            ScaleMultiplier = 1.2f,
            ColorOverride = new Color(0.8f, 0.2f, 0.8f),
            UseColorOverride = true
        };

        // ─────────────────────────────────────────────────────────────────
        // Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Settings")]

        [Tooltip("Maximum distance to spawn damage numbers from camera")]
        public float CullDistance = 50f;

        [Tooltip("Maximum active damage numbers before culling low-priority ones")]
        public int MaxActiveNumbers = 50;

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Get the visual profile for a specific HitType.
        /// </summary>
        public DamageNumberProfile GetHitProfile(HitType hitType)
        {
            return hitType switch
            {
                HitType.Critical => CriticalHit,
                HitType.Graze => GrazeHit,
                HitType.Miss => MissHit,
                HitType.Execute => ExecuteHit,
                HitType.Blocked => BlockedHit,
                HitType.Parried => ParriedHit,
                HitType.Immune => ImmuneHit,
                _ => NormalHit
            };
        }

        /// <summary>
        /// Find the damage type profile for a specific element.
        /// </summary>
        public DamageTypeProfile GetDamageTypeProfile(DamageType type)
        {
            for (int i = 0; i < DamageTypes.Count; i++)
            {
                if (DamageTypes[i].Type == type)
                    return DamageTypes[i];
            }
            return default;
        }
    }

    /// <summary>
    /// EPIC 15.22: Visual profile for a specific hit severity tier.
    /// </summary>
    [System.Serializable]
    public struct DamageNumberProfile
    {
        [Tooltip("Optional prefab override (uses default if null)")]
        public DamageNumber Prefab;

        [Tooltip("Scale multiplier for this hit type")]
        public float ScaleMultiplier;

        [Tooltip("Color override for this hit type")]
        public Color ColorOverride;

        [Tooltip("Whether to use the color override instead of element color")]
        public bool UseColorOverride;
    }

    /// <summary>
    /// EPIC 15.22: Visual profile for a specific damage element type.
    /// </summary>
    [System.Serializable]
    public struct DamageTypeProfile
    {
        [Tooltip("The damage type this profile applies to")]
        public DamageType Type;

        [Tooltip("Display name for this damage type")]
        public string DisplayName;

        [Tooltip("Primary color for this damage type")]
        public Color Color;

        [Tooltip("Size multiplier for this damage type")]
        public float SizeMultiplier;
    }
}
