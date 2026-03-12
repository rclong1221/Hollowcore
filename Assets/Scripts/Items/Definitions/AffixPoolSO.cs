using System.Collections.Generic;
using UnityEngine;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// EPIC 16.6: Pool of available affixes for an item type.
    /// Referenced by ItemEntrySO.PossibleAffixes.
    /// Controls which prefixes/suffixes/implicits can roll on an item.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Items/Affix Pool", order = 3)]
    public class AffixPoolSO : ScriptableObject
    {
        [Header("Implicit Affixes")]
        [Tooltip("Implicits are always present (not random).")]
        public List<AffixDefinitionSO> Implicits = new();

        [Header("Prefix Affixes")]
        public List<AffixDefinitionSO> Prefixes = new();

        [Tooltip("Maximum prefixes scaled by rarity: Common=0, Uncommon=1, Rare=1, Epic=2, Legendary=2, Unique=3")]
        [Min(0)]
        public int MaxPrefixes = 2;

        [Header("Suffix Affixes")]
        public List<AffixDefinitionSO> Suffixes = new();

        [Tooltip("Maximum suffixes scaled by rarity: Common=0, Uncommon=0, Rare=1, Epic=1, Legendary=2, Unique=2")]
        [Min(0)]
        public int MaxSuffixes = 2;

        /// <summary>
        /// Get max prefix count scaled by item rarity.
        /// </summary>
        public int GetMaxPrefixes(ItemRarity rarity)
        {
            int scale = rarity switch
            {
                ItemRarity.Common => 0,
                ItemRarity.Uncommon => 1,
                ItemRarity.Rare => 1,
                ItemRarity.Epic => 2,
                ItemRarity.Legendary => 2,
                ItemRarity.Unique => 3,
                _ => 0
            };
            return UnityEngine.Mathf.Min(scale, MaxPrefixes);
        }

        /// <summary>
        /// Get max suffix count scaled by item rarity.
        /// </summary>
        public int GetMaxSuffixes(ItemRarity rarity)
        {
            int scale = rarity switch
            {
                ItemRarity.Common => 0,
                ItemRarity.Uncommon => 0,
                ItemRarity.Rare => 1,
                ItemRarity.Epic => 1,
                ItemRarity.Legendary => 2,
                ItemRarity.Unique => 2,
                _ => 0
            };
            return UnityEngine.Mathf.Min(scale, MaxSuffixes);
        }
    }
}
