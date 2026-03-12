using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Combat.Definitions;
using DIG.Combat.Formulas;
using DIG.Combat.Resolvers;

namespace DIG.Combat.Editor
{
    /// <summary>
    /// Custom editor for DamageFormula with validation and testing.
    /// </summary>
    [CustomEditor(typeof(DamageFormula))]
    public class DamageFormulaEditor : UnityEditor.Editor
    {
        private FormulaEvaluator _evaluator;
        private bool _showTest = false;
        private bool _showValidation = true;
        
        // Test values
        private float _testWeaponDamage = 100f;
        private float _testAttackPower = 50f;
        private float _testStrength = 20f;
        private float _testCritChance = 0.1f;
        private float _testCritMultiplier = 1.5f;
        private float _testDefense = 30f;
        private float _testEvasion = 0.1f;
        private int _testAttackerLevel = 10;
        private int _testTargetLevel = 10;
        private float _testHealthPercent = 1f;
        
        private void OnEnable()
        {
            _evaluator = new FormulaEvaluator();
        }
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            
            var formula = (DamageFormula)target;
            
            // Validation section
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation", true);
            if (_showValidation)
            {
                DrawValidation(formula);
            }
            
            EditorGUILayout.Space(5);
            
            // Test section
            _showTest = EditorGUILayout.Foldout(_showTest, "Formula Tester", true);
            if (_showTest)
            {
                DrawFormulaTester(formula);
            }
        }
        
        private void DrawValidation(DamageFormula formula)
        {
            EditorGUI.indentLevel++;
            
            if (_evaluator.ValidateFormula(formula, out List<string> errors))
            {
                EditorGUILayout.HelpBox("All expressions are valid.", MessageType.Info);
            }
            else
            {
                foreach (var error in errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void DrawFormulaTester(DamageFormula formula)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.LabelField("Test Values", EditorStyles.boldLabel);
            
            _testWeaponDamage = EditorGUILayout.FloatField("Weapon Damage", _testWeaponDamage);
            _testAttackPower = EditorGUILayout.FloatField("Attack Power", _testAttackPower);
            _testStrength = EditorGUILayout.FloatField("Strength", _testStrength);
            _testCritChance = EditorGUILayout.Slider("Crit Chance", _testCritChance, 0f, 1f);
            _testCritMultiplier = EditorGUILayout.FloatField("Crit Multiplier", _testCritMultiplier);
            _testDefense = EditorGUILayout.FloatField("Target Defense", _testDefense);
            _testEvasion = EditorGUILayout.Slider("Target Evasion", _testEvasion, 0f, 1f);
            _testAttackerLevel = EditorGUILayout.IntField("Attacker Level", _testAttackerLevel);
            _testTargetLevel = EditorGUILayout.IntField("Target Level", _testTargetLevel);
            _testHealthPercent = EditorGUILayout.Slider("Target Health %", _testHealthPercent, 0f, 1f);
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Calculate Damage"))
            {
                CalculateTestDamage(formula);
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void CalculateTestDamage(DamageFormula formula)
        {
            // Create test context
            var context = new CombatContext
            {
                AttackerStats = new StatBlock
                {
                    AttackPower = _testAttackPower,
                    Strength = _testStrength,
                    CritChance = _testCritChance,
                    CritMultiplier = _testCritMultiplier,
                    Level = _testAttackerLevel
                },
                TargetStats = new StatBlock
                {
                    Defense = _testDefense,
                    Evasion = _testEvasion,
                    Level = _testTargetLevel,
                    HealthPercent = _testHealthPercent
                },
                WeaponData = new WeaponStats
                {
                    BaseDamage = _testWeaponDamage,
                    DamageMin = _testWeaponDamage * 0.9f,
                    DamageMax = _testWeaponDamage * 1.1f
                }
            };
            
            // Calculate
            float baseDamage = _evaluator.EvaluateBaseDamage(formula, in context);
            float critChance = _evaluator.EvaluateCritChance(formula, in context);
            float critMult = _evaluator.EvaluateCritMultiplier(formula, in context);
            float hitChance = _evaluator.EvaluateHitChance(formula, in context);
            float normalDamage = _evaluator.CalculateFullDamage(formula, in context, false);
            float critDamage = _evaluator.CalculateFullDamage(formula, in context, true);
            
            Debug.Log($"=== Formula Test: {formula.FormulaName} ===");
            Debug.Log($"Base Damage: {baseDamage:F1}");
            Debug.Log($"Hit Chance: {hitChance:P1}");
            Debug.Log($"Crit Chance: {critChance:P1}");
            Debug.Log($"Crit Multiplier: {critMult:F2}x");
            Debug.Log($"Final Damage (Normal): {normalDamage:F1}");
            Debug.Log($"Final Damage (Crit): {critDamage:F1}");
        }
    }
}
