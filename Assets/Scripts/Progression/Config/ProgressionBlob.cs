using Unity.Entities;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Singleton holding BlobAsset references for all progression configuration.
    /// Created by ProgressionBootstrapSystem from Resources/ ScriptableObjects.
    /// </summary>
    public struct ProgressionConfigSingleton : IComponentData
    {
        public BlobAssetReference<ProgressionBlob> Curve;
        public BlobAssetReference<LevelStatScalingBlob> StatScaling;
        public BlobAssetReference<LevelRewardsBlob> Rewards;
    }

    /// <summary>
    /// EPIC 16.14: XP curve and formula parameters baked from ProgressionCurveSO.
    /// </summary>
    public struct ProgressionBlob
    {
        public int MaxLevel;
        public int StatPointsPerLevel;

        // Kill XP formula
        public float BaseKillXP;
        public float KillXPPerEnemyLevel;

        // Diminishing returns
        public int DiminishStartDelta;
        public float DiminishFactorPerLevel;
        public float DiminishFloor;

        // Other XP sources
        public float QuestXPBase;
        public float CraftXPBase;
        public float ExplorationXPBase;
        public float InteractionXPBase;

        // Rested XP
        public float RestedXPMultiplier;
        public float RestedXPAccumRatePerHour;
        public float RestedXPMaxDays;

        /// <summary>XP required per level. Index 0 = XP for level 1→2, etc.</summary>
        public BlobArray<int> XPPerLevel;
    }

    /// <summary>
    /// EPIC 16.14: Per-level base stat values baked from LevelStatScalingSO.
    /// </summary>
    public struct LevelStatScalingBlob
    {
        /// <summary>Index 0 = level 1 stats, etc. Length = MaxLevel.</summary>
        public BlobArray<LevelStatEntry> StatsPerLevel;
    }

    /// <summary>
    /// EPIC 16.14: Base stats for a single level.
    /// </summary>
    public struct LevelStatEntry
    {
        public float MaxHealth;
        public float AttackPower;
        public float SpellPower;
        public float Defense;
        public float Armor;
        public float MaxMana;
        public float ManaRegen;
        public float MaxStamina;
        public float StaminaRegen;
    }

    /// <summary>
    /// EPIC 16.14: Per-level rewards baked from LevelRewardsSO.
    /// </summary>
    public struct LevelRewardsBlob
    {
        public BlobArray<LevelRewardEntry> Rewards;
    }

    /// <summary>
    /// EPIC 16.14: Reward type for level-up rewards.
    /// </summary>
    public enum LevelRewardType : byte
    {
        StatPoints = 0,
        CurrencyGold = 1,
        RecipeUnlock = 2,
        AbilityUnlock = 3,
        ContentGate = 4,
        ResourceMaxUp = 5,
        TalentPoint = 6,
        Title = 7
    }

    /// <summary>
    /// EPIC 16.14: A single reward entry for a specific level.
    /// </summary>
    public struct LevelRewardEntry
    {
        public int Level;
        public LevelRewardType RewardType;
        public int IntValue;
        public float FloatValue;
    }
}
