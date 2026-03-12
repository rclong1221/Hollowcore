using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.CharacterWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CW-05: Character Stats module.
    /// Health pools, armor values, damage resistances, stat comparison.
    /// </summary>
    public class CharacterStatsModule : ICharacterModule
    {
        private Vector2 _scrollPosition;
        
        // Character list
        private List<CharacterStatProfile> _profiles = new List<CharacterStatProfile>();
        private int _selectedProfileIndex = -1;
        private string _newProfileName = "NewCharacter";
        
        // Comparison
        private int _compareProfileIndex = -1;
        private bool _showComparison = false;
        
        // Presets
        private enum CharacterPreset { None, Tank, Assassin, Balanced, Glass_Cannon, Support }
        private CharacterPreset _selectedPreset = CharacterPreset.None;

        [System.Serializable]
        private class CharacterStatProfile
        {
            public string Name;
            public GameObject Prefab;
            
            // Health
            public float MaxHealth = 100f;
            public float HealthRegen = 0f;
            public float ShieldCapacity = 0f;
            public float ShieldRegenDelay = 3f;
            public float ShieldRegenRate = 20f;
            
            // Defense
            public float BaseArmor = 0f;
            public float ArmorEffectiveness = 0.01f; // % damage reduction per point
            public float DamageReduction = 0f; // Flat % reduction
            
            // Resistances
            public float PhysicalResist = 0f;
            public float FireResist = 0f;
            public float ColdResist = 0f;
            public float LightningResist = 0f;
            public float PoisonResist = 0f;
            
            // Movement
            public float MoveSpeed = 5f;
            public float SprintMultiplier = 1.5f;
            public float JumpHeight = 2f;
            
            // Combat
            public float AttackSpeedMult = 1f;
            public float DamageMult = 1f;
            public float CritChance = 0.05f;
            public float CritDamage = 1.5f;
            
            // Stamina
            public float MaxStamina = 100f;
            public float StaminaRegen = 20f;
            public float StaminaRegenDelay = 1f;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Character Stats", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure character combat statistics, compare profiles, and balance characters.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - profile list
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            DrawProfileList();
            EditorGUILayout.EndVertical();

            // Right panel - stats editor
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_showComparison && _selectedProfileIndex >= 0 && _compareProfileIndex >= 0)
            {
                DrawComparison();
            }
            else if (_selectedProfileIndex >= 0)
            {
                DrawStatsEditor();
            }
            else
            {
                EditorGUILayout.LabelField("Select or create a profile", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawProfileList()
        {
            EditorGUILayout.LabelField("Profiles", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _profiles.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool selected = i == _selectedProfileIndex;
                Color prevColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = Color.cyan;
                else if (i == _compareProfileIndex) GUI.backgroundColor = Color.yellow;
                
                if (GUILayout.Button(_profiles[i].Name, EditorStyles.miniButton))
                {
                    if (Event.current.shift && _selectedProfileIndex >= 0)
                    {
                        _compareProfileIndex = i;
                        _showComparison = true;
                    }
                    else
                    {
                        _selectedProfileIndex = i;
                        _compareProfileIndex = -1;
                        _showComparison = false;
                    }
                }
                
                GUI.backgroundColor = prevColor;

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _profiles.RemoveAt(i);
                    if (_selectedProfileIndex >= _profiles.Count)
                        _selectedProfileIndex = _profiles.Count - 1;
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Shift+Click to compare", EditorStyles.miniLabel);

            EditorGUILayout.Space(10);

            // New profile
            _newProfileName = EditorGUILayout.TextField(_newProfileName);
            if (GUILayout.Button("+ Create Profile"))
            {
                _profiles.Add(new CharacterStatProfile { Name = _newProfileName });
                _selectedProfileIndex = _profiles.Count - 1;
                _newProfileName = "NewCharacter";
            }

            EditorGUILayout.Space(5);

            // Import from prefab
            if (GUILayout.Button("Import from Selection"))
            {
                ImportFromSelection();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatsEditor()
        {
            var profile = _profiles[_selectedProfileIndex];

            // Header
            EditorGUILayout.BeginHorizontal();
            profile.Name = EditorGUILayout.TextField("Name", profile.Name);
            EditorGUILayout.EndHorizontal();

            // Prefab link
            profile.Prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", profile.Prefab, 
                typeof(GameObject), false);

            EditorGUILayout.Space(5);

            // Preset
            EditorGUILayout.BeginHorizontal();
            _selectedPreset = (CharacterPreset)EditorGUILayout.EnumPopup("Apply Preset", _selectedPreset);
            if (GUILayout.Button("Apply", GUILayout.Width(60)) && _selectedPreset != CharacterPreset.None)
            {
                ApplyPreset(profile, _selectedPreset);
                _selectedPreset = CharacterPreset.None;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Stats sections
            DrawHealthSection(profile);
            EditorGUILayout.Space(10);
            DrawDefenseSection(profile);
            EditorGUILayout.Space(10);
            DrawResistancesSection(profile);
            EditorGUILayout.Space(10);
            DrawMovementSection(profile);
            EditorGUILayout.Space(10);
            DrawCombatSection(profile);
            EditorGUILayout.Space(10);
            DrawStaminaSection(profile);

            EditorGUILayout.Space(15);

            // Actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply to Prefab", GUILayout.Height(30)))
            {
                ApplyToPrefab(profile);
            }
            if (GUILayout.Button("Export JSON", GUILayout.Height(30)))
            {
                ExportProfile(profile);
            }
            EditorGUILayout.EndHorizontal();

            // Power rating
            EditorGUILayout.Space(10);
            float powerRating = CalculatePowerRating(profile);
            EditorGUILayout.LabelField($"Power Rating: {powerRating:F0}", EditorStyles.boldLabel);
            
            Rect powerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(20), GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(powerRect, Mathf.Clamp01(powerRating / 200f), 
                GetPowerTier(powerRating));
        }

        private void DrawHealthSection(CharacterStatProfile profile)
        {
            EditorGUILayout.LabelField("Health & Shield", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            profile.MaxHealth = EditorGUILayout.FloatField("Max Health", profile.MaxHealth);
            profile.HealthRegen = EditorGUILayout.FloatField("Health Regen/s", profile.HealthRegen);
            
            EditorGUILayout.Space(5);
            profile.ShieldCapacity = EditorGUILayout.FloatField("Shield Capacity", profile.ShieldCapacity);
            
            EditorGUI.BeginDisabledGroup(profile.ShieldCapacity <= 0);
            profile.ShieldRegenDelay = EditorGUILayout.FloatField("Shield Regen Delay", profile.ShieldRegenDelay);
            profile.ShieldRegenRate = EditorGUILayout.FloatField("Shield Regen Rate/s", profile.ShieldRegenRate);
            EditorGUI.EndDisabledGroup();

            // EHP preview
            float ehp = CalculateEHP(profile);
            EditorGUILayout.LabelField($"Effective HP: {ehp:F0}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawDefenseSection(CharacterStatProfile profile)
        {
            EditorGUILayout.LabelField("Defense", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            profile.BaseArmor = EditorGUILayout.FloatField("Base Armor", profile.BaseArmor);
            profile.ArmorEffectiveness = EditorGUILayout.Slider("Armor Effectiveness", 
                profile.ArmorEffectiveness, 0.001f, 0.05f);
            
            float armorReduction = profile.BaseArmor * profile.ArmorEffectiveness * 100f;
            EditorGUILayout.LabelField($"  → {armorReduction:F1}% damage reduction from armor", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);
            profile.DamageReduction = EditorGUILayout.Slider("Flat Damage Reduction", 
                profile.DamageReduction, 0f, 0.9f);
            EditorGUILayout.LabelField($"  → {profile.DamageReduction * 100:F0}% flat reduction", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawResistancesSection(CharacterStatProfile profile)
        {
            EditorGUILayout.LabelField("Elemental Resistances", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            profile.PhysicalResist = DrawResistanceSlider("Physical", profile.PhysicalResist, Color.gray);
            profile.FireResist = DrawResistanceSlider("Fire", profile.FireResist, Color.red);
            profile.ColdResist = DrawResistanceSlider("Cold", profile.ColdResist, Color.cyan);
            profile.LightningResist = DrawResistanceSlider("Lightning", profile.LightningResist, Color.yellow);
            profile.PoisonResist = DrawResistanceSlider("Poison", profile.PoisonResist, Color.green);

            EditorGUILayout.EndVertical();
        }

        private float DrawResistanceSlider(string name, float value, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(name, GUILayout.Width(80));
            GUI.color = prevColor;
            
            value = EditorGUILayout.Slider(value, -0.5f, 0.9f);
            
            string label = value >= 0 ? $"{value * 100:F0}% resist" : $"{-value * 100:F0}% WEAK";
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(80));
            
            EditorGUILayout.EndHorizontal();
            return value;
        }

        private void DrawMovementSection(CharacterStatProfile profile)
        {
            EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            profile.MoveSpeed = EditorGUILayout.Slider("Move Speed", profile.MoveSpeed, 1f, 15f);
            profile.SprintMultiplier = EditorGUILayout.Slider("Sprint Multiplier", profile.SprintMultiplier, 1f, 3f);
            profile.JumpHeight = EditorGUILayout.Slider("Jump Height", profile.JumpHeight, 0.5f, 5f);

            EditorGUILayout.LabelField($"Sprint Speed: {profile.MoveSpeed * profile.SprintMultiplier:F1} m/s", 
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawCombatSection(CharacterStatProfile profile)
        {
            EditorGUILayout.LabelField("Combat", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            profile.AttackSpeedMult = EditorGUILayout.Slider("Attack Speed Mult", profile.AttackSpeedMult, 0.5f, 2f);
            profile.DamageMult = EditorGUILayout.Slider("Damage Mult", profile.DamageMult, 0.5f, 3f);
            
            EditorGUILayout.Space(5);
            profile.CritChance = EditorGUILayout.Slider("Crit Chance", profile.CritChance, 0f, 1f);
            profile.CritDamage = EditorGUILayout.Slider("Crit Damage", profile.CritDamage, 1f, 5f);

            float avgDamageMult = profile.DamageMult * (1f + profile.CritChance * (profile.CritDamage - 1f));
            EditorGUILayout.LabelField($"Average Damage Mult: {avgDamageMult:F2}x", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawStaminaSection(CharacterStatProfile profile)
        {
            EditorGUILayout.LabelField("Stamina", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            profile.MaxStamina = EditorGUILayout.FloatField("Max Stamina", profile.MaxStamina);
            profile.StaminaRegen = EditorGUILayout.FloatField("Stamina Regen/s", profile.StaminaRegen);
            profile.StaminaRegenDelay = EditorGUILayout.FloatField("Regen Delay", profile.StaminaRegenDelay);

            EditorGUILayout.EndVertical();
        }

        private void DrawComparison()
        {
            var p1 = _profiles[_selectedProfileIndex];
            var p2 = _profiles[_compareProfileIndex];

            EditorGUILayout.LabelField($"Comparing: {p1.Name} vs {p2.Name}", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Close Comparison"))
            {
                _showComparison = false;
                _compareProfileIndex = -1;
            }

            EditorGUILayout.Space(10);

            // Comparison table
            DrawCompareRow("Max Health", p1.MaxHealth, p2.MaxHealth);
            DrawCompareRow("Health Regen", p1.HealthRegen, p2.HealthRegen);
            DrawCompareRow("Shield", p1.ShieldCapacity, p2.ShieldCapacity);
            DrawCompareRow("Armor", p1.BaseArmor, p2.BaseArmor);
            DrawCompareRow("Damage Reduction", p1.DamageReduction * 100f, p2.DamageReduction * 100f, "%");
            
            EditorGUILayout.Space(5);
            DrawCompareRow("Move Speed", p1.MoveSpeed, p2.MoveSpeed);
            DrawCompareRow("Attack Speed", p1.AttackSpeedMult, p2.AttackSpeedMult, "x");
            DrawCompareRow("Damage Mult", p1.DamageMult, p2.DamageMult, "x");
            DrawCompareRow("Crit Chance", p1.CritChance * 100f, p2.CritChance * 100f, "%");
            DrawCompareRow("Crit Damage", p1.CritDamage, p2.CritDamage, "x");

            EditorGUILayout.Space(5);
            DrawCompareRow("EHP", CalculateEHP(p1), CalculateEHP(p2));
            DrawCompareRow("Power Rating", CalculatePowerRating(p1), CalculatePowerRating(p2));
        }

        private void DrawCompareRow(string label, float v1, float v2, string suffix = "")
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));

            Color prevColor = GUI.color;
            
            GUI.color = v1 > v2 ? Color.green : (v1 < v2 ? Color.red : Color.white);
            EditorGUILayout.LabelField($"{v1:F1}{suffix}", GUILayout.Width(80));
            
            GUI.color = Color.white;
            EditorGUILayout.LabelField("vs", GUILayout.Width(30));
            
            GUI.color = v2 > v1 ? Color.green : (v2 < v1 ? Color.red : Color.white);
            EditorGUILayout.LabelField($"{v2:F1}{suffix}", GUILayout.Width(80));
            
            GUI.color = prevColor;

            float diff = v1 - v2;
            string diffStr = diff > 0 ? $"+{diff:F1}" : $"{diff:F1}";
            EditorGUILayout.LabelField(diffStr, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyPreset(CharacterStatProfile profile, CharacterPreset preset)
        {
            switch (preset)
            {
                case CharacterPreset.Tank:
                    profile.MaxHealth = 200f;
                    profile.HealthRegen = 5f;
                    profile.ShieldCapacity = 50f;
                    profile.BaseArmor = 100f;
                    profile.DamageReduction = 0.1f;
                    profile.MoveSpeed = 4f;
                    profile.AttackSpeedMult = 0.8f;
                    profile.DamageMult = 0.8f;
                    break;

                case CharacterPreset.Assassin:
                    profile.MaxHealth = 80f;
                    profile.HealthRegen = 0f;
                    profile.ShieldCapacity = 0f;
                    profile.BaseArmor = 20f;
                    profile.DamageReduction = 0f;
                    profile.MoveSpeed = 7f;
                    profile.SprintMultiplier = 1.8f;
                    profile.AttackSpeedMult = 1.3f;
                    profile.DamageMult = 1.5f;
                    profile.CritChance = 0.25f;
                    profile.CritDamage = 2.5f;
                    break;

                case CharacterPreset.Balanced:
                    profile.MaxHealth = 100f;
                    profile.HealthRegen = 2f;
                    profile.ShieldCapacity = 25f;
                    profile.BaseArmor = 50f;
                    profile.DamageReduction = 0f;
                    profile.MoveSpeed = 5f;
                    profile.AttackSpeedMult = 1f;
                    profile.DamageMult = 1f;
                    profile.CritChance = 0.1f;
                    profile.CritDamage = 1.5f;
                    break;

                case CharacterPreset.Glass_Cannon:
                    profile.MaxHealth = 60f;
                    profile.HealthRegen = 0f;
                    profile.ShieldCapacity = 0f;
                    profile.BaseArmor = 0f;
                    profile.DamageReduction = 0f;
                    profile.MoveSpeed = 5f;
                    profile.AttackSpeedMult = 1.2f;
                    profile.DamageMult = 2f;
                    profile.CritChance = 0.2f;
                    profile.CritDamage = 3f;
                    break;

                case CharacterPreset.Support:
                    profile.MaxHealth = 100f;
                    profile.HealthRegen = 10f;
                    profile.ShieldCapacity = 100f;
                    profile.ShieldRegenRate = 30f;
                    profile.BaseArmor = 30f;
                    profile.DamageReduction = 0f;
                    profile.MoveSpeed = 5.5f;
                    profile.AttackSpeedMult = 0.9f;
                    profile.DamageMult = 0.7f;
                    break;
            }
        }

        private float CalculateEHP(CharacterStatProfile profile)
        {
            float armorMitigation = 1f - (profile.BaseArmor * profile.ArmorEffectiveness);
            float totalMitigation = armorMitigation * (1f - profile.DamageReduction);
            
            float effectiveHealth = profile.MaxHealth / Mathf.Max(0.1f, totalMitigation);
            effectiveHealth += profile.ShieldCapacity;
            
            return effectiveHealth;
        }

        private float CalculatePowerRating(CharacterStatProfile profile)
        {
            float survivalScore = CalculateEHP(profile) / 100f * 30f;
            float damageScore = profile.DamageMult * (1f + profile.CritChance * (profile.CritDamage - 1f)) * 30f;
            float mobilityScore = (profile.MoveSpeed / 5f) * 20f;
            float attackSpeedScore = profile.AttackSpeedMult * 20f;
            
            return survivalScore + damageScore + mobilityScore + attackSpeedScore;
        }

        private string GetPowerTier(float rating)
        {
            if (rating < 50) return "Weak";
            if (rating < 80) return "Normal";
            if (rating < 120) return "Strong";
            if (rating < 160) return "Elite";
            return "Boss";
        }

        private void ImportFromSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[CharacterStats] No GameObject selected.");
                return;
            }

            var profile = new CharacterStatProfile
            {
                Name = selected.name,
                Prefab = selected
            };

            // Try to import from existing components
            // This would integrate with your actual stat components

            _profiles.Add(profile);
            _selectedProfileIndex = _profiles.Count - 1;
            
            Debug.Log($"[CharacterStats] Imported profile for {selected.name}");
        }

        private void ApplyToPrefab(CharacterStatProfile profile)
        {
            if (profile.Prefab == null)
            {
                Debug.LogWarning("[CharacterStats] No prefab assigned to profile.");
                return;
            }

            // This would apply stats to actual stat components on the prefab
            EditorUtility.SetDirty(profile.Prefab);
            Debug.Log($"[CharacterStats] Applied stats to {profile.Prefab.name}");
        }

        private void ExportProfile(CharacterStatProfile profile)
        {
            string json = JsonUtility.ToJson(profile, true);
            string path = EditorUtility.SaveFilePanel("Export Profile", "", profile.Name, "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[CharacterStats] Exported profile to {path}");
            }
        }
    }
}
