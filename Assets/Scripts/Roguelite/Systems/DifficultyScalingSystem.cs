using Unity.Burst;
using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Every frame, aggregates base zone difficulty curve + RunModifierStack
    /// + ascension reward multiplier into RuntimeDifficultyScale singleton.
    /// Game systems read RuntimeDifficultyScale for enemy spawning, loot drops, XP grants, etc.
    ///
    /// StatId conventions for EnemyStat target:  0=HealthScale, 1=DamageScale, 2=SpawnRate
    /// StatId conventions for Economy target:    0=LootQty, 1=LootQuality, 2=XP, 3=Currency
    /// StatId conventions for Encounter target:  0=SpawnRate
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ModifierAcquisitionSystem))]
    [BurstCompile]
    public partial struct DifficultyScalingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunState>();
            state.RequireForUpdate<RunConfigSingleton>();
            state.RequireForUpdate<RuntimeDifficultyScale>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var run = SystemAPI.GetSingleton<RunState>();
            ref var config = ref SystemAPI.GetSingleton<RunConfigSingleton>().Config.Value;

            // Start from defaults
            var scale = RuntimeDifficultyScale.Default;

            // Apply zone difficulty curve from RunConfig
            float zoneDifficulty = 1f;
            if (run.CurrentZoneIndex < config.DifficultyMultiplierPerZone.Length)
                zoneDifficulty = config.DifficultyMultiplierPerZone[run.CurrentZoneIndex];

            scale.ZoneDifficultyMultiplier = zoneDifficulty;
            scale.EnemyHealthScale = zoneDifficulty;
            scale.EnemyDamageScale = zoneDifficulty;

            // Apply ascension reward multiplier cumulatively (matches AscensionSetupSystem)
            if (SystemAPI.HasSingleton<AscensionSingleton>())
            {
                ref var ascension = ref SystemAPI.GetSingleton<AscensionSingleton>().Ascension.Value;
                float cumulativeReward = 1f;
                for (int i = 0; i < ascension.Tiers.Length; i++)
                {
                    if (ascension.Tiers[i].Level <= run.AscensionLevel)
                        cumulativeReward *= ascension.Tiers[i].RewardMultiplier;
                }
                scale.AscensionRewardMultiplier = cumulativeReward;
            }

            // Apply run modifiers from stack
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            if (SystemAPI.HasBuffer<RunModifierStack>(runEntity))
            {
                var modifiers = SystemAPI.GetBuffer<RunModifierStack>(runEntity);
                for (int i = 0; i < modifiers.Length; i++)
                {
                    var mod = modifiers[i];
                    ApplyModifier(ref scale, in mod);
                }
            }

            // Apply ascension reward multiplier to reward fields
            scale.XPMultiplier *= scale.AscensionRewardMultiplier;
            scale.CurrencyMultiplier *= scale.AscensionRewardMultiplier;

            SystemAPI.SetSingleton(scale);
        }

        private static void ApplyModifier(ref RuntimeDifficultyScale scale, in RunModifierStack mod)
        {
            switch (mod.Target)
            {
                case ModifierTarget.EnemyStat:
                    ApplyEnemyModifier(ref scale, mod.StatId, mod.EffectiveValue, mod.IsMultiplicative);
                    break;
                case ModifierTarget.Economy:
                    ApplyEconomyModifier(ref scale, mod.StatId, mod.EffectiveValue, mod.IsMultiplicative);
                    break;
                case ModifierTarget.Encounter:
                    ApplyEncounterModifier(ref scale, mod.StatId, mod.EffectiveValue, mod.IsMultiplicative);
                    break;
                // PlayerStat and RunMechanic are handled by game-side bridge systems
            }
        }

        private static void ApplyEnemyModifier(ref RuntimeDifficultyScale scale, int statId, float value, bool multiplicative)
        {
            switch (statId)
            {
                case 0: // EnemyHealthScale
                    scale.EnemyHealthScale = multiplicative ? scale.EnemyHealthScale * value : scale.EnemyHealthScale + value;
                    break;
                case 1: // EnemyDamageScale
                    scale.EnemyDamageScale = multiplicative ? scale.EnemyDamageScale * value : scale.EnemyDamageScale + value;
                    break;
                case 2: // EnemySpawnRateScale
                    scale.EnemySpawnRateScale = multiplicative ? scale.EnemySpawnRateScale * value : scale.EnemySpawnRateScale + value;
                    break;
            }
        }

        private static void ApplyEconomyModifier(ref RuntimeDifficultyScale scale, int statId, float value, bool multiplicative)
        {
            switch (statId)
            {
                case 0: // LootQuantityScale
                    scale.LootQuantityScale = multiplicative ? scale.LootQuantityScale * value : scale.LootQuantityScale + value;
                    break;
                case 1: // LootQualityBonus
                    scale.LootQualityBonus = multiplicative ? scale.LootQualityBonus * value : scale.LootQualityBonus + value;
                    break;
                case 2: // XPMultiplier
                    scale.XPMultiplier = multiplicative ? scale.XPMultiplier * value : scale.XPMultiplier + value;
                    break;
                case 3: // CurrencyMultiplier
                    scale.CurrencyMultiplier = multiplicative ? scale.CurrencyMultiplier * value : scale.CurrencyMultiplier + value;
                    break;
            }
        }

        private static void ApplyEncounterModifier(ref RuntimeDifficultyScale scale, int statId, float value, bool multiplicative)
        {
            switch (statId)
            {
                case 0: // EnemySpawnRateScale
                    scale.EnemySpawnRateScale = multiplicative ? scale.EnemySpawnRateScale * value : scale.EnemySpawnRateScale + value;
                    break;
            }
        }
    }
}
