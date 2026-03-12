using Unity.Entities;

namespace DIG.Items
{
    /// <summary>
    /// EPIC 16.6: Stat block attached to item entities.
    /// Contains base + affix-modified stats. All values default to 0 (no effect).
    /// Placed on ITEM entities only (NOT player entity — avoids 16KB archetype limit).
    /// </summary>
    public struct ItemStatBlock : IComponentData
    {
        public float BaseDamage;
        public float AttackSpeed;
        public float CritChance;
        public float CritMultiplier;
        public float Armor;
        public float MaxHealthBonus;
        public float MovementSpeedBonus;
        public float DamageResistance;

        // Resource modifiers (EPIC 16.8)
        public float MaxManaBonus;
        public float ManaRegenBonus;
        public float MaxEnergyBonus;
        public float EnergyRegenBonus;
        public float MaxStaminaBonus;
        public float StaminaRegenBonus;

        // Progression modifier (EPIC 16.14)
        public float XPBonusPercent;
    }
}
