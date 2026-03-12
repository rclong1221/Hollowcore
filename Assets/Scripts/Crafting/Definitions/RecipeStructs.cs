using System;
using DIG.Economy;
using DIG.Shared;
using UnityEngine;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Station type for crafting stations.
    /// </summary>
    public enum StationType : byte
    {
        Any = 0,
        Workbench = 1,
        Forge = 2,
        AlchemyTable = 3,
        Armory = 4,
        Engineering = 5
    }

    /// <summary>
    /// EPIC 16.13: Recipe category for UI grouping.
    /// </summary>
    public enum RecipeCategory : byte
    {
        Weapons = 0,
        Armor = 1,
        Consumables = 2,
        Ammo = 3,
        Materials = 4,
        Tools = 5,
        Upgrades = 6
    }

    /// <summary>
    /// EPIC 16.13: How a recipe is unlocked.
    /// </summary>
    public enum RecipeUnlockCondition : byte
    {
        AlwaysAvailable = 0,
        PlayerLevel = 1,
        PreviousRecipe = 2,
        QuestComplete = 3,
        SchematicItem = 4
    }

    /// <summary>
    /// EPIC 16.13: What type of ingredient is required.
    /// </summary>
    public enum IngredientType : byte
    {
        Resource = 0,
        Item = 1
    }

    /// <summary>
    /// EPIC 16.13: What type of output a recipe produces.
    /// </summary>
    public enum RecipeOutputType : byte
    {
        Item = 0,
        Resource = 1,
        Currency = 2
    }

    /// <summary>
    /// EPIC 16.13: Crafting queue element state.
    /// </summary>
    public enum CraftState : byte
    {
        Queued = 0,
        InProgress = 1,
        Complete = 2,
        Failed = 3
    }

    /// <summary>
    /// EPIC 16.13: A single ingredient requirement for a recipe.
    /// </summary>
    [Serializable]
    public struct RecipeIngredient
    {
        public IngredientType IngredientType;
        [Tooltip("Used when IngredientType = Resource")]
        public ResourceType ResourceType;
        [Tooltip("Used when IngredientType = Item")]
        public int ItemTypeId;
        public int Quantity;
    }

    /// <summary>
    /// EPIC 16.13: A currency cost for a recipe.
    /// </summary>
    [Serializable]
    public struct CurrencyCost
    {
        public CurrencyType CurrencyType;
        public int Amount;
    }

    /// <summary>
    /// EPIC 16.13: What a recipe produces.
    /// </summary>
    [Serializable]
    public struct RecipeOutput
    {
        public RecipeOutputType OutputType;
        [Tooltip("Used when OutputType = Item")]
        public int ItemTypeId;
        [Tooltip("Used when OutputType = Resource")]
        public ResourceType ResourceType;
        public int Quantity;
        [Tooltip("Used when OutputType = Item. Minimum rarity tier for rolled output.")]
        [Range(0, 5)] public int MinRarity;
        [Tooltip("Used when OutputType = Item. Maximum rarity tier for rolled output.")]
        [Range(0, 5)] public int MaxRarity;
        [Tooltip("Whether to roll random affixes on produced items.")]
        public bool RollAffixes;
    }
}
