using System;
using System.Collections.Generic;
using UnityEngine;
using DIG.Targeting.Theming;

namespace DIG.Combat.Definitions
{
    /// <summary>
    /// Configurable damage formula for stat-based combat resolvers.
    /// Uses expression strings that can be parsed at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDamageFormula", menuName = "DIG/Combat/Damage Formula", order = 1)]
    public class DamageFormula : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Human-readable name for this formula")]
        public string FormulaName = "Default Formula";
        
        [Tooltip("Description of when to use this formula")]
        [TextArea(2, 4)]
        public string Description;
        
        [Header("Damage Calculation")]
        [Tooltip("Base damage expression. Variables: WeaponDamage, Strength, AttackPower, etc.")]
        public string BaseDamageExpression = "WeaponDamage * (1 + Strength * 0.02)";
        
        [Tooltip("Critical chance expression. Should evaluate to 0-1.")]
        public string CritChanceExpression = "0.05 + CritRating * 0.001";
        
        [Tooltip("Critical multiplier expression. Typically 1.5+")]
        public string CritMultiplierExpression = "1.5 + CritDamage * 0.01";
        
        [Header("Mitigation")]
        [Tooltip("Damage mitigation expression. Applied after crit.")]
        public string MitigationExpression = "Damage * (100 / (100 + Defense))";
        
        [Tooltip("How resistances are applied")]
        public ResistanceMode ResistanceMode = ResistanceMode.Multiplicative;
        
        [Header("Bounds")]
        [Tooltip("Minimum damage (never goes below this)")]
        public float MinDamage = 1f;
        
        [Tooltip("Maximum damage cap (0 = no cap)")]
        public float MaxDamage = 0f;
        
        [Header("Hit Calculation (for StatBasedRoll)")]
        [Tooltip("Hit chance expression. Should evaluate to 0-1.")]
        public string HitChanceExpression = "0.9 + Accuracy * 0.01 - TargetEvasion * 0.01";
        
        [Tooltip("Minimum hit chance (floor)")]
        [Range(0f, 1f)]
        public float MinHitChance = 0.05f;
        
        [Tooltip("Maximum hit chance (cap)")]
        [Range(0f, 1f)]
        public float MaxHitChance = 0.95f;
        
        [Header("Graze Settings")]
        [Tooltip("Enable graze hits (partial damage on near-miss)")]
        public bool EnableGraze = false;
        
        [Tooltip("How close to hit threshold triggers graze (0.1 = within 10%)")]
        [Range(0f, 0.5f)]
        public float GrazeThreshold = 0.1f;
        
        [Tooltip("Damage multiplier for graze hits")]
        [Range(0f, 1f)]
        public float GrazeDamageMultiplier = 0.5f;
        
        [Header("Elemental Modifiers")]
        [Tooltip("Elemental interaction modifiers (e.g., Fire vs Ice)")]
        public List<ElementalModifier> ElementalModifiers = new List<ElementalModifier>();
        
        [Header("Level Scaling")]
        [Tooltip("Enable level difference scaling")]
        public bool EnableLevelScaling = false;
        
        [Tooltip("Damage bonus/penalty per level difference")]
        public float LevelScalingFactor = 0.03f;
        
        [Tooltip("Maximum level difference for scaling")]
        public int MaxLevelDifference = 10;
        
        /// <summary>
        /// Get elemental modifier for damage type interaction.
        /// Returns 1.0 if no modifier defined.
        /// </summary>
        public float GetElementalModifier(DamageType attackType, DamageType defendType)
        {
            foreach (var mod in ElementalModifiers)
            {
                if (mod.AttackElement == attackType && mod.DefendElement == defendType)
                    return mod.DamageMultiplier;
            }
            return 1f;
        }
        
        /// <summary>
        /// Clamp damage to min/max bounds.
        /// </summary>
        public float ClampDamage(float damage)
        {
            damage = Mathf.Max(damage, MinDamage);
            if (MaxDamage > 0f)
                damage = Mathf.Min(damage, MaxDamage);
            return damage;
        }
        
        /// <summary>
        /// Clamp hit chance to min/max bounds.
        /// </summary>
        public float ClampHitChance(float hitChance)
        {
            return Mathf.Clamp(hitChance, MinHitChance, MaxHitChance);
        }
        
        /// <summary>
        /// Calculate level scaling multiplier.
        /// </summary>
        public float GetLevelScalingMultiplier(int attackerLevel, int targetLevel)
        {
            if (!EnableLevelScaling)
                return 1f;
                
            int diff = Mathf.Clamp(attackerLevel - targetLevel, -MaxLevelDifference, MaxLevelDifference);
            return 1f + (diff * LevelScalingFactor);
        }
    }
    
    /// <summary>
    /// Defines how elemental resistances reduce damage.
    /// </summary>
    public enum ResistanceMode
    {
        /// <summary>
        /// FinalDamage = Damage * (1 - Resistance)
        /// </summary>
        Multiplicative,
        
        /// <summary>
        /// FinalDamage = Damage - FlatResistance
        /// </summary>
        Flat,
        
        /// <summary>
        /// FinalDamage = Damage * (100 / (100 + Resistance))
        /// Diminishing returns formula.
        /// </summary>
        DiminishingReturns
    }
    
    /// <summary>
    /// Elemental interaction modifier.
    /// </summary>
    [Serializable]
    public struct ElementalModifier
    {
        [Tooltip("Element of the attack")]
        public DamageType AttackElement;
        
        [Tooltip("Element the target is resistant/weak to")]
        public DamageType DefendElement;
        
        [Tooltip("Damage multiplier (>1 = bonus damage, <1 = reduced)")]
        public float DamageMultiplier;
    }
}
