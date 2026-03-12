using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Interface for achievement UI providers (toast and panel).
    /// MonoBehaviours implement this and register with AchievementUIRegistry.
    /// </summary>
    public interface IAchievementUIProvider
    {
        void ShowToast(AchievementToastData data);
        void UpdatePanel(AchievementPanelData data);
        void UpdateProgress(ushort achievementId, int currentValue, int nextThreshold);
        void HideToast();
    }

    /// <summary>
    /// EPIC 17.7: Data for a single achievement unlock toast notification.
    /// </summary>
    public struct AchievementToastData
    {
        public string AchievementName;
        public string Description;
        public string RewardText;
        public Sprite Icon;
        public AchievementTier Tier;
        public float DisplayDuration;
    }

    /// <summary>
    /// EPIC 17.7: Data for the full achievement panel UI.
    /// </summary>
    public struct AchievementPanelData
    {
        public AchievementEntryUI[] Entries;
        public int TotalUnlocked;
        public int TotalAchievements;
        public float CompletionPercent;
    }

    /// <summary>
    /// EPIC 17.7: Single achievement entry for panel display.
    /// </summary>
    public struct AchievementEntryUI
    {
        public ushort AchievementId;
        public string Name;
        public string Description;
        public Sprite Icon;
        public AchievementCategory Category;
        public AchievementTier HighestTier;
        public int CurrentValue;
        public int NextThreshold;
        public float ProgressPercent;
        public bool IsHidden;
        public bool IsComplete;
        public AchievementTierRewardUI[] Tiers;
    }

    /// <summary>
    /// EPIC 17.7: Tier reward info for display in the panel.
    /// </summary>
    public struct AchievementTierRewardUI
    {
        public AchievementTier Tier;
        public int Threshold;
        public string RewardText;
        public bool IsUnlocked;
    }
}
