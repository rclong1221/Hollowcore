namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Achievement category for UI grouping and filtering.
    /// </summary>
    public enum AchievementCategory : byte
    {
        Combat      = 0,
        Exploration = 1,
        Crafting    = 2,
        Social      = 3,
        Collection  = 4,
        Progression = 5,
        Challenge   = 6
    }

    /// <summary>
    /// EPIC 17.7: What game event triggers progress for an achievement.
    /// </summary>
    public enum AchievementConditionType : byte
    {
        EnemyKill           = 0,
        EnemyKillByType     = 1,
        QuestComplete       = 2,
        LevelReached        = 3,
        ItemCrafted         = 4,
        ItemCraftedByRecipe = 5,
        LootCollected       = 6,
        LootByRarity        = 7,
        DamageDealt         = 8,
        PlayerDeath         = 9,
        KillStreak          = 10,
        NPCInteraction      = 11,
        GoldEarned          = 12,
        DialogueComplete    = 13,
        SurvivalTime        = 14,
        BossKill            = 15,
        CraftRareItem       = 16,
        ReachZone           = 17,
        StatPointsAllocated = 18,
        TalentPointsSpent   = 19
    }

    /// <summary>
    /// EPIC 17.7: Achievement tier levels with escalating thresholds and rewards.
    /// </summary>
    public enum AchievementTier : byte
    {
        None     = 0,
        Bronze   = 1,
        Silver   = 2,
        Gold     = 3,
        Platinum = 4
    }

    /// <summary>
    /// EPIC 17.7: Reward type granted on achievement tier unlock.
    /// </summary>
    public enum AchievementRewardType : byte
    {
        Gold          = 0,
        XP            = 1,
        Title         = 2,
        Cosmetic      = 3,
        TalentPoints  = 4,
        RecipeUnlock  = 5,
        StatBonus     = 6
    }
}
