using ECSStatusType = global::Player.Components.StatusEffectType;
using UIStatusType = DIG.Combat.UI.StatusEffectType;

namespace DIG.Combat.UI
{
    /// <summary>
    /// EPIC 15.30: Maps ECS StatusEffectType (Player.Components, 12 values)
    /// to UI StatusEffectType (DIG.Combat.UI, 30+ values) for FloatingTextManager.ShowStatusApplied().
    /// Environmental effects (Hypoxia, RadiationPoisoning, Concussion) map to None (no combat visual).
    /// </summary>
    public static class StatusEffectTypeConverter
    {
        public static UIStatusType ToUI(ECSStatusType ecsType)
        {
            return ecsType switch
            {
                ECSStatusType.Burn => UIStatusType.Burn,
                ECSStatusType.Bleed => UIStatusType.Bleed,
                ECSStatusType.Frostbite => UIStatusType.Frostbite,
                ECSStatusType.Shock => UIStatusType.Stun,
                ECSStatusType.PoisonDOT => UIStatusType.Poison,
                ECSStatusType.Stun => UIStatusType.Stun,
                ECSStatusType.Slow => UIStatusType.Slow,
                ECSStatusType.Weaken => UIStatusType.Weakness,
                // Environmental — no combat visual
                ECSStatusType.Hypoxia => UIStatusType.None,
                ECSStatusType.RadiationPoisoning => UIStatusType.None,
                ECSStatusType.Concussion => UIStatusType.None,
                _ => UIStatusType.None
            };
        }
    }
}
