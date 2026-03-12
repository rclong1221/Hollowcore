using UnityEngine;
using DIG.Loot.Definitions;
using DIG.Roguelite;

namespace DIG.Roguelite.Rewards
{
    public enum RewardType : byte
    {
        Item = 0,              // Resolve via LootTableSO
        RunCurrency = 1,       // Add to RunState.RunCurrency
        MetaCurrency = 2,      // Add to MetaBank (rare)
        StatBoost = 3,         // Temporary (this run only)
        AbilityUnlock = 4,     // Unlock ability for this run
        Modifier = 5,          // Add RunModifier (23.4)
        Healing = 6,           // Restore % of max health
        MaxHPUp = 7            // Increase max health for this run
    }

    /// <summary>
    /// EPIC 23.5: Single reward definition. Used by RewardPoolSO and RunEventDefinitionSO.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Reward Definition")]
    public class RewardDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int RewardId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public RewardType Type;
        public byte Rarity;                         // 0 = Common, 1 = Uncommon, 2 = Rare, 3 = Epic, 4 = Legendary

        [Header("Values")]
        public int IntValue;                        // Currency amount, item ID, ability ID
        public float FloatValue;                    // Stat multiplier, heal %, etc.

        [Header("References")]
        public LootTableSO LootTable;               // For Item type (nullable)
        public RunModifierDefinitionSO Modifier;     // For Modifier type (nullable)

        [Header("Constraints")]
        public int MinZoneIndex;                    // 0 = any
        public int MaxZoneIndex;                    // 0 = any
        public int RequiredAscensionLevel;          // 0 = always available
    }
}
