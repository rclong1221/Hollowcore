using Unity.Entities;

namespace DIG.Items
{
    /// <summary>
    /// EPIC 16.6: Slot where an affix can appear on an item.
    /// </summary>
    public enum AffixSlot : byte
    {
        Implicit = 0,
        Prefix = 1,
        Suffix = 2
    }

    /// <summary>
    /// EPIC 16.6: Stat types that affixes can modify.
    /// </summary>
    public enum StatType : byte
    {
        BaseDamage = 0,
        AttackSpeed = 1,
        CritChance = 2,
        CritMultiplier = 3,
        Armor = 4,
        MaxHealthBonus = 5,
        MovementSpeedBonus = 6,
        DamageResistance = 7
    }

    /// <summary>
    /// EPIC 16.6: A rolled affix on an item entity.
    /// Buffer on ITEM entities only. Capacity 4 covers implicit + 1 prefix + 2 suffixes.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ItemAffix : IBufferElementData
    {
        public int AffixId;
        public AffixSlot Slot;
        public float Value;
        public int Tier;
    }
}
