using UnityEngine;
using Unity.Mathematics;
using DIG.Targeting.Theming;
using DIG.Combat.UI.Config;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Base adapter for integrating Asset Store damage number assets.
    /// EPIC 15.22: All visual config (colors, scales, prefabs) comes from DamageFeedbackProfile.
    /// </summary>
    public abstract class DamageNumberAdapterBase : MonoBehaviour, IDamageNumberProvider
    {
        [Header("Feedback Profile")]
        [Tooltip("Data-driven profile for all damage number visuals. Assign via DIG > Setup > Create Default Damage Feedback Profile.")]
        [SerializeField] protected DamageFeedbackProfile feedbackProfile;

        /// <summary>Public read-only access to the visual feedback profile.</summary>
        public DamageFeedbackProfile FeedbackProfile => feedbackProfile;

        protected virtual void OnEnable()
        {
            CombatUIRegistry.RegisterDamageNumbers(this);
        }

        protected virtual void OnDisable()
        {
            CombatUIRegistry.UnregisterDamageNumbers(this);
        }

        public abstract void ShowDamageNumber(float damage, float3 worldPosition, HitType hitType, DamageType damageType);
        public abstract void ShowMiss(float3 worldPosition);
        public abstract void ShowHealNumber(float amount, float3 worldPosition);

        /// <summary>
        /// Get color based on hit type from profile.
        /// </summary>
        protected Color GetHitTypeColor(HitType hitType)
        {
            if (feedbackProfile == null) return Color.white;
            return feedbackProfile.GetHitProfile(hitType).ColorOverride;
        }

        /// <summary>
        /// Get color based on damage element type from profile.
        /// </summary>
        protected Color GetElementColor(DamageType damageType)
        {
            if (feedbackProfile != null)
            {
                var profile = feedbackProfile.GetDamageTypeProfile(damageType);
                if (profile.Color != default) return profile.Color;
            }
            // EPIC 15.30: Hardcoded fallbacks when profile is missing or incomplete
            return damageType switch
            {
                DamageType.Fire => new Color(1f, 0.4f, 0.1f),       // Orange-red
                DamageType.Ice => new Color(0.3f, 0.7f, 1f),        // Light blue
                DamageType.Lightning => new Color(1f, 0.95f, 0.3f),  // Yellow
                DamageType.Poison => new Color(0.4f, 0.9f, 0.2f),   // Green
                DamageType.Holy => new Color(1f, 1f, 0.8f),          // Warm white
                DamageType.Shadow => new Color(0.6f, 0.2f, 0.8f),   // Purple
                DamageType.Arcane => new Color(0.8f, 0.3f, 1f),     // Magenta
                _ => Color.white
            };
        }

        /// <summary>
        /// Get scale based on hit type from profile.
        /// </summary>
        protected float GetScale(HitType hitType)
        {
            if (feedbackProfile == null) return 1f;
            float scale = feedbackProfile.GetHitProfile(hitType).ScaleMultiplier;
            return scale > 0f ? scale : 1f;
        }

        /// <summary>
        /// Blend hit type color with element color.
        /// Defensive types use their dedicated color without element blending.
        /// </summary>
        protected virtual Color GetFinalColor(HitType hitType, DamageType damageType)
        {
            if (feedbackProfile == null) return Color.white;

            var hitProfile = feedbackProfile.GetHitProfile(hitType);

            // Defensive types and any profile with UseColorOverride use their dedicated color
            if (hitProfile.UseColorOverride)
                return hitProfile.ColorOverride;

            Color elementColor = GetElementColor(damageType);

            // For crits, tint the element color with crit color
            if (hitType == HitType.Critical)
            {
                var critProfile = feedbackProfile.CriticalHit;
                return Color.Lerp(elementColor, critProfile.ColorOverride, 0.5f);
            }

            // Execute: tint element color with execute color
            if (hitType == HitType.Execute)
            {
                var execProfile = feedbackProfile.ExecuteHit;
                return Color.Lerp(elementColor, execProfile.ColorOverride, 0.6f);
            }

            // For graze, dim the element color
            if (hitType == HitType.Graze)
                return elementColor * 0.7f;

            return elementColor;
        }
    }
}
