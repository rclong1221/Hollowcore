// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · CombatFeedbackConfig
// Unified configuration for combat UI feedback and visual settings
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using MoreMountains.Feedbacks;

namespace DIG.Combat.UI.Config
{
    /// <summary>
    /// EPIC 15.9: Centralized configuration for all combat feedback systems.
    /// Create via: Assets → Create → DIG → Combat → Combat Feedback Config
    /// </summary>
    [CreateAssetMenu(fileName = "CombatFeedbackConfig", menuName = "DIG/Combat/Combat Feedback Config")]
    public class CombatFeedbackConfig : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────
        // Damage Number Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Damage Numbers")]
        
        [Tooltip("Enable damage number stacking for rapid hits")]
        public bool EnableDamageStacking = true;
        
        [Tooltip("Time window (seconds) for stacking damage numbers")]
        [Range(0.05f, 0.5f)]
        public float DamageStackingWindow = 0.1f;
        
        [Tooltip("Maximum distance for damage number spawns before culling")]
        [Range(10f, 100f)]
        public float DamageNumberCullDistance = 50f;
        
        [Tooltip("Offset range for damage number spawn position")]
        public Vector2 DamageNumberOffsetRange = new Vector2(0.3f, 0.6f);
        
        // ─────────────────────────────────────────────────────────────────
        // Floating Text Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Floating Text")]
        
        [Tooltip("Pool size for floating text elements")]
        [Range(10, 100)]
        public int FloatingTextPoolSize = 50;
        
        [Tooltip("Default lifetime for floating text")]
        [Range(0.5f, 3f)]
        public float FloatingTextLifetime = 1.5f;
        
        [Tooltip("Rise speed for floating text (units per second)")]
        [Range(0.5f, 3f)]
        public float FloatingTextRiseSpeed = 1f;
        
        [Tooltip("Minimum time between same floating text")]
        [Range(0f, 1f)]
        public float FloatingTextSpamCooldown = 0.3f;
        
        // ─────────────────────────────────────────────────────────────────
        // Enemy Health Bar Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Enemy Health Bars")]
        
        [Tooltip("Pool size for enemy health bars")]
        [Range(10, 50)]
        public int EnemyHealthBarPoolSize = 20;
        
        [Tooltip("How long health bars persist after damage (seconds)")]
        [Range(1f, 10f)]
        public float HealthBarPersistDuration = 3f;
        
        [Tooltip("Height offset above enemy")]
        [Range(0.5f, 3f)]
        public float HealthBarHeightOffset = 1.5f;
        
        [Tooltip("Maximum distance for health bar visibility")]
        [Range(10f, 50f)]
        public float HealthBarMaxDistance = 25f;
        
        [Tooltip("Distance at which health bar begins fading")]
        [Range(5f, 25f)]
        public float HealthBarFadeStartDistance = 15f;
        
        [Tooltip("Enable trail effect showing recent damage")]
        public bool EnableHealthTrail = true;
        
        [Tooltip("Speed of health trail catchup")]
        [Range(0.5f, 5f)]
        public float HealthTrailSpeed = 2f;
        
        // ─────────────────────────────────────────────────────────────────
        // Directional Damage Indicator Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Directional Damage Indicators")]
        
        [Tooltip("Maximum simultaneous damage indicators")]
        [Range(4, 12)]
        public int MaxDamageIndicators = 8;
        
        [Tooltip("Indicator display duration (seconds)")]
        [Range(0.5f, 3f)]
        public float IndicatorDuration = 1.5f;
        
        [Tooltip("Distance from screen center (normalized 0-1)")]
        [Range(0.3f, 0.5f)]
        public float IndicatorRadius = 0.4f;
        
        [Tooltip("Color for normal damage")]
        public Color NormalDamageColor = new Color(1f, 0f, 0f, 0.8f);
        
        [Tooltip("Color for critical damage")]
        public Color CriticalDamageColor = new Color(1f, 0.5f, 0f, 0.9f);
        
        // ─────────────────────────────────────────────────────────────────
        // Combo Counter Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Combo Counter")]
        
        [Tooltip("Time between hits before combo resets")]
        [Range(1f, 5f)]
        public float ComboTimeout = 3f;
        
        [Tooltip("Combo counts that trigger milestone feedback")]
        public int[] ComboMilestones = { 5, 10, 25, 50, 100 };
        
        [Tooltip("Enable multiplier display")]
        public bool ShowMultiplier = true;
        
        [Tooltip("Multiplier increment per combo threshold")]
        [Range(0.01f, 0.1f)]
        public float MultiplierIncrement = 0.05f;
        
        // ─────────────────────────────────────────────────────────────────
        // Kill Feed Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Kill Feed")]
        
        [Tooltip("Maximum visible kill feed entries")]
        [Range(3, 10)]
        public int MaxKillFeedEntries = 5;
        
        [Tooltip("Duration entries stay visible (seconds)")]
        [Range(2f, 10f)]
        public float KillFeedEntryDuration = 4f;
        
        [Tooltip("Enable headshot icons")]
        public bool ShowHeadshotIcons = true;
        
        [Tooltip("Enable kill streak notifications")]
        public bool ShowKillStreaks = true;
        
        // ─────────────────────────────────────────────────────────────────
        // Status Effects UI Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Status Effects")]
        
        [Tooltip("Maximum status effect icons visible")]
        [Range(6, 16)]
        public int MaxStatusIcons = 10;
        
        [Tooltip("Flash when duration is below this threshold (seconds)")]
        [Range(2f, 5f)]
        public float StatusExpiringThreshold = 3f;
        
        [Tooltip("Arrange buffs separately from debuffs")]
        public bool SeparateBuffsAndDebuffs = true;
        
        // ─────────────────────────────────────────────────────────────────
        // Combat Log Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Combat Log")]
        
        [Tooltip("Maximum log entries kept in memory")]
        [Range(50, 500)]
        public int MaxLogEntries = 200;
        
        [Tooltip("Enable timestamps in log")]
        public bool ShowTimestamps = true;
        
        [Tooltip("Enable damage source information")]
        public bool ShowDamageSource = true;
        
        // ─────────────────────────────────────────────────────────────────
        // Interaction Ring Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Interaction Ring")]
        
        [Tooltip("Pool size for interaction rings")]
        [Range(5, 20)]
        public int InteractionRingPoolSize = 10;
        
        [Tooltip("Default completion time for held interactions")]
        [Range(0.5f, 3f)]
        public float DefaultInteractionTime = 1f;
        
        [Tooltip("Color for active interaction progress")]
        public Color InteractionActiveColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        
        [Tooltip("Color for cancelled/interrupted interaction")]
        public Color InteractionCancelledColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        
        // ─────────────────────────────────────────────────────────────────
        // Validation
        // ─────────────────────────────────────────────────────────────────
        private void OnValidate()
        {
            // Ensure fade distance is less than max distance
            HealthBarFadeStartDistance = Mathf.Min(HealthBarFadeStartDistance, HealthBarMaxDistance - 1f);
            
            // Ensure milestones are sorted
            if (ComboMilestones != null && ComboMilestones.Length > 0)
            {
                System.Array.Sort(ComboMilestones);
            }
        }
    }
}
