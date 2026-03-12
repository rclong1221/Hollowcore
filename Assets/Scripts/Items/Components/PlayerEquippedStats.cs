using Unity.Entities;

namespace DIG.Items
{
    /// <summary>
    /// EPIC 16.6: Aggregate of all equipped item stats on a player entity.
    /// Recomputed by EquippedStatsSystem whenever equipment changes.
    /// Safe to add to player entity (~32 bytes, within 16KB budget).
    /// </summary>
    public struct PlayerEquippedStats : IComponentData
    {
        public float TotalBaseDamage;
        public float TotalAttackSpeed;
        public float TotalCritChance;
        public float TotalCritMultiplier;
        public float TotalArmor;
        public float TotalMaxHealthBonus;
        public float TotalMovementSpeedBonus;
        public float TotalDamageResistance;

        // Resource modifiers (EPIC 16.8)
        public float TotalMaxManaBonus;
        public float TotalManaRegenBonus;
        public float TotalMaxEnergyBonus;
        public float TotalEnergyRegenBonus;
        public float TotalMaxStaminaBonus;
        public float TotalStaminaRegenBonus;

        // Progression modifier (EPIC 16.14)
        public float TotalXPBonusPercent;
    }
}
