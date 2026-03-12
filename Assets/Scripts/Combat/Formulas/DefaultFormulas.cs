using UnityEngine;
using DIG.Combat.Definitions;

namespace DIG.Combat.Formulas
{
    /// <summary>
    /// Provides default DamageFormula presets for common game types.
    /// Use these as starting points or create custom formulas.
    /// </summary>
    public static class DefaultFormulas
    {
        /// <summary>
        /// Create a DIG-style simple formula (pure weapon damage, no stats).
        /// </summary>
        public static DamageFormula CreateDIGSimple()
        {
            var formula = ScriptableObject.CreateInstance<DamageFormula>();
            formula.FormulaName = "DIG Simple";
            formula.Description = "Pure weapon damage, no stat scaling. For physics-based action combat.";
            
            formula.BaseDamageExpression = "WeaponDamage";
            formula.CritChanceExpression = "0";
            formula.CritMultiplierExpression = "1";
            formula.MitigationExpression = "Damage";
            formula.HitChanceExpression = "1";
            
            formula.MinDamage = 1f;
            formula.MaxDamage = 0f;
            formula.EnableLevelScaling = false;
            formula.EnableGraze = false;
            
            return formula;
        }
        
        /// <summary>
        /// Create an ARPG-style standard formula (stat scaling, crits, defense).
        /// </summary>
        public static DamageFormula CreateARPGStandard()
        {
            var formula = ScriptableObject.CreateInstance<DamageFormula>();
            formula.FormulaName = "ARPG Standard";
            formula.Description = "Stat-based damage with attack power scaling, crits, and defense mitigation.";
            
            formula.BaseDamageExpression = "WeaponDamage * (1 + AttackPower / 100)";
            formula.CritChanceExpression = "CritChance";
            formula.CritMultiplierExpression = "CritMultiplier";
            formula.MitigationExpression = "Damage * (100 / (100 + Defense))";
            formula.HitChanceExpression = "1";
            
            formula.MinDamage = 1f;
            formula.MaxDamage = 0f;
            formula.EnableLevelScaling = false;
            formula.EnableGraze = false;
            
            formula.ResistanceMode = ResistanceMode.Multiplicative;
            
            return formula;
        }
        
        /// <summary>
        /// Create a tactical ARPG formula (accuracy rolls, graze, full stat interaction).
        /// </summary>
        public static DamageFormula CreateARPGTactical()
        {
            var formula = ScriptableObject.CreateInstance<DamageFormula>();
            formula.FormulaName = "ARPG Tactical";
            formula.Description = "Full tactical combat with accuracy vs evasion, graze hits, and level scaling.";
            
            formula.BaseDamageExpression = "WeaponDamage * (1 + AttackPower / 100) * (1 + Strength * 0.02)";
            formula.CritChanceExpression = "0.05 + CritRating * 0.001";
            formula.CritMultiplierExpression = "1.5 + CritDamage * 0.01";
            formula.MitigationExpression = "Damage * (100 / (100 + Defense + Armor))";
            formula.HitChanceExpression = "0.9 + Accuracy * 0.01 - TargetEvasion";
            
            formula.MinDamage = 1f;
            formula.MaxDamage = 0f;
            formula.MinHitChance = 0.05f;
            formula.MaxHitChance = 0.95f;
            
            formula.EnableGraze = true;
            formula.GrazeThreshold = 0.1f;
            formula.GrazeDamageMultiplier = 0.5f;
            
            formula.EnableLevelScaling = true;
            formula.LevelScalingFactor = 0.03f;
            formula.MaxLevelDifference = 10;
            
            formula.ResistanceMode = ResistanceMode.DiminishingReturns;
            
            return formula;
        }
        
        /// <summary>
        /// Create a hybrid formula (physics hit + stat damage).
        /// </summary>
        public static DamageFormula CreateHybrid()
        {
            var formula = ScriptableObject.CreateInstance<DamageFormula>();
            formula.FormulaName = "Hybrid (Physics + Stats)";
            formula.Description = "Requires physics hit, then applies stat-based damage calculation.";
            
            formula.BaseDamageExpression = "WeaponDamage * (1 + AttackPower / 100) * (1 + Strength * 0.01)";
            formula.CritChanceExpression = "CritChance";
            formula.CritMultiplierExpression = "CritMultiplier";
            formula.MitigationExpression = "Damage * (100 / (100 + Defense))";
            formula.HitChanceExpression = "1"; // Physics determines hit
            
            formula.MinDamage = 1f;
            formula.MaxDamage = 0f;
            formula.EnableLevelScaling = false;
            formula.EnableGraze = false;
            
            formula.ResistanceMode = ResistanceMode.Multiplicative;
            
            return formula;
        }
        
        /// <summary>
        /// Create a spell-caster formula (intelligence scaling, spell power).
        /// </summary>
        public static DamageFormula CreateSpellcaster()
        {
            var formula = ScriptableObject.CreateInstance<DamageFormula>();
            formula.FormulaName = "Spellcaster";
            formula.Description = "Magic damage scaling with intelligence and spell power.";
            
            formula.BaseDamageExpression = "WeaponDamage * (1 + SpellPower / 100) * (1 + Intelligence * 0.03)";
            formula.CritChanceExpression = "CritChance";
            formula.CritMultiplierExpression = "CritMultiplier";
            formula.MitigationExpression = "Damage * (1 - Resistance)";
            formula.HitChanceExpression = "1";
            
            formula.MinDamage = 1f;
            formula.MaxDamage = 0f;
            formula.EnableLevelScaling = true;
            formula.LevelScalingFactor = 0.05f;
            
            formula.ResistanceMode = ResistanceMode.Multiplicative;
            
            return formula;
        }
        
        /// <summary>
        /// Create an execute formula (bonus damage at low health).
        /// </summary>
        public static DamageFormula CreateExecute()
        {
            var formula = ScriptableObject.CreateInstance<DamageFormula>();
            formula.FormulaName = "Execute";
            formula.Description = "Deals bonus damage to low-health targets.";
            
            // Damage increases as target health decreases (up to 2x at 0% HP)
            formula.BaseDamageExpression = "WeaponDamage * (1 + AttackPower / 100) * (2 - TargetHealthPercent)";
            formula.CritChanceExpression = "CritChance";
            formula.CritMultiplierExpression = "CritMultiplier";
            formula.MitigationExpression = "Damage * (100 / (100 + Defense))";
            formula.HitChanceExpression = "1";
            
            formula.MinDamage = 1f;
            formula.MaxDamage = 0f;
            
            return formula;
        }
    }
}
