// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · EnemyUIConfig
// Configuration for enemy-related UI elements (health bars, nameplates)
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;

namespace DIG.Combat.UI.Config
{
    /// <summary>
    /// EPIC 15.9: Configuration for enemy UI elements including health bars and nameplates.
    /// Create via: Assets → Create → DIG → Combat → Enemy UI Config
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyUIConfig", menuName = "DIG/Combat/Enemy UI Config")]
    public class EnemyUIConfig : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────
        // Health Bar Appearance
        // ─────────────────────────────────────────────────────────────────
        [Header("Health Bar Appearance")]
        
        [Tooltip("Color for full health")]
        public Color HealthFullColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        
        [Tooltip("Color for low health (< 25%)")]
        public Color HealthLowColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        
        [Tooltip("Color for health trail (recent damage)")]
        public Color TrailColor = new Color(1f, 0.5f, 0f, 0.8f);
        
        [Tooltip("Background color")]
        public Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        
        [Tooltip("Border color")]
        public Color BorderColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        // ─────────────────────────────────────────────────────────────────
        // Health Bar Sizes by Enemy Tier
        // ─────────────────────────────────────────────────────────────────
        [Header("Size by Enemy Tier")]
        
        [Tooltip("Health bar width for trash mobs")]
        public Vector2 TrashMobSize = new Vector2(1f, 0.1f);
        
        [Tooltip("Health bar width for elite enemies")]
        public Vector2 EliteSize = new Vector2(1.5f, 0.15f);
        
        [Tooltip("Health bar width for mini-bosses")]
        public Vector2 MiniBossSize = new Vector2(2f, 0.2f);
        
        // ─────────────────────────────────────────────────────────────────
        // Shield Bar Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Shield Bar")]
        
        [Tooltip("Enable shield overlay")]
        public bool ShowShieldBar = true;
        
        [Tooltip("Shield bar color")]
        public Color ShieldColor = new Color(0.3f, 0.7f, 1f, 0.9f);
        
        [Tooltip("Shield bar height relative to health bar")]
        [Range(0.3f, 0.6f)]
        public float ShieldBarHeightRatio = 0.4f;
        
        // ─────────────────────────────────────────────────────────────────
        // Armor Bar Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Armor Bar")]
        
        [Tooltip("Enable armor overlay")]
        public bool ShowArmorBar = true;
        
        [Tooltip("Armor bar color")]
        public Color ArmorColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
        
        [Tooltip("Armor bar height relative to health bar")]
        [Range(0.2f, 0.5f)]
        public float ArmorBarHeightRatio = 0.3f;
        
        // ─────────────────────────────────────────────────────────────────
        // Status Effect Icons
        // ─────────────────────────────────────────────────────────────────
        [Header("Status Icons on Health Bar")]
        
        [Tooltip("Show status effect icons above health bar")]
        public bool ShowStatusIcons = true;
        
        [Tooltip("Maximum status icons to display")]
        [Range(3, 8)]
        public int MaxStatusIcons = 5;
        
        [Tooltip("Icon size relative to health bar height")]
        [Range(0.5f, 1.5f)]
        public float StatusIconSizeRatio = 1f;
        
        // ─────────────────────────────────────────────────────────────────
        // Level/Tier Indicator
        // ─────────────────────────────────────────────────────────────────
        [Header("Level Indicator")]
        
        [Tooltip("Show enemy level next to health bar")]
        public bool ShowLevel = true;
        
        [Tooltip("Color for enemies at player level")]
        public Color LevelEqualColor = Color.white;
        
        [Tooltip("Color for enemies above player level")]
        public Color LevelHigherColor = new Color(1f, 0.5f, 0.5f, 1f);
        
        [Tooltip("Color for enemies below player level")]
        public Color LevelLowerColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        // ─────────────────────────────────────────────────────────────────
        // Nameplate Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Nameplates")]
        
        [Tooltip("Show enemy name above health bar")]
        public bool ShowNameplate = true;
        
        [Tooltip("Nameplate font size")]
        [Range(8, 24)]
        public int NameplateFontSize = 14;
        
        [Tooltip("Elite/boss name color")]
        public Color EliteNameColor = new Color(1f, 0.8f, 0.2f, 1f);
        
        [Tooltip("Normal enemy name color")]
        public Color NormalNameColor = Color.white;
        
        // ─────────────────────────────────────────────────────────────────
        // Billboard Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Billboard Behavior")]
        
        [Tooltip("Always face camera")]
        public bool BillboardEnabled = true;
        
        [Tooltip("Lock Y axis (no tilting)")]
        public bool LockYAxis = true;
        
        [Tooltip("Scale with distance to maintain readability")]
        public bool ScaleWithDistance = false;
        
        [Tooltip("Minimum scale when scaling with distance")]
        [Range(0.3f, 1f)]
        public float MinDistanceScale = 0.5f;
        
        // ─────────────────────────────────────────────────────────────────
        // Animation Curves
        // ─────────────────────────────────────────────────────────────────
        [Header("Animation")]
        
        [Tooltip("Curve for health bar fill changes")]
        public AnimationCurve HealthFillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Tooltip("Curve for fade in/out")]
        public AnimationCurve FadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        // ─────────────────────────────────────────────────────────────────
        // Helper Methods
        // ─────────────────────────────────────────────────────────────────
        
        /// <summary>Get health color interpolated by current health percentage</summary>
        public Color GetHealthColor(float healthPercent)
        {
            return Color.Lerp(HealthLowColor, HealthFullColor, healthPercent);
        }
        
        /// <summary>Get size based on enemy tier</summary>
        public Vector2 GetSizeForTier(EnemyTier tier)
        {
            return tier switch
            {
                EnemyTier.Elite => EliteSize,
                EnemyTier.MiniBoss => MiniBossSize,
                EnemyTier.Boss => MiniBossSize * 1.5f,
                _ => TrashMobSize
            };
        }
        
        /// <summary>Get level indicator color based on level difference</summary>
        public Color GetLevelColor(int enemyLevel, int playerLevel)
        {
            int diff = enemyLevel - playerLevel;
            if (diff > 2) return LevelHigherColor;
            if (diff < -2) return LevelLowerColor;
            return LevelEqualColor;
        }
    }
    
    /// <summary>Enemy tier for UI sizing</summary>
    public enum EnemyTier
    {
        Trash,
        Elite,
        MiniBoss,
        Boss
    }
}
