using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Targeting.Theming
{
    /// <summary>
    /// ScriptableObject theme profile for designer-friendly configuration.
    /// Baked to ECS IndicatorThemeData at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeProfile", menuName = "DIG/Targeting/Theme Profile")]
    public class ThemeProfile : ScriptableObject
    {
        [Header("Profile Info")]
        public string ProfileName = "Default";
        public string Description;
        
        [Header("Faction Colors")]
        public Color EnemyColor = Color.red;
        public Color AllyColor = Color.green;
        public Color NeutralColor = Color.white;
        public Color HostileColor = new Color(1f, 0.5f, 0f); // Orange for PvP
        
        [Header("Category Colors")]
        public Color NormalColor = Color.white;
        public Color EliteColor = Color.yellow;
        public Color BossColor = new Color(1f, 0f, 1f); // Magenta
        
        [Header("Damage Type Colors")]
        public Color PhysicalColor = Color.gray;
        public Color FireColor = new Color(1f, 0.4f, 0f);
        public Color IceColor = new Color(0.5f, 0.8f, 1f);
        public Color LightningColor = new Color(0.8f, 0.8f, 1f);
        public Color PoisonColor = new Color(0.5f, 1f, 0.3f);
        
        [Header("Hit Type Settings")]
        public float CritSizeMultiplier = 1.5f;
        public float MissAlpha = 0.3f;
        
        [Header("Indicator Prefab Addresses (Addressables)")]
        [Tooltip("Use Addressables paths to reduce memory. Empty = use default.")]
        public string DefaultIndicatorAddress = "";
        public string BossIndicatorAddress = "";
        public string LockOnIndicatorAddress = "";
        
        [Header("Accessibility Overrides")]
        public bool UseHighContrastColors = false;
        public Color HighContrastEnemy = Color.red;
        public Color HighContrastAlly = Color.cyan;
        
        /// <summary>
        /// Get color for a given faction.
        /// </summary>
        public Color GetFactionColor(TargetFaction faction, bool highContrast = false)
        {
            if (highContrast || UseHighContrastColors)
            {
                return faction switch
                {
                    TargetFaction.Enemy => HighContrastEnemy,
                    TargetFaction.Ally => HighContrastAlly,
                    _ => NeutralColor
                };
            }
            
            return faction switch
            {
                TargetFaction.Enemy => EnemyColor,
                TargetFaction.Ally => AllyColor,
                TargetFaction.Hostile => HostileColor,
                _ => NeutralColor
            };
        }
        
        /// <summary>
        /// Get color for a given damage type.
        /// </summary>
        public Color GetDamageTypeColor(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Fire => FireColor,
                DamageType.Ice => IceColor,
                DamageType.Lightning => LightningColor,
                DamageType.Poison => PoisonColor,
                _ => PhysicalColor
            };
        }
        
        /// <summary>
        /// Get category overlay color.
        /// </summary>
        public Color GetCategoryColor(TargetCategory category)
        {
            return category switch
            {
                TargetCategory.Elite => EliteColor,
                TargetCategory.Boss => BossColor,
                TargetCategory.Miniboss => BossColor,
                _ => NormalColor
            };
        }
        
        /// <summary>
        /// Bake to ECS-compatible data.
        /// </summary>
        public IndicatorThemeData Bake()
        {
            return new IndicatorThemeData
            {
                EnemyColor = ColorToFloat4(EnemyColor),
                AllyColor = ColorToFloat4(AllyColor),
                NeutralColor = ColorToFloat4(NeutralColor),
                HostileColor = ColorToFloat4(HostileColor),
                EliteColor = ColorToFloat4(EliteColor),
                BossColor = ColorToFloat4(BossColor),
                FireColor = ColorToFloat4(FireColor),
                IceColor = ColorToFloat4(IceColor),
                LightningColor = ColorToFloat4(LightningColor),
                CritSizeMultiplier = CritSizeMultiplier,
                MissAlpha = MissAlpha,
                UseHighContrast = UseHighContrastColors
            };
        }
        
        private Unity.Mathematics.float4 ColorToFloat4(Color c)
        {
            return new Unity.Mathematics.float4(c.r, c.g, c.b, c.a);
        }
    }
}
