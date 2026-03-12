using System;
using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Single achievement definition.
    /// Create via DIG/Achievement/Achievement Definition in Assets menu.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Achievement/Achievement Definition")]
    public class AchievementDefinitionSO : ScriptableObject
    {
        [Tooltip("Stable unique ID (MUST NOT change across versions)")]
        public ushort AchievementId;

        [Tooltip("Display name shown in UI (e.g. 'Slayer', 'Master Crafter')")]
        public string AchievementName = "";

        [Tooltip("Description text. Use {0} for threshold placeholder.")]
        [TextArea]
        public string Description = "";

        [Tooltip("Category for UI grouping")]
        public AchievementCategory Category = AchievementCategory.Combat;

        [Tooltip("What event to track for progress")]
        public AchievementConditionType ConditionType = AchievementConditionType.EnemyKill;

        [Tooltip("Subtype filter (EnemyTypeId, RecipeId, RarityLevel, etc.)")]
        public int ConditionParam;

        [Tooltip("Hide from UI until unlocked")]
        public bool IsHidden;

        [Tooltip("Achievement icon for panel and toast")]
        public Sprite Icon;

        [Tooltip("Tier definitions (Bronze through Platinum)")]
        public AchievementTierDefinition[] Tiers = new AchievementTierDefinition[]
        {
            new AchievementTierDefinition
            {
                Tier = AchievementTier.Bronze,
                Threshold = 10,
                RewardType = AchievementRewardType.Gold,
                RewardIntValue = 100,
                RewardDescription = "100 Gold"
            }
        };
    }

    /// <summary>
    /// EPIC 17.7: Single tier within an achievement definition.
    /// </summary>
    [Serializable]
    public struct AchievementTierDefinition
    {
        public AchievementTier Tier;

        [Tooltip("Counter value required for this tier")]
        [Min(1)]
        public int Threshold;

        public AchievementRewardType RewardType;

        [Tooltip("Amount for Gold/XP/TitleId/CosmeticId/TalentPoints/RecipeId")]
        public int RewardIntValue;

        [Tooltip("Amount for stat bonuses (percentage)")]
        public float RewardFloatValue;

        [Tooltip("Human-readable reward text (e.g. '500 Gold')")]
        public string RewardDescription;
    }
}
