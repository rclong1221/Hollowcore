using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Weapons.Authoring;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 EW-01: Melee Weapon Setup module.
    /// Configure swept hitboxes, presets, damage curves, and multi-hit settings.
    /// </summary>
    public class MeleeSetupModule : IEquipmentModule
    {
        private GameObject _selectedPrefab;
        private Vector2 _scrollPosition;
        
        // Preset selection
        private int _selectedPreset = 0;
        private readonly string[] _presets = { "Custom", "Sword", "Greatsword", "Dagger", "Fist", "Spear", "Axe", "Hammer" };
        
        // Hitbox config
        private float _hitboxRadius = 0.15f;
        private float _hitboxHeight = 0.8f;
        private Vector3 _tipOffset = new Vector3(0, 0, 1f);
        private Vector3 _handleOffset = Vector3.zero;
        
        // Damage config
        private float _baseDamage = 25f;
        private int _maxHitsPerSwing = 3;
        private float _hitCooldownPerTarget = 0.5f;
        private bool _canHitSameTargetMultipleTimes = false;
        
        // Damage curve
        private AnimationCurve _damageCurve = AnimationCurve.Linear(0, 0.8f, 1, 1.2f);
        private bool _useDamageCurve = true;
        
        // Sweep config
        private float _sweepDistance = 1.5f;
        private bool _visualizeSweep = true;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Melee Weapon Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure swept melee hitboxes for anti-tunneling collision detection. " +
                "Swept hitboxes prevent fast weapons from passing through targets between frames.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Prefab selection
            DrawPrefabSelection();
            EditorGUILayout.Space(10);

            // Preset selection
            DrawPresetSection();
            EditorGUILayout.Space(10);

            // Hitbox configuration
            DrawHitboxConfig();
            EditorGUILayout.Space(10);

            // Damage configuration
            DrawDamageConfig();
            EditorGUILayout.Space(10);

            // Sweep configuration
            DrawSweepConfig();
            EditorGUILayout.Space(10);

            // Actions
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabSelection()
        {
            EditorGUILayout.LabelField("Target Prefab", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _selectedPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Melee Weapon", _selectedPrefab, typeof(GameObject), true);

            if (Selection.activeGameObject != null && _selectedPrefab != Selection.activeGameObject)
            {
                if (GUILayout.Button($"Use Selected: {Selection.activeGameObject.name}", EditorStyles.miniButton))
                {
                    _selectedPrefab = Selection.activeGameObject;
                    LoadFromPrefab();
                }
            }

            if (_selectedPrefab != null)
            {
                var authoring = _selectedPrefab.GetComponent<SweptMeleeAuthoring>();
                if (authoring != null)
                {
                    EditorGUILayout.HelpBox("SweptMeleeAuthoring found. Values will be loaded.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("No SweptMeleeAuthoring component. Will be added on apply.", MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetSection()
        {
            EditorGUILayout.LabelField("Weapon Preset", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _selectedPreset = EditorGUILayout.Popup("Preset", _selectedPreset, _presets);
            if (EditorGUI.EndChangeCheck() && _selectedPreset > 0)
            {
                ApplyPreset(_presets[_selectedPreset]);
            }

            EditorGUILayout.BeginHorizontal();
            foreach (var preset in _presets)
            {
                if (preset == "Custom") continue;
                if (GUILayout.Button(preset, EditorStyles.miniButton))
                {
                    ApplyPreset(preset);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawHitboxConfig()
        {
            EditorGUILayout.LabelField("Hitbox Shape", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _hitboxRadius = EditorGUILayout.Slider("Radius", _hitboxRadius, 0.05f, 0.5f);
            _hitboxHeight = EditorGUILayout.Slider("Height", _hitboxHeight, 0.2f, 2f);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Offsets (Local Space)", EditorStyles.miniLabel);
            _tipOffset = EditorGUILayout.Vector3Field("Tip Offset", _tipOffset);
            _handleOffset = EditorGUILayout.Vector3Field("Handle Offset", _handleOffset);

            EditorGUILayout.EndVertical();
        }

        private void DrawDamageConfig()
        {
            EditorGUILayout.LabelField("Damage Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _baseDamage = EditorGUILayout.FloatField("Base Damage", _baseDamage);
            _maxHitsPerSwing = EditorGUILayout.IntSlider("Max Hits Per Swing", _maxHitsPerSwing, 1, 10);
            
            _canHitSameTargetMultipleTimes = EditorGUILayout.Toggle("Multi-Hit Same Target", _canHitSameTargetMultipleTimes);
            if (_canHitSameTargetMultipleTimes)
            {
                EditorGUI.indentLevel++;
                _hitCooldownPerTarget = EditorGUILayout.Slider("Hit Cooldown (s)", _hitCooldownPerTarget, 0.1f, 2f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            _useDamageCurve = EditorGUILayout.Toggle("Use Damage Curve", _useDamageCurve);
            if (_useDamageCurve)
            {
                EditorGUILayout.LabelField("Damage Multiplier Over Swing (0=start, 1=end)", EditorStyles.miniLabel);
                _damageCurve = EditorGUILayout.CurveField("Damage Curve", _damageCurve, Color.red, new Rect(0, 0, 1, 2));
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSweepConfig()
        {
            EditorGUILayout.LabelField("Sweep Detection", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _sweepDistance = EditorGUILayout.Slider("Sweep Distance", _sweepDistance, 0.5f, 3f);
            _visualizeSweep = EditorGUILayout.Toggle("Visualize in Scene", _visualizeSweep);

            if (_visualizeSweep && _selectedPrefab != null)
            {
                EditorGUILayout.HelpBox("Sweep volume shown as green capsule in Scene view.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(_selectedPrefab == null);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply to Prefab", GUILayout.Height(30)))
            {
                ApplyToPrefab();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Load from Prefab", GUILayout.Height(30)))
            {
                LoadFromPrefab();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Reset to Defaults"))
            {
                ResetToDefaults();
            }

            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(string presetName)
        {
            _selectedPreset = System.Array.IndexOf(_presets, presetName);
            
            switch (presetName)
            {
                case "Sword":
                    _hitboxRadius = 0.12f;
                    _hitboxHeight = 0.9f;
                    _tipOffset = new Vector3(0, 0, 0.9f);
                    _handleOffset = new Vector3(0, 0, 0.1f);
                    _baseDamage = 30f;
                    _maxHitsPerSwing = 3;
                    _sweepDistance = 1.2f;
                    break;
                    
                case "Greatsword":
                    _hitboxRadius = 0.18f;
                    _hitboxHeight = 1.4f;
                    _tipOffset = new Vector3(0, 0, 1.3f);
                    _handleOffset = new Vector3(0, 0, 0.15f);
                    _baseDamage = 55f;
                    _maxHitsPerSwing = 5;
                    _sweepDistance = 1.8f;
                    break;
                    
                case "Dagger":
                    _hitboxRadius = 0.08f;
                    _hitboxHeight = 0.35f;
                    _tipOffset = new Vector3(0, 0, 0.35f);
                    _handleOffset = Vector3.zero;
                    _baseDamage = 18f;
                    _maxHitsPerSwing = 1;
                    _sweepDistance = 0.6f;
                    break;
                    
                case "Fist":
                    _hitboxRadius = 0.12f;
                    _hitboxHeight = 0.15f;
                    _tipOffset = new Vector3(0, 0, 0.25f);
                    _handleOffset = Vector3.zero;
                    _baseDamage = 12f;
                    _maxHitsPerSwing = 1;
                    _sweepDistance = 0.4f;
                    break;
                    
                case "Spear":
                    _hitboxRadius = 0.1f;
                    _hitboxHeight = 1.8f;
                    _tipOffset = new Vector3(0, 0, 1.8f);
                    _handleOffset = new Vector3(0, 0, 0.5f);
                    _baseDamage = 35f;
                    _maxHitsPerSwing = 2;
                    _sweepDistance = 2.2f;
                    break;
                    
                case "Axe":
                    _hitboxRadius = 0.2f;
                    _hitboxHeight = 0.5f;
                    _tipOffset = new Vector3(0, 0, 0.8f);
                    _handleOffset = new Vector3(0, 0, 0.3f);
                    _baseDamage = 45f;
                    _maxHitsPerSwing = 2;
                    _sweepDistance = 1.0f;
                    break;
                    
                case "Hammer":
                    _hitboxRadius = 0.25f;
                    _hitboxHeight = 0.4f;
                    _tipOffset = new Vector3(0, 0, 0.9f);
                    _handleOffset = new Vector3(0, 0, 0.35f);
                    _baseDamage = 60f;
                    _maxHitsPerSwing = 4;
                    _sweepDistance = 1.1f;
                    break;
            }
            
            Debug.Log($"[MeleeSetup] Applied preset: {presetName}");
        }

        private void ApplyToPrefab()
        {
            if (_selectedPrefab == null) return;

            Undo.RecordObject(_selectedPrefab, "Apply Melee Setup");

            var authoring = _selectedPrefab.GetComponent<SweptMeleeAuthoring>();
            if (authoring == null)
            {
                authoring = Undo.AddComponent<SweptMeleeAuthoring>(_selectedPrefab);
            }

            // Apply values
            authoring.capsuleRadius = _hitboxRadius;
            authoring.tipOffset = _tipOffset;
            authoring.handleOffset = _handleOffset;
            authoring.maxHitsPerSwing = _maxHitsPerSwing;

            EditorUtility.SetDirty(_selectedPrefab);
            
            Debug.Log($"[MeleeSetup] Applied settings to {_selectedPrefab.name}");
        }

        private void LoadFromPrefab()
        {
            if (_selectedPrefab == null) return;

            var authoring = _selectedPrefab.GetComponent<SweptMeleeAuthoring>();
            if (authoring == null)
            {
                Debug.LogWarning("[MeleeSetup] No SweptMeleeAuthoring component found.");
                return;
            }

            _hitboxRadius = authoring.capsuleRadius;
            _tipOffset = authoring.tipOffset;
            _handleOffset = authoring.handleOffset;
            _maxHitsPerSwing = authoring.maxHitsPerSwing;
            _selectedPreset = 0; // Custom

            Debug.Log($"[MeleeSetup] Loaded settings from {_selectedPrefab.name}");
        }

        private void ResetToDefaults()
        {
            _selectedPreset = 0;
            _hitboxRadius = 0.15f;
            _hitboxHeight = 0.8f;
            _tipOffset = new Vector3(0, 0, 1f);
            _handleOffset = Vector3.zero;
            _baseDamage = 25f;
            _maxHitsPerSwing = 3;
            _hitCooldownPerTarget = 0.5f;
            _canHitSameTargetMultipleTimes = false;
            _damageCurve = AnimationCurve.Linear(0, 0.8f, 1, 1.2f);
            _useDamageCurve = true;
            _sweepDistance = 1.5f;
        }
    }
}
