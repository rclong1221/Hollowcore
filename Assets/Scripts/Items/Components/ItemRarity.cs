namespace DIG.Items
{
    /// <summary>
    /// EPIC 16.6: Item rarity tiers.
    /// Used for loot table rolls, affix scaling, visual presentation, and lifetime.
    /// </summary>
    public enum ItemRarity : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Unique = 5
    }
}
