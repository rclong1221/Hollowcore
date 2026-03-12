using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Singleton with all difficulty fields after zone curve + run modifiers are applied.
    /// Game systems read this instead of raw DifficultyDefinitionSO values.
    /// Recalculated every frame by DifficultyScalingSystem.
    ///
    /// Fields mirror DifficultyDefinitionSO but with modifiers applied on top of zone curve base.
    /// NOT on a ghost prefab entity — [GhostComponent] would be dead weight. For multiplayer,
    /// game-side bridges replicate difficulty via RPC or a ghost-prefab component.
    /// </summary>
    public struct RuntimeDifficultyScale : IComponentData
    {
        // Enemy scaling (zone curve × modifiers)
        public float EnemyHealthScale;
        public float EnemyDamageScale;
        public float EnemySpawnRateScale;

        // Loot scaling
        public float LootQuantityScale;
        public float LootQualityBonus;

        // Reward scaling
        public float XPMultiplier;
        public float CurrencyMultiplier;

        // Composite zone difficulty (curve value × all modifiers)
        public float ZoneDifficultyMultiplier;

        // Ascension reward bonus (from AscensionDefinitionSO tier)
        public float AscensionRewardMultiplier;

        public static RuntimeDifficultyScale Default => new RuntimeDifficultyScale
        {
            EnemyHealthScale = 1f,
            EnemyDamageScale = 1f,
            EnemySpawnRateScale = 1f,
            LootQuantityScale = 1f,
            LootQualityBonus = 0f,
            XPMultiplier = 1f,
            CurrencyMultiplier = 1f,
            ZoneDifficultyMultiplier = 1f,
            AscensionRewardMultiplier = 1f
        };
    }
}
