using UnityEngine;
using UnityEditor;
using DIG.Combat.Definitions;
using DIG.Combat.Formulas;

namespace DIG.Combat.Editor
{
    /// <summary>
    /// Editor window for testing combat formulas interactively.
    /// </summary>
    public class FormulaTestingWindow : EditorWindow
    {
        private DamageFormula _formula;
        private FormulaEvaluator _evaluator = new();
        
        // Attacker stats
        private float _attackPower = 50f;
        private float _spellPower = 0f;
        private float _strength = 20f;
        private float _dexterity = 15f;
        private float _intelligence = 10f;
        private float _critChance = 0.1f;
        private float _critMultiplier = 1.5f;
        private float _accuracy = 1f;
        private int _attackerLevel = 10;
        
        // Weapon stats
        private float _weaponDamage = 100f;
        private float _weaponMin = 90f;
        private float _weaponMax = 110f;
        private float _attackSpeed = 1f;
        
        // Target stats
        private float _defense = 30f;
        private float _armor = 0f;
        private float _evasion = 0.1f;
        private int _targetLevel = 10;
        private float _healthPercent = 1f;
        private float _resistance = 0f;
        
        // Results
        private float _resultBaseDamage;
        private float _resultHitChance;
        private float _resultCritChance;
        private float _resultCritMult;
        private float _resultNormalDamage;
        private float _resultCritDamage;
        private float _resultDPS;
        
        private Vector2 _scrollPos;
        
        [MenuItem("DIG/Combat/Formula Testing Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<FormulaTestingWindow>("Formula Tester");
            window.minSize = new Vector2(400, 600);
        }
        
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            EditorGUILayout.LabelField("Combat Formula Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Formula selection
            _formula = (DamageFormula)EditorGUILayout.ObjectField("Damage Formula", _formula, typeof(DamageFormula), false);
            
            if (_formula == null)
            {
                EditorGUILayout.HelpBox("Select a DamageFormula asset to test.", MessageType.Info);
                
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Quick Create", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Create DIG Simple"))
                    _formula = DefaultFormulas.CreateDIGSimple();
                if (GUILayout.Button("Create ARPG Standard"))
                    _formula = DefaultFormulas.CreateARPGStandard();
                if (GUILayout.Button("Create ARPG Tactical"))
                    _formula = DefaultFormulas.CreateARPGTactical();
                if (GUILayout.Button("Create Hybrid"))
                    _formula = DefaultFormulas.CreateHybrid();
                if (GUILayout.Button("Create Spellcaster"))
                    _formula = DefaultFormulas.CreateSpellcaster();
                
                EditorGUILayout.EndScrollView();
                return;
            }
            
            EditorGUILayout.Space(10);
            
            // Attacker Stats
            EditorGUILayout.LabelField("Attacker Stats", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            _attackPower = EditorGUILayout.FloatField("Attack Power", _attackPower);
            _spellPower = EditorGUILayout.FloatField("Spell Power", _spellPower);
            _strength = EditorGUILayout.FloatField("Strength", _strength);
            _dexterity = EditorGUILayout.FloatField("Dexterity", _dexterity);
            _intelligence = EditorGUILayout.FloatField("Intelligence", _intelligence);
            _critChance = EditorGUILayout.Slider("Crit Chance", _critChance, 0f, 1f);
            _critMultiplier = EditorGUILayout.FloatField("Crit Multiplier", _critMultiplier);
            _accuracy = EditorGUILayout.FloatField("Accuracy", _accuracy);
            _attackerLevel = EditorGUILayout.IntField("Level", _attackerLevel);
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(5);
            
            // Weapon Stats
            EditorGUILayout.LabelField("Weapon Stats", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            _weaponDamage = EditorGUILayout.FloatField("Base Damage", _weaponDamage);
            _weaponMin = EditorGUILayout.FloatField("Damage Min", _weaponMin);
            _weaponMax = EditorGUILayout.FloatField("Damage Max", _weaponMax);
            _attackSpeed = EditorGUILayout.FloatField("Attack Speed", _attackSpeed);
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(5);
            
            // Target Stats
            EditorGUILayout.LabelField("Target Stats", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            _defense = EditorGUILayout.FloatField("Defense", _defense);
            _armor = EditorGUILayout.FloatField("Armor", _armor);
            _evasion = EditorGUILayout.Slider("Evasion", _evasion, 0f, 1f);
            _targetLevel = EditorGUILayout.IntField("Level", _targetLevel);
            _healthPercent = EditorGUILayout.Slider("Health %", _healthPercent, 0f, 1f);
            _resistance = EditorGUILayout.Slider("Resistance", _resistance, 0f, 1f);
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            
            // Calculate button
            if (GUILayout.Button("Calculate", GUILayout.Height(30)))
            {
                Calculate();
            }
            
            EditorGUILayout.Space(10);
            
            // Results
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField("Base Damage", _resultBaseDamage);
            EditorGUILayout.FloatField("Hit Chance", _resultHitChance);
            EditorGUILayout.FloatField("Crit Chance", _resultCritChance);
            EditorGUILayout.FloatField("Crit Multiplier", _resultCritMult);
            EditorGUILayout.Space(5);
            EditorGUILayout.FloatField("Normal Hit Damage", _resultNormalDamage);
            EditorGUILayout.FloatField("Critical Hit Damage", _resultCritDamage);
            EditorGUILayout.Space(5);
            EditorGUILayout.FloatField("Expected DPS", _resultDPS);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndScrollView();
        }
        
        private void Calculate()
        {
            if (_formula == null) return;
            
            var context = new Resolvers.CombatContext
            {
                AttackerStats = new Resolvers.StatBlock
                {
                    AttackPower = _attackPower,
                    SpellPower = _spellPower,
                    Strength = _strength,
                    Dexterity = _dexterity,
                    Intelligence = _intelligence,
                    CritChance = _critChance,
                    CritMultiplier = _critMultiplier,
                    Accuracy = _accuracy,
                    Level = _attackerLevel
                },
                TargetStats = new Resolvers.StatBlock
                {
                    Defense = _defense,
                    Armor = _armor,
                    Evasion = _evasion,
                    Level = _targetLevel,
                    HealthPercent = _healthPercent
                },
                WeaponData = new Resolvers.WeaponStats
                {
                    BaseDamage = _weaponDamage,
                    DamageMin = _weaponMin,
                    DamageMax = _weaponMax,
                    AttackSpeed = _attackSpeed
                }
            };
            
            _resultBaseDamage = _evaluator.EvaluateBaseDamage(_formula, in context);
            _resultHitChance = _evaluator.EvaluateHitChance(_formula, in context);
            _resultCritChance = _evaluator.EvaluateCritChance(_formula, in context);
            _resultCritMult = _evaluator.EvaluateCritMultiplier(_formula, in context);
            _resultNormalDamage = _evaluator.CalculateFullDamage(_formula, in context, false);
            _resultCritDamage = _evaluator.CalculateFullDamage(_formula, in context, true);
            
            // Calculate expected DPS
            float avgDamage = (_resultNormalDamage * (1f - _resultCritChance)) + (_resultCritDamage * _resultCritChance);
            _resultDPS = avgDamage * _resultHitChance * _attackSpeed;
        }
    }
}
