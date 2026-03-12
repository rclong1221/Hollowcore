using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using DIG.Weapons.Config;

namespace DIG.Editor.CombatWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.7: Combo System Configuration module.
    /// Configure global and per-weapon combo behavior: input modes, cancel policies, queue settings.
    /// </summary>
    public class ComboSystemModule : ICombatModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _weaponListScrollPosition;
        
        // Global config reference
        private ComboSystemConfig _globalConfig;
        private SerializedObject _serializedConfig;
        
        // Selected weapon for editing
        private GameObject _selectedWeaponPrefab;
        private List<WeaponComboInfo> _weaponList = new List<WeaponComboInfo>();
        private bool _showWeaponList = true;
        private string _weaponSearchFilter = "";
        
        // Timeline visualization
        private bool _showTimeline = true;
        private float _timelineZoom = 1f;
        
        private class WeaponComboInfo
        {
            public string Name;
            public string Path;
            public bool UsesGlobalConfig;
            public ComboInputMode InputMode;
            public int MaxCombos;
            public bool HasComboData;
        }

        public void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawGlobalConfigSection();
            EditorGUILayout.Space(10);
            
            DrawQuickPresetsSection();
            EditorGUILayout.Space(10);
            
            DrawWeaponOverridesSection();
            EditorGUILayout.Space(10);
            
            if (_showTimeline)
            {
                DrawTimelineSection();
                EditorGUILayout.Space(10);
            }
            
            DrawRuntimeDebugSection();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Combo System Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure how melee combos work across the game. Set global defaults, then override per-weapon as needed.\n\n" +
                "• InputPerSwing: Each attack requires a new button press (Souls-like)\n" +
                "• HoldToCombo: Hold button to auto-advance chain (DMC-style)\n" +
                "• RhythmBased: Timed inputs with bonus for perfect timing (Arkham-style)",
                MessageType.Info);
        }

        private void DrawGlobalConfigSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Global Configuration", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            _globalConfig = (ComboSystemConfig)EditorGUILayout.ObjectField(
                "Config Asset", _globalConfig, typeof(ComboSystemConfig), false);
            
            if (EditorGUI.EndChangeCheck() && _globalConfig != null)
            {
                _serializedConfig = new SerializedObject(_globalConfig);
            }
            
            if (_globalConfig == null)
            {
                EditorGUILayout.HelpBox(
                    "No ComboSystemConfig found. Create one via:\nAssets > Create > DIG > Weapons > Combo System Config",
                    MessageType.Warning);
                
                if (GUILayout.Button("Create New Config"))
                {
                    CreateNewConfig();
                }
                
                // Try to find existing
                if (GUILayout.Button("Find Existing Config"))
                {
                    FindExistingConfig();
                }
            }
            else
            {
                if (_serializedConfig == null)
                {
                    _serializedConfig = new SerializedObject(_globalConfig);
                }
                
                _serializedConfig.Update();
                
                EditorGUILayout.Space(5);
                
                // Input Mode
                var inputModeProp = _serializedConfig.FindProperty("inputMode");
                EditorGUILayout.PropertyField(inputModeProp, new GUIContent("Input Mode"));
                
                // Queue Depth
                var queueDepthProp = _serializedConfig.FindProperty("queueDepth");
                EditorGUILayout.PropertyField(queueDepthProp, new GUIContent("Queue Depth", "How many attacks can be buffered. 1 = buffer one ahead, 0 = no buffering"));
                
                // Cancel Policy
                var cancelPolicyProp = _serializedConfig.FindProperty("cancelPolicy");
                EditorGUILayout.PropertyField(cancelPolicyProp, new GUIContent("Cancel Policy"));
                
                // Cancel Priority
                var cancelPriorityProp = _serializedConfig.FindProperty("cancelPriority");
                EditorGUILayout.PropertyField(cancelPriorityProp, new GUIContent("Cancel Priority", "Which actions can cancel attacks"));
                
                // Queue Clear Policy
                var queueClearProp = _serializedConfig.FindProperty("queueClearPolicy");
                EditorGUILayout.PropertyField(queueClearProp, new GUIContent("Queue Clear Policy"));
                
                EditorGUILayout.Space(5);
                
                // Timing Settings
                EditorGUILayout.LabelField("Timing Settings", EditorStyles.miniBoldLabel);
                
                var comboWindowProp = _serializedConfig.FindProperty("comboWindow");
                EditorGUILayout.PropertyField(comboWindowProp, new GUIContent("Combo Window", "Time after attack ends to continue combo"));
                
                var rhythmWindowProp = _serializedConfig.FindProperty("rhythmWindowStart");
                var rhythmEndProp = _serializedConfig.FindProperty("rhythmWindowEnd");
                EditorGUILayout.PropertyField(rhythmWindowProp, new GUIContent("Rhythm Window Start"));
                EditorGUILayout.PropertyField(rhythmEndProp, new GUIContent("Rhythm Window End"));
                
                var perfectBonusProp = _serializedConfig.FindProperty("perfectTimingBonus");
                EditorGUILayout.PropertyField(perfectBonusProp, new GUIContent("Perfect Timing Bonus", "Damage multiplier for perfect rhythm timing"));
                
                _serializedConfig.ApplyModifiedProperties();
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save Config"))
                {
                    EditorUtility.SetDirty(_globalConfig);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[ComboSystem] Config saved.");
                }
                if (GUILayout.Button("Ping in Project"))
                {
                    EditorGUIUtility.PingObject(_globalConfig);
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawQuickPresetsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            
            if (_globalConfig == null)
            {
                EditorGUILayout.HelpBox("Assign a config above to use presets.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                
                GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
                if (GUILayout.Button("Souls-like\n(Input Per Swing)", GUILayout.Height(50)))
                {
                    if (EditorUtility.DisplayDialog("Apply Preset", 
                        "Apply Souls-like preset?\n\n• InputPerSwing mode\n• Queue depth 1\n• RecoveryOnly cancel\n• Dodge priority", "Apply", "Cancel"))
                    {
                        _globalConfig.ApplySoulsLikePreset();
                        EditorUtility.SetDirty(_globalConfig);
                        _serializedConfig.Update();
                    }
                }
                
                GUI.backgroundColor = new Color(1f, 0.9f, 0.8f);
                if (GUILayout.Button("Character Action\n(Hold to Combo)", GUILayout.Height(50)))
                {
                    if (EditorUtility.DisplayDialog("Apply Preset",
                        "Apply Character Action preset?\n\n• HoldToCombo mode\n• Queue depth 3\n• Anytime cancel\n• Dodge+Jump+Ability priority", "Apply", "Cancel"))
                    {
                        _globalConfig.ApplyCharacterActionPreset();
                        EditorUtility.SetDirty(_globalConfig);
                        _serializedConfig.Update();
                    }
                }
                
                GUI.backgroundColor = new Color(0.9f, 1f, 0.8f);
                if (GUILayout.Button("Brawler\n(Rhythm Based)", GUILayout.Height(50)))
                {
                    if (EditorUtility.DisplayDialog("Apply Preset",
                        "Apply Brawler preset?\n\n• RhythmBased mode\n• Queue depth 2\n• Anytime cancel\n• All priorities", "Apply", "Cancel"))
                    {
                        _globalConfig.ApplyBrawlerPreset();
                        EditorUtility.SetDirty(_globalConfig);
                        _serializedConfig.Update();
                    }
                }
                
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawWeaponOverridesSection()
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            _showWeaponList = EditorGUILayout.Foldout(_showWeaponList, "Per-Weapon Overrides", true);
            if (GUILayout.Button("Scan Weapons", GUILayout.Width(100)))
            {
                ScanWeaponsInProject();
            }
            EditorGUILayout.EndHorizontal();
            
            if (_showWeaponList)
            {
                EditorGUILayout.Space(5);
                
                _weaponSearchFilter = EditorGUILayout.TextField("Search", _weaponSearchFilter);
                
                EditorGUILayout.Space(5);
                
                if (_weaponList.Count == 0)
                {
                    EditorGUILayout.HelpBox("Click 'Scan Weapons' to find all melee weapons in the project.", MessageType.Info);
                }
                else
                {
                    // Header
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    GUILayout.Label("Weapon", EditorStyles.miniLabel, GUILayout.Width(150));
                    GUILayout.Label("Combos", EditorStyles.miniLabel, GUILayout.Width(50));
                    GUILayout.Label("Config", EditorStyles.miniLabel, GUILayout.Width(60));
                    GUILayout.Label("Mode", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                    GUILayout.Label("Actions", EditorStyles.miniLabel, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                    
                    _weaponListScrollPosition = EditorGUILayout.BeginScrollView(
                        _weaponListScrollPosition, GUILayout.MaxHeight(200));
                    
                    var filtered = _weaponList
                        .Where(w => string.IsNullOrEmpty(_weaponSearchFilter) || 
                                    w.Name.ToLower().Contains(_weaponSearchFilter.ToLower()))
                        .ToList();
                    
                    foreach (var weapon in filtered)
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        // Name
                        GUILayout.Label(weapon.Name, GUILayout.Width(150));
                        
                        // Combo count
                        string comboText = weapon.HasComboData ? weapon.MaxCombos.ToString() : "-";
                        GUILayout.Label(comboText, GUILayout.Width(50));
                        
                        // Global/Override
                        string configText = weapon.UsesGlobalConfig ? "Global" : "Override";
                        GUI.color = weapon.UsesGlobalConfig ? Color.green : Color.yellow;
                        GUILayout.Label(configText, GUILayout.Width(60));
                        GUI.color = Color.white;
                        
                        // Mode
                        GUILayout.Label(weapon.InputMode.ToString(), GUILayout.ExpandWidth(true));
                        
                        // Actions
                        if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(80)))
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(weapon.Path);
                            if (obj != null)
                            {
                                Selection.activeGameObject = obj;
                                EditorGUIUtility.PingObject(obj);
                            }
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndScrollView();
                    
                    EditorGUILayout.LabelField($"Showing {filtered.Count} of {_weaponList.Count} weapons", EditorStyles.miniLabel);
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawTimelineSection()
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            _showTimeline = EditorGUILayout.Foldout(_showTimeline, "Combo Timeline Preview", true);
            _timelineZoom = EditorGUILayout.Slider(_timelineZoom, 0.5f, 2f, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            
            if (_showTimeline)
            {
                EditorGUILayout.Space(5);
                
                // Selected weapon preview
                _selectedWeaponPrefab = (GameObject)EditorGUILayout.ObjectField(
                    "Preview Weapon", _selectedWeaponPrefab, typeof(GameObject), false);
                
                if (_selectedWeaponPrefab != null)
                {
                    DrawComboTimeline();
                }
                else
                {
                    EditorGUILayout.HelpBox("Select a weapon prefab to preview its combo timeline.", MessageType.Info);
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawComboTimeline()
        {
            // Get combo data from weapon
            var authoring = _selectedWeaponPrefab.GetComponent<DIG.Weapons.Authoring.WeaponAuthoring>();
            if (authoring == null || authoring.comboData == null || authoring.comboData.Count == 0)
            {
                EditorGUILayout.HelpBox("This weapon has no combo data defined.", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.LabelField($"Combo Steps: {authoring.comboData.Count}", EditorStyles.miniBoldLabel);
            
            // Draw timeline
            float totalWidth = EditorGUIUtility.currentViewWidth - 40;
            float stepWidth = (totalWidth / authoring.comboData.Count) * _timelineZoom;
            
            EditorGUILayout.BeginHorizontal();
            
            for (int i = 0; i < authoring.comboData.Count; i++)
            {
                var combo = authoring.comboData[i];
                
                EditorGUILayout.BeginVertical("box", GUILayout.Width(stepWidth));
                
                // Step header
                EditorGUILayout.LabelField($"Step {i + 1}", EditorStyles.centeredGreyMiniLabel);
                
                // Duration bar
                Rect rect = GUILayoutUtility.GetRect(stepWidth - 10, 30);
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                
                // Hitbox window (green) - use global hitbox settings
                float hitboxStart = authoring.HitboxActiveStart;
                float hitboxEnd = authoring.HitboxActiveEnd;
                Rect hitboxRect = new Rect(
                    rect.x + rect.width * hitboxStart,
                    rect.y + 5,
                    rect.width * (hitboxEnd - hitboxStart),
                    10);
                EditorGUI.DrawRect(hitboxRect, new Color(0.2f, 0.8f, 0.2f, 0.8f));
                
                // Input window (blue) - from combo step data
                float inputWindowNormalized = combo.InputWindowEnd - combo.InputWindowStart;
                Rect inputRect = new Rect(
                    rect.x + rect.width * combo.InputWindowStart,
                    rect.y + 17,
                    rect.width * inputWindowNormalized,
                    8);
                EditorGUI.DrawRect(inputRect, new Color(0.2f, 0.5f, 1f, 0.8f));
                
                // Info
                EditorGUILayout.LabelField($"Dur: {combo.Duration:F2}s", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Dmg: x{combo.DamageMultiplier:F1}", EditorStyles.miniLabel);
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Legend
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(12, 12), new Color(0.2f, 0.8f, 0.2f));
            GUILayout.Label("Hitbox Active", EditorStyles.miniLabel);
            GUILayout.Space(10);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(12, 12), new Color(0.2f, 0.5f, 1f));
            GUILayout.Label("Input Window", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRuntimeDebugSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Runtime Debug", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see real-time combo state.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Combo State Monitor", EditorStyles.miniBoldLabel);
                
                // TODO: Hook into ECS to read MeleeState from player
                EditorGUILayout.LabelField("Current Combo: --", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Is Attacking: --", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Queued Attack: --", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Input Mode: --", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Cancel Window: --", EditorStyles.miniLabel);
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Spawn Target Dummy"))
                {
                    Debug.Log("[ComboSystem] Would spawn target dummy for testing.");
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void CreateNewConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Combo System Config",
                "ComboSystemConfig",
                "asset",
                "Choose where to save the config");
            
            if (!string.IsNullOrEmpty(path))
            {
                var config = ScriptableObject.CreateInstance<ComboSystemConfig>();
                config.ApplySoulsLikePreset(); // Default to souls-like
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                _globalConfig = config;
                _serializedConfig = new SerializedObject(_globalConfig);
                EditorGUIUtility.PingObject(config);
            }
        }

        private void FindExistingConfig()
        {
            var guids = AssetDatabase.FindAssets("t:ComboSystemConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _globalConfig = AssetDatabase.LoadAssetAtPath<ComboSystemConfig>(path);
                _serializedConfig = new SerializedObject(_globalConfig);
                EditorGUIUtility.PingObject(_globalConfig);
                Debug.Log($"[ComboSystem] Found config at: {path}");
            }
            else
            {
                Debug.LogWarning("[ComboSystem] No ComboSystemConfig assets found in project.");
            }
        }

        private void ScanWeaponsInProject()
        {
            _weaponList.Clear();
            
            var guids = AssetDatabase.FindAssets("t:Prefab");
            int found = 0;
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab == null) continue;
                
                var authoring = prefab.GetComponent<DIG.Weapons.Authoring.WeaponAuthoring>();
                if (authoring == null) continue;
                
                // Check if it's a melee weapon
                if (authoring.Type != DIG.Weapons.Authoring.WeaponType.Melee) continue;
                
                var info = new WeaponComboInfo
                {
                    Name = prefab.name,
                    Path = path,
                    UsesGlobalConfig = authoring.useGlobalComboConfig,
                    InputMode = authoring.inputModeOverride,
                    HasComboData = authoring.comboData != null && authoring.comboData.Count > 0,
                    MaxCombos = authoring.comboData?.Count ?? 0
                };
                
                _weaponList.Add(info);
                found++;
            }
            
            _weaponList = _weaponList.OrderBy(w => w.Name).ToList();
            Debug.Log($"[ComboSystem] Found {found} melee weapons in project.");
        }
    }
}
