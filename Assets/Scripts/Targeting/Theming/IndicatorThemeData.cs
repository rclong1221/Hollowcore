using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Targeting.Theming
{
    /// <summary>
    /// ECS-baked theme data for Burst-compatible indicator theming.
    /// Populated from ThemeProfile ScriptableObject at bake time.
    /// </summary>
    public struct IndicatorThemeData : IComponentData
    {
        // Faction colors (RGBA as float4)
        public float4 EnemyColor;
        public float4 AllyColor;
        public float4 NeutralColor;
        public float4 HostileColor;
        
        // Category overlay colors
        public float4 EliteColor;
        public float4 BossColor;
        
        // Damage type colors
        public float4 FireColor;
        public float4 IceColor;
        public float4 LightningColor;
        
        // Hit type modifiers
        public float CritSizeMultiplier;
        public float MissAlpha;
        
        // Accessibility
        public bool UseHighContrast;
        
        /// <summary>
        /// Get faction color based on context.
        /// </summary>
        public float4 GetFactionColor(TargetFaction faction)
        {
            return faction switch
            {
                TargetFaction.Enemy => EnemyColor,
                TargetFaction.Ally => AllyColor,
                TargetFaction.Hostile => HostileColor,
                _ => NeutralColor
            };
        }
        
        /// <summary>
        /// Get damage type color.
        /// </summary>
        public float4 GetDamageTypeColor(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Fire => FireColor,
                DamageType.Ice => IceColor,
                DamageType.Lightning => LightningColor,
                _ => new float4(0.5f, 0.5f, 0.5f, 1f) // Gray for physical
            };
        }
        
        /// <summary>
        /// Apply hit type modifier to size.
        /// </summary>
        public float ApplyHitTypeScale(float baseSize, HitType hitType)
        {
            return hitType switch
            {
                HitType.Critical => baseSize * CritSizeMultiplier,
                _ => baseSize
            };
        }
        
        /// <summary>
        /// Apply hit type modifier to alpha.
        /// </summary>
        public float ApplyHitTypeAlpha(float baseAlpha, HitType hitType)
        {
            return hitType switch
            {
                HitType.Miss => baseAlpha * MissAlpha,
                _ => baseAlpha
            };
        }
        
        public static IndicatorThemeData Default => new IndicatorThemeData
        {
            EnemyColor = new float4(1f, 0f, 0f, 1f),
            AllyColor = new float4(0f, 1f, 0f, 1f),
            NeutralColor = new float4(1f, 1f, 1f, 1f),
            HostileColor = new float4(1f, 0.5f, 0f, 1f),
            EliteColor = new float4(1f, 1f, 0f, 1f),
            BossColor = new float4(1f, 0f, 1f, 1f),
            FireColor = new float4(1f, 0.4f, 0f, 1f),
            IceColor = new float4(0.5f, 0.8f, 1f, 1f),
            LightningColor = new float4(0.8f, 0.8f, 1f, 1f),
            CritSizeMultiplier = 1.5f,
            MissAlpha = 0.3f,
            UseHighContrast = false
        };
    }
}
