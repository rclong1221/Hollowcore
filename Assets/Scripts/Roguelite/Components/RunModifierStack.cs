using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Polarity of a run modifier — positive (helps player), negative (hurts), or neutral.
    /// </summary>
    public enum ModifierPolarity : byte
    {
        Positive = 0,
        Negative = 1,
        Neutral = 2
    }

    /// <summary>
    /// EPIC 23.4: What a modifier targets. Determines which scaling fields are affected.
    /// PlayerStat and RunMechanic are handled by game-side bridge systems.
    /// EnemyStat, Economy, and Encounter are handled by DifficultyScalingSystem.
    /// </summary>
    public enum ModifierTarget : byte
    {
        PlayerStat = 0,     // Game-side: stat bonuses via bridge system
        EnemyStat = 1,      // Framework: EnemyHealthScale, EnemyDamageScale, EnemySpawnRateScale
        RunMechanic = 2,    // Game-side: custom run mechanics via bridge system
        Economy = 3,        // Framework: LootQuantityScale, LootQualityBonus, XPMultiplier, CurrencyMultiplier
        Encounter = 4       // Framework: EnemySpawnRateScale, encounter composition
    }

    /// <summary>
    /// EPIC 23.4: Active modifier entry on the RunState entity.
    /// InternalBufferCapacity=16 keeps typical modifier counts inline (avoids heap allocation).
    /// DifficultyScalingSystem reads this every frame to produce RuntimeDifficultyScale.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct RunModifierStack : IBufferElementData
    {
        public int ModifierId;
        public byte StackCount;
        public ModifierTarget Target;
        public int StatId;
        public float EffectiveValue;       // FloatValue × StackCount (pre-computed)
        public bool IsMultiplicative;
    }

    /// <summary>
    /// EPIC 23.4: Signal to add a modifier to the stack.
    /// Baked disabled on RunState entity. UI or reward systems enable this with a ModifierId.
    /// ModifierAcquisitionSystem processes and disables it.
    /// </summary>
    public struct ModifierAcquisitionRequest : IComponentData, IEnableableComponent
    {
        public int ModifierId;
    }

    /// <summary>
    /// EPIC 23.4: Modifier choices offered to the player (e.g., on zone transition).
    /// ModifierAcquisitionSystem populates this; game UI reads it via IModifierUIProvider.
    /// Cleared after the player selects or skips.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct PendingModifierChoice : IBufferElementData
    {
        public int ModifierId;
        public ModifierPolarity Polarity;
        public ModifierTarget Target;
        public int StatId;
        public float FloatValue;
        public bool IsMultiplicative;
    }
}
