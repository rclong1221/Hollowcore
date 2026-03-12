using System.Collections.Generic;
using DIG.Combat.Resolvers;
using DIG.Combat.Definitions;
using DIG.Targeting.Theming;

namespace DIG.Combat.Formulas
{
    /// <summary>
    /// Evaluates DamageFormula ScriptableObjects using FormulaParser.
    /// Bridges between combat context and formula expressions.
    /// </summary>
    public class FormulaEvaluator
    {
        private readonly FormulaParser _parser = new();
        
        /// <summary>
        /// Populate parser variables from combat context.
        /// </summary>
        public void SetContextVariables(in CombatContext context)
        {
            _parser.ClearVariables();
            
            // Attacker stats
            _parser.SetVariable("Strength", context.AttackerStats.Strength);
            _parser.SetVariable("Dexterity", context.AttackerStats.Dexterity);
            _parser.SetVariable("Intelligence", context.AttackerStats.Intelligence);
            _parser.SetVariable("AttackPower", context.AttackerStats.AttackPower);
            _parser.SetVariable("SpellPower", context.AttackerStats.SpellPower);
            _parser.SetVariable("CritChance", context.AttackerStats.CritChance);
            _parser.SetVariable("CritMultiplier", context.AttackerStats.CritMultiplier);
            _parser.SetVariable("CritRating", context.AttackerStats.CritChance * 100f);
            _parser.SetVariable("CritDamage", (context.AttackerStats.CritMultiplier - 1f) * 100f);
            _parser.SetVariable("Accuracy", context.AttackerStats.Accuracy);
            _parser.SetVariable("Level", context.AttackerStats.Level);
            
            // Weapon stats
            _parser.SetVariable("WeaponDamage", context.WeaponData.BaseDamage);
            _parser.SetVariable("WeaponDamageMin", context.WeaponData.DamageMin);
            _parser.SetVariable("WeaponDamageMax", context.WeaponData.DamageMax);
            _parser.SetVariable("AttackSpeed", context.WeaponData.AttackSpeed);
            _parser.SetVariable("ElementType", (float)context.WeaponData.DamageType);
            
            // Target stats
            _parser.SetVariable("Defense", context.TargetStats.Defense);
            _parser.SetVariable("TargetDefense", context.TargetStats.Defense);
            _parser.SetVariable("Armor", context.TargetStats.Armor);
            _parser.SetVariable("Evasion", context.TargetStats.Evasion);
            _parser.SetVariable("TargetEvasion", context.TargetStats.Evasion);
            _parser.SetVariable("TargetLevel", context.TargetStats.Level);
            _parser.SetVariable("HealthPercent", context.TargetStats.HealthPercent);
            _parser.SetVariable("TargetHealthPercent", context.TargetStats.HealthPercent);
            
            // Resistance for current damage type
            _parser.SetVariable("Resistance", context.TargetStats.GetResistance(context.WeaponData.DamageType));
            
            // Distance
            _parser.SetVariable("Distance", context.HitDistance);
        }
        
        /// <summary>
        /// Evaluate base damage from formula.
        /// </summary>
        public float EvaluateBaseDamage(DamageFormula formula, in CombatContext context)
        {
            SetContextVariables(in context);
            return _parser.Evaluate(formula.BaseDamageExpression);
        }
        
        /// <summary>
        /// Evaluate critical hit chance from formula.
        /// </summary>
        public float EvaluateCritChance(DamageFormula formula, in CombatContext context)
        {
            SetContextVariables(in context);
            float critChance = _parser.Evaluate(formula.CritChanceExpression);
            return UnityEngine.Mathf.Clamp01(critChance);
        }
        
        /// <summary>
        /// Evaluate critical hit multiplier from formula.
        /// </summary>
        public float EvaluateCritMultiplier(DamageFormula formula, in CombatContext context)
        {
            SetContextVariables(in context);
            float critMult = _parser.Evaluate(formula.CritMultiplierExpression);
            return UnityEngine.Mathf.Max(1f, critMult);
        }
        
        /// <summary>
        /// Evaluate hit chance from formula (for StatBasedRoll resolver).
        /// </summary>
        public float EvaluateHitChance(DamageFormula formula, in CombatContext context)
        {
            SetContextVariables(in context);
            float hitChance = _parser.Evaluate(formula.HitChanceExpression);
            return formula.ClampHitChance(hitChance);
        }
        
        /// <summary>
        /// Evaluate mitigation (apply defense reduction).
        /// Sets "Damage" variable for use in mitigation expression.
        /// </summary>
        public float EvaluateMitigation(DamageFormula formula, float rawDamage, in CombatContext context)
        {
            SetContextVariables(in context);
            _parser.SetVariable("Damage", rawDamage);
            
            float mitigated = _parser.Evaluate(formula.MitigationExpression);
            return formula.ClampDamage(mitigated);
        }
        
        /// <summary>
        /// Apply elemental resistance based on formula settings.
        /// </summary>
        public float ApplyResistance(DamageFormula formula, float damage, DamageType damageType, in CombatContext context)
        {
            float resistance = context.TargetStats.GetResistance(damageType);
            
            return formula.ResistanceMode switch
            {
                ResistanceMode.Multiplicative => damage * (1f - resistance),
                ResistanceMode.Flat => UnityEngine.Mathf.Max(0f, damage - resistance),
                ResistanceMode.DiminishingReturns => damage * (100f / (100f + resistance * 100f)),
                _ => damage
            };
        }
        
        /// <summary>
        /// Full damage calculation using formula.
        /// </summary>
        public float CalculateFullDamage(DamageFormula formula, in CombatContext context, bool isCrit)
        {
            // Base damage
            float damage = EvaluateBaseDamage(formula, in context);
            
            // Apply crit
            if (isCrit)
            {
                float critMult = EvaluateCritMultiplier(formula, in context);
                damage *= critMult;
            }
            
            // Apply level scaling
            if (formula.EnableLevelScaling)
            {
                float levelMult = formula.GetLevelScalingMultiplier(
                    context.AttackerStats.Level, 
                    context.TargetStats.Level);
                damage *= levelMult;
            }
            
            // Apply mitigation
            damage = EvaluateMitigation(formula, damage, in context);
            
            // Apply resistance
            damage = ApplyResistance(formula, damage, context.WeaponData.DamageType, in context);
            
            // Apply elemental modifier
            // (Would need target's elemental affinity for full implementation)
            
            return formula.ClampDamage(damage);
        }
        
        /// <summary>
        /// Validate a formula's expressions.
        /// </summary>
        public bool ValidateFormula(DamageFormula formula, out List<string> errors)
        {
            errors = new List<string>();
            
            if (!_parser.Validate(formula.BaseDamageExpression, out string error))
                errors.Add($"Base Damage: {error}");
            
            if (!_parser.Validate(formula.CritChanceExpression, out error))
                errors.Add($"Crit Chance: {error}");
            
            if (!_parser.Validate(formula.CritMultiplierExpression, out error))
                errors.Add($"Crit Multiplier: {error}");
            
            if (!_parser.Validate(formula.MitigationExpression, out error))
                errors.Add($"Mitigation: {error}");
            
            if (!string.IsNullOrEmpty(formula.HitChanceExpression))
            {
                if (!_parser.Validate(formula.HitChanceExpression, out error))
                    errors.Add($"Hit Chance: {error}");
            }
            
            return errors.Count == 0;
        }
    }
}
