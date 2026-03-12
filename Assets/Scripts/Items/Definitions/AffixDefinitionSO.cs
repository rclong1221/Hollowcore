using System;
using UnityEngine;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// EPIC 16.6: How an affix modifies a stat.
    /// </summary>
    [Serializable]
    public struct AffixStatModifier
    {
        public StatType Stat;

        [Tooltip("Multiplier applied to the rolled affix value for this stat.")]
        public float Multiplier;
    }

    /// <summary>
    /// EPIC 16.6: Definition of a single affix type.
    /// Describes what stats it modifies, valid item categories, and roll parameters.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Items/Affix Definition", order = 2)]
    public class AffixDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int AffixId;
        public string DisplayName;
        public AffixSlot Slot;

        [Header("Stat Modifiers")]
        public AffixStatModifier[] Modifiers;

        [Header("Roll Range")]
        [Tooltip("Minimum value when rolled.")]
        public float MinValue;

        [Tooltip("Maximum value when rolled.")]
        public float MaxValue;

        [Tooltip("Minimum tier (quality level).")]
        [Min(1)]
        public int MinTier = 1;

        [Tooltip("Maximum tier (quality level).")]
        [Min(1)]
        public int MaxTier = 5;

        [Header("Constraints")]
        [Tooltip("Item categories this affix can appear on. Empty = all categories.")]
        public ItemCategory[] ValidCategories;

        [Tooltip("Minimum item rarity required for this affix to appear.")]
        public ItemRarity MinRarity;

        [Tooltip("Weight for random selection (higher = more common).")]
        [Min(0f)]
        public float Weight = 1f;
    }
}
