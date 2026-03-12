using System;
using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Per-level base stat scaling configuration.
    /// Defines base MaxHealth, AttackPower, SpellPower, Defense, Armor,
    /// and resource pool base values for each level.
    /// Loaded from Resources/LevelStatScaling by ProgressionBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelStatScaling", menuName = "DIG/Progression/Level Stat Scaling", order = 1)]
    public class LevelStatScalingSO : ScriptableObject
    {
        [Header("Per-Level Stats")]
        [Tooltip("If empty or shorter than MaxLevel, remaining levels use linear interpolation from the formulas below.")]
        public LevelStatEntryData[] StatsPerLevel;

        [Header("Linear Scaling Fallback")]
        [Tooltip("Used for levels not covered by the StatsPerLevel array.")]
        public float BaseMaxHealth = 100f;
        public float MaxHealthPerLevel = 15f;
        public float BaseAttackPower = 5f;
        public float AttackPowerPerLevel = 2f;
        public float BaseSpellPower = 5f;
        public float SpellPowerPerLevel = 2f;
        public float BaseDefense = 2f;
        public float DefensePerLevel = 1f;
        public float BaseArmor = 0f;
        public float ArmorPerLevel = 0.5f;

        [Header("Resource Scaling")]
        public float BaseMaxMana = 50f;
        public float MaxManaPerLevel = 5f;
        public float BaseManaRegen = 2f;
        public float ManaRegenPerLevel = 0.2f;
        public float BaseMaxStamina = 100f;
        public float MaxStaminaPerLevel = 5f;
        public float BaseStaminaRegen = 5f;
        public float StaminaRegenPerLevel = 0.3f;

        /// <summary>
        /// Returns stats for a given level. Uses designer array if available, otherwise linear formula.
        /// </summary>
        public LevelStatEntryData GetStatsForLevel(int level)
        {
            int index = level - 1;
            if (StatsPerLevel != null && index < StatsPerLevel.Length)
                return StatsPerLevel[index];

            return new LevelStatEntryData
            {
                MaxHealth = BaseMaxHealth + MaxHealthPerLevel * index,
                AttackPower = BaseAttackPower + AttackPowerPerLevel * index,
                SpellPower = BaseSpellPower + SpellPowerPerLevel * index,
                Defense = BaseDefense + DefensePerLevel * index,
                Armor = BaseArmor + ArmorPerLevel * index,
                MaxMana = BaseMaxMana + MaxManaPerLevel * index,
                ManaRegen = BaseManaRegen + ManaRegenPerLevel * index,
                MaxStamina = BaseMaxStamina + MaxStaminaPerLevel * index,
                StaminaRegen = BaseStaminaRegen + StaminaRegenPerLevel * index
            };
        }
    }

    /// <summary>
    /// EPIC 16.14: Serializable stat entry for designer-defined per-level values.
    /// </summary>
    [Serializable]
    public struct LevelStatEntryData
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
}
