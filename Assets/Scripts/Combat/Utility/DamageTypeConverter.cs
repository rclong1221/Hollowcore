using SurvivalDamageType = Player.Components.DamageType;
using ThemeDamageType = DIG.Targeting.Theming.DamageType;

namespace DIG.Combat.Utility
{
    /// <summary>
    /// EPIC 15.29/15.30: Centralized conversion between the two DamageType enums.
    /// - DIG.Targeting.Theming.DamageType (8 elemental types, used in combat resolution + UI)
    /// - Player.Components.DamageType (11 types, used in DamageEvent + health pipeline)
    /// EPIC 15.30: Lossless for all combat elemental types (Ice, Lightning, Holy, Shadow, Arcane).
    /// </summary>
    public static class DamageTypeConverter
    {
        /// <summary>
        /// Theme (8 elemental) to Survival (11 types) for DamageEvent.Type.
        /// </summary>
        public static SurvivalDamageType ToSurvival(ThemeDamageType theme)
        {
            return theme switch
            {
                ThemeDamageType.Physical => SurvivalDamageType.Physical,
                ThemeDamageType.Fire => SurvivalDamageType.Heat,
                ThemeDamageType.Ice => SurvivalDamageType.Ice,
                ThemeDamageType.Lightning => SurvivalDamageType.Lightning,
                ThemeDamageType.Poison => SurvivalDamageType.Toxic,
                ThemeDamageType.Holy => SurvivalDamageType.Holy,
                ThemeDamageType.Shadow => SurvivalDamageType.Shadow,
                ThemeDamageType.Arcane => SurvivalDamageType.Arcane,
                _ => SurvivalDamageType.Physical
            };
        }

        /// <summary>
        /// Survival (11 types) to Theme (8 elemental) for visual bridge.
        /// </summary>
        public static ThemeDamageType ToTheme(SurvivalDamageType survival)
        {
            return survival switch
            {
                SurvivalDamageType.Physical => ThemeDamageType.Physical,
                SurvivalDamageType.Heat => ThemeDamageType.Fire,
                SurvivalDamageType.Toxic => ThemeDamageType.Poison,
                SurvivalDamageType.Ice => ThemeDamageType.Ice,
                SurvivalDamageType.Lightning => ThemeDamageType.Lightning,
                SurvivalDamageType.Holy => ThemeDamageType.Holy,
                SurvivalDamageType.Shadow => ThemeDamageType.Shadow,
                SurvivalDamageType.Arcane => ThemeDamageType.Arcane,
                SurvivalDamageType.Explosion => ThemeDamageType.Physical,
                SurvivalDamageType.Radiation => ThemeDamageType.Physical,
                SurvivalDamageType.Suffocation => ThemeDamageType.Physical,
                _ => ThemeDamageType.Physical
            };
        }
    }
}
