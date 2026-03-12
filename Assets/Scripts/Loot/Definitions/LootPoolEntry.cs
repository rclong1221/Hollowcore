using System;
using DIG.Items;
using DIG.Items.Definitions;
using DIG.Shared;

namespace DIG.Loot.Definitions
{
    /// <summary>
    /// EPIC 16.6: Type of loot a pool entry can represent.
    /// </summary>
    public enum LootEntryType : byte
    {
        Item = 0,
        Currency = 1,
        Resource = 2,
        NestedTable = 3
    }

    /// <summary>
    /// EPIC 16.6: Condition types for conditional loot table entries.
    /// </summary>
    public enum ConditionType : byte
    {
        MinLevel = 0,
        MaxLevel = 1,
        MinDifficulty = 2,
        MaxDifficulty = 3
    }

    /// <summary>
    /// EPIC 16.6: A single entry within a loot pool.
    /// Defines what can drop, its weight, and constraints.
    /// </summary>
    [Serializable]
    public struct LootPoolEntry
    {
        public LootEntryType Type;

        /// <summary>Item reference (used when Type == Item).</summary>
        public ItemEntrySO Item;

        /// <summary>Resource type (used when Type == Resource).</summary>
        public ResourceType Resource;

        /// <summary>Currency type (used when Type == Currency).</summary>
        public Economy.CurrencyType Currency;

        /// <summary>Nested table (used when Type == NestedTable).</summary>
        public LootTableSO NestedTable;

        [UnityEngine.Min(1)]
        public int MinQuantity;

        [UnityEngine.Min(1)]
        public int MaxQuantity;

        /// <summary>
        /// Relative weight for weighted random selection within the pool.
        /// Higher = more likely to be chosen.
        /// </summary>
        [UnityEngine.Min(0f)]
        public float Weight;

        /// <summary>
        /// Independent drop chance (0-1). 1.0 = always drops if selected.
        /// Applied after weight-based selection.
        /// </summary>
        [UnityEngine.Range(0f, 1f)]
        public float DropChance;

        /// <summary>Minimum rarity for rarity-upgraded drops.</summary>
        public ItemRarity MinRarity;

        /// <summary>Maximum rarity for rarity-upgraded drops.</summary>
        public ItemRarity MaxRarity;

        public static LootPoolEntry Default => new LootPoolEntry
        {
            Type = LootEntryType.Item,
            MinQuantity = 1,
            MaxQuantity = 1,
            Weight = 1f,
            DropChance = 1f,
            MinRarity = ItemRarity.Common,
            MaxRarity = ItemRarity.Legendary
        };
    }

    /// <summary>
    /// EPIC 16.6: Conditional requirement for a loot table/pool entry.
    /// </summary>
    [Serializable]
    public struct LootTableCondition
    {
        public ConditionType Type;
        public float Value;
    }
}
