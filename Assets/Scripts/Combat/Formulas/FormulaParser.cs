using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DIG.Combat.Formulas
{
    /// <summary>
    /// Parses and evaluates damage formula expressions.
    /// Supports basic math operations and variable substitution.
    /// </summary>
    public class FormulaParser
    {
        private readonly Dictionary<string, float> _variables = new();
        private static readonly Regex VariablePattern = new(@"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
        
        /// <summary>
        /// Set a variable value for expression evaluation.
        /// </summary>
        public void SetVariable(string name, float value)
        {
            _variables[name] = value;
        }
        
        /// <summary>
        /// Set multiple variables at once.
        /// </summary>
        public void SetVariables(Dictionary<string, float> variables)
        {
            foreach (var kvp in variables)
            {
                _variables[kvp.Key] = kvp.Value;
            }
        }
        
        /// <summary>
        /// Clear all variables.
        /// </summary>
        public void ClearVariables()
        {
            _variables.Clear();
        }
        
        /// <summary>
        /// Evaluate an expression string with current variables.
        /// </summary>
        /// <param name="expression">Expression like "WeaponDamage * (1 + Strength * 0.02)"</param>
        /// <returns>Evaluated result</returns>
        public float Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0f;
            
            try
            {
                // Substitute variables with their values
                string substituted = SubstituteVariables(expression);
                
                // Parse and evaluate the math expression
                return EvaluateMathExpression(substituted);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FormulaParser] Failed to evaluate '{expression}': {e.Message}");
                return 0f;
            }
        }
        
        /// <summary>
        /// Validate an expression without evaluating.
        /// Returns true if expression is syntactically valid.
        /// </summary>
        public bool Validate(string expression, out string error)
        {
            error = null;
            
            if (string.IsNullOrWhiteSpace(expression))
            {
                error = "Expression is empty";
                return false;
            }
            
            // Check for balanced parentheses
            int depth = 0;
            foreach (char c in expression)
            {
                if (c == '(') depth++;
                if (c == ')') depth--;
                if (depth < 0)
                {
                    error = "Unbalanced parentheses";
                    return false;
                }
            }
            if (depth != 0)
            {
                error = "Unbalanced parentheses";
                return false;
            }
            
            // Check for unknown variables
            var matches = VariablePattern.Matches(expression);
            foreach (Match match in matches)
            {
                string varName = match.Value;
                if (!IsKnownVariable(varName) && !float.TryParse(varName, out _))
                {
                    error = $"Unknown variable: {varName}";
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Get list of variables used in an expression.
        /// </summary>
        public List<string> GetVariablesInExpression(string expression)
        {
            var result = new List<string>();
            var matches = VariablePattern.Matches(expression);
            
            foreach (Match match in matches)
            {
                string varName = match.Value;
                if (IsKnownVariable(varName) && !result.Contains(varName))
                {
                    result.Add(varName);
                }
            }
            
            return result;
        }
        
        // ========== PRIVATE METHODS ==========
        
        private string SubstituteVariables(string expression)
        {
            return VariablePattern.Replace(expression, match =>
            {
                string varName = match.Value;
                
                // Check if it's a known variable
                if (_variables.TryGetValue(varName, out float value))
                {
                    return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                
                // Check special variables
                if (varName == "Random")
                {
                    return UnityEngine.Random.value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                
                // Unknown variable, return as-is (will fail in math eval)
                return varName;
            });
        }
        
        private float EvaluateMathExpression(string expression)
        {
            // Remove whitespace
            expression = expression.Replace(" ", "");
            
            // Handle empty expression
            if (string.IsNullOrEmpty(expression))
                return 0f;
            
            return ParseAddSubtract(expression, ref _parseIndex);
        }
        
        private int _parseIndex;
        
        private float ParseAddSubtract(string expr, ref int index)
        {
            index = 0;
            return ParseAddSubtractInternal(expr);
        }
        
        private float ParseAddSubtractInternal(string expr)
        {
            float left = ParseMultiplyDivide(expr);
            
            while (_parseIndex < expr.Length)
            {
                char op = expr[_parseIndex];
                if (op != '+' && op != '-')
                    break;
                
                _parseIndex++;
                float right = ParseMultiplyDivide(expr);
                
                left = op == '+' ? left + right : left - right;
            }
            
            return left;
        }
        
        private float ParseMultiplyDivide(string expr)
        {
            float left = ParseUnary(expr);
            
            while (_parseIndex < expr.Length)
            {
                char op = expr[_parseIndex];
                if (op != '*' && op != '/')
                    break;
                
                _parseIndex++;
                float right = ParseUnary(expr);
                
                left = op == '*' ? left * right : left / right;
            }
            
            return left;
        }
        
        private float ParseUnary(string expr)
        {
            // Handle unary minus
            if (_parseIndex < expr.Length && expr[_parseIndex] == '-')
            {
                _parseIndex++;
                return -ParsePrimary(expr);
            }
            
            // Handle unary plus
            if (_parseIndex < expr.Length && expr[_parseIndex] == '+')
            {
                _parseIndex++;
            }
            
            return ParsePrimary(expr);
        }
        
        private float ParsePrimary(string expr)
        {
            // Handle parentheses
            if (_parseIndex < expr.Length && expr[_parseIndex] == '(')
            {
                _parseIndex++; // Skip '('
                float result = ParseAddSubtractInternal(expr);
                
                if (_parseIndex < expr.Length && expr[_parseIndex] == ')')
                    _parseIndex++; // Skip ')'
                
                return result;
            }
            
            // Parse number
            int start = _parseIndex;
            while (_parseIndex < expr.Length && (char.IsDigit(expr[_parseIndex]) || expr[_parseIndex] == '.'))
            {
                _parseIndex++;
            }
            
            string numStr = expr.Substring(start, _parseIndex - start);
            if (float.TryParse(numStr, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                return value;
            }
            
            throw new FormatException($"Invalid number: {numStr}");
        }
        
        private bool IsKnownVariable(string name)
        {
            // Check if already set
            if (_variables.ContainsKey(name))
                return true;
            
            // Check known variable names
            return name switch
            {
                // Attacker stats
                "Strength" or "Dexterity" or "Intelligence" => true,
                "AttackPower" or "SpellPower" => true,
                "CritChance" or "CritMultiplier" or "CritRating" or "CritDamage" => true,
                "Accuracy" or "Level" => true,
                
                // Weapon stats
                "WeaponDamage" or "WeaponDamageMin" or "WeaponDamageMax" => true,
                "AttackSpeed" or "ElementType" => true,
                
                // Target stats
                "Defense" or "Armor" or "Evasion" or "Resistance" => true,
                "TargetDefense" or "TargetEvasion" or "TargetLevel" => true,
                "HealthPercent" or "TargetHealthPercent" => true,
                
                // Special
                "Random" or "Distance" or "Damage" => true,
                
                _ => false
            };
        }
    }
}
