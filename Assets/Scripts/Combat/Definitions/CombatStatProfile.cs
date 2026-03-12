using System;
using UnityEngine;

namespace DIG.Combat.Definitions
{
    /// <summary>
    /// Defines base combat stats for a character class or enemy type.
    /// Used by stat initialization system to set starting values.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatProfile", menuName = "DIG/Combat/Stat Profile", order = 2)]
    public class CombatStatProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this profile")]
        public string ProfileID;
        
        [Tooltip("Display name for UI")]
        public string DisplayName;
        
        [Header("Base Offensive Stats")]
        [Tooltip("Starting attack power")]
        public float BaseAttackPower = 0f;
        
        [Tooltip("Starting spell power")]
        public float BaseSpellPower = 0f;
        
        [Tooltip("Starting crit chance (0-1)")]
        [Range(0f, 1f)]
        public float BaseCritChance = 0.05f;
        
        [Tooltip("Starting crit multiplier")]
        [Min(1f)]
        public float BaseCritMultiplier = 1.5f;
        
        [Tooltip("Starting accuracy")]
        [Min(0f)]
        public float BaseAccuracy = 1f;
        
        [Header("Base Defensive Stats")]
        [Tooltip("Starting defense")]
        [Min(0f)]
        public float BaseDefense = 0f;
        
        [Tooltip("Starting armor")]
        [Min(0f)]
        public float BaseArmor = 0f;
        
        [Tooltip("Starting evasion (0-1)")]
        [Range(0f, 1f)]
        public float BaseEvasion = 0f;
        
        [Header("Base Attributes")]
        [Min(1)] public int BaseStrength = 10;
        [Min(1)] public int BaseDexterity = 10;
        [Min(1)] public int BaseIntelligence = 10;
        [Min(1)] public int BaseVitality = 10;
        
        [Header("Per-Level Scaling")]
        [Tooltip("Attack power gained per level")]
        public float AttackPowerPerLevel = 1f;
        
        [Tooltip("Spell power gained per level")]
        public float SpellPowerPerLevel = 1f;
        
        [Tooltip("Defense gained per level")]
        public float DefensePerLevel = 0.5f;
        
        [Header("Elemental Resistances")]
        public ElementalResistanceSet BaseResistances;
        
        /// <summary>
        /// Calculate attack stats for a given level.
        /// </summary>
        public Components.AttackStats GetAttackStatsForLevel(int level)
        {
            return new Components.AttackStats
            {
                AttackPower = BaseAttackPower + (AttackPowerPerLevel * (level - 1)),
                SpellPower = BaseSpellPower + (SpellPowerPerLevel * (level - 1)),
                CritChance = BaseCritChance,
                CritMultiplier = BaseCritMultiplier,
                Accuracy = BaseAccuracy
            };
        }
        
        /// <summary>
        /// Calculate defense stats for a given level.
        /// </summary>
        public Components.DefenseStats GetDefenseStatsForLevel(int level)
        {
            return new Components.DefenseStats
            {
                Defense = BaseDefense + (DefensePerLevel * (level - 1)),
                Armor = BaseArmor,
                Evasion = BaseEvasion
            };
        }
        
        /// <summary>
        /// Get elemental resistances.
        /// </summary>
        public Components.ElementalResistances GetElementalResistances()
        {
            return new Components.ElementalResistances
            {
                Physical = BaseResistances.Physical,
                Fire = BaseResistances.Fire,
                Ice = BaseResistances.Ice,
                Lightning = BaseResistances.Lightning,
                Poison = BaseResistances.Poison,
                Holy = BaseResistances.Holy,
                Shadow = BaseResistances.Shadow,
                Arcane = BaseResistances.Arcane
            };
        }
        
        /// <summary>
        /// Get character attributes for a given level.
        /// </summary>
        public Components.CharacterAttributes GetAttributesForLevel(int level)
        {
            return new Components.CharacterAttributes
            {
                Strength = BaseStrength,
                Dexterity = BaseDexterity,
                Intelligence = BaseIntelligence,
                Vitality = BaseVitality,
                Level = level
            };
        }
    }
    
    /// <summary>
    /// Serializable resistance set for ScriptableObject.
    /// </summary>
    [Serializable]
    public struct ElementalResistanceSet
    {
        [Range(0f, 1f)] public float Physical;
        [Range(0f, 1f)] public float Fire;
        [Range(0f, 1f)] public float Ice;
        [Range(0f, 1f)] public float Lightning;
        [Range(0f, 1f)] public float Poison;
        [Range(0f, 1f)] public float Holy;
        [Range(0f, 1f)] public float Shadow;
        [Range(0f, 1f)] public float Arcane;
    }
}
