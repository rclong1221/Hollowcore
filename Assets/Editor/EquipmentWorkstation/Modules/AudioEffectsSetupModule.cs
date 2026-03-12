using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DIG.Weapons.Audio;
using DIG.Weapons.Authoring;
using DIG.Items.Authoring;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    /// <summary>
    /// EPIC 14.20: Audio & Effects Setup module for Equipment Workstation.
    /// Helps designers set up weapon audio, VFX, and related components.
    /// </summary>
    public class AudioEffectsSetupModule : IEquipmentModule
    {
        private GameObject _selectedPrefab;
        private Vector2 _scrollPosition;
        private List<SetupItem> _setupItems = new List<SetupItem>();
        private bool _hasAnalyzed = false;

        // Tabs within this module
        private int _subTab = 0;
        private readonly string[] _subTabs = { "Quick Setup", "Audio", "VFX", "Transforms" };

        // Audio config creation
        private string _newConfigName = "NewWeaponAudio";
        private WeaponAudioConfig _selectedAudioConfig;

        // Cache for found transforms
        private Transform _muzzleTransform;
        private Transform _ejectionTransform;
        private Transform _magazineTransform;

        private enum SetupStatus { Missing, Incomplete, Complete }

        private struct SetupItem
        {
            public string Category;
            public string Name;
            public string Description;
            public SetupStatus Status;
            public System.Action FixAction;
            public string FixButtonLabel;

            public SetupItem(string category, string name, string desc, SetupStatus status,
                System.Action fix = null, string fixLabel = "Fix")
            {
                Category = category;
                Name = name;
                Description = desc;
                Status = status;
                FixAction = fix;
                FixButtonLabel = fixLabel;
            }
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Audio & Effects Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure weapon audio, visual effects, and related components. " +
                "Select a weapon prefab to analyze and set up missing components.",
                MessageType.Info);
            EditorGUILayout.Space(5);

            // Prefab selection
            DrawPrefabSelection();
            EditorGUILayout.Space(10);

            // Sub-tabs
            _subTab = GUILayout.Toolbar(_subTab, _subTabs);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_subTab)
            {
                case 0: DrawQuickSetup(); break;
                case 1: DrawAudioSetup(); break;
                case 2: DrawVFXSetup(); break;
                case 3: DrawTransformSetup(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabSelection()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            _selectedPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Weapon Prefab", _selectedPrefab, typeof(GameObject), true);

            if (EditorGUI.EndChangeCheck())
            {
                _hasAnalyzed = false;
            }

            // Quick select from project selection
            if (Selection.activeGameObject != null && _selectedPrefab != Selection.activeGameObject)
            {
                if (GUILayout.Button("Use Selected", GUILayout.Width(100)))
                {
                    _selectedPrefab = Selection.activeGameObject;
                    _hasAnalyzed = false;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (_selectedPrefab != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Analyze Prefab", GUILayout.Height(25)))
                {
                    AnalyzePrefab();
                }
                if (_hasAnalyzed && GUILayout.Button("Fix All Missing", GUILayout.Height(25)))
                {
                    FixAllMissing();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        #region Quick Setup Tab

        private void DrawQuickSetup()
        {
            if (_selectedPrefab == null)
            {
                EditorGUILayout.HelpBox("Select a weapon prefab to begin setup.", MessageType.Warning);
                return;
            }

            if (!_hasAnalyzed)
            {
                EditorGUILayout.HelpBox("Click 'Analyze Prefab' to check component status.", MessageType.Info);
                return;
            }

            // Summary
            int missing = _setupItems.Count(i => i.Status == SetupStatus.Missing);
            int incomplete = _setupItems.Count(i => i.Status == SetupStatus.Incomplete);
            int complete = _setupItems.Count(i => i.Status == SetupStatus.Complete);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Setup Summary", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawStatusBadge("Complete", complete, Color.green);
            DrawStatusBadge("Incomplete", incomplete, new Color(1f, 0.8f, 0f));
            DrawStatusBadge("Missing", missing, Color.red);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Group items by category
            var categories = _setupItems.GroupBy(i => i.Category);
            foreach (var category in categories)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(category.Key, EditorStyles.miniBoldLabel);

                foreach (var item in category)
                {
                    DrawSetupItem(item);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }

        private void DrawStatusBadge(string label, int count, Color color)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label($"{label}: {count}", EditorStyles.miniButton, GUILayout.Width(100));
            GUI.backgroundColor = oldColor;
        }

        private void DrawSetupItem(SetupItem item)
        {
            EditorGUILayout.BeginHorizontal();

            // Status icon
            GUIContent icon = item.Status switch
            {
                SetupStatus.Complete => EditorGUIUtility.IconContent("TestPassed"),
                SetupStatus.Incomplete => EditorGUIUtility.IconContent("console.warnicon.sml"),
                SetupStatus.Missing => EditorGUIUtility.IconContent("console.erroricon.sml"),
                _ => GUIContent.none
            };
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(18));

            // Name and description
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(item.Name);
            if (!string.IsNullOrEmpty(item.Description))
            {
                EditorGUILayout.LabelField(item.Description, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            // Fix button
            if (item.FixAction != null && item.Status != SetupStatus.Complete)
            {
                if (GUILayout.Button(item.FixButtonLabel, GUILayout.Width(80)))
                {
                    item.FixAction();
                    AnalyzePrefab(); // Re-analyze after fix
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Audio Setup Tab

        private void DrawAudioSetup()
        {
            if (_selectedPrefab == null)
            {
                EditorGUILayout.HelpBox("Select a weapon prefab first.", MessageType.Warning);
                return;
            }

            // WeaponAudioBridge component
            EditorGUILayout.LabelField("Audio Bridge Component", EditorStyles.boldLabel);
            var audioBridge = _selectedPrefab.GetComponent<WeaponAudioBridge>();

            if (audioBridge == null)
            {
                audioBridge = _selectedPrefab.GetComponentInChildren<WeaponAudioBridge>();
            }

            if (audioBridge != null)
            {
                EditorGUILayout.HelpBox("WeaponAudioBridge found on prefab.", MessageType.Info);

                // Show current config
                var so = new SerializedObject(audioBridge);
                var configProp = so.FindProperty("audioConfig");
                EditorGUILayout.PropertyField(configProp, new GUIContent("Audio Config"));

                var muzzleProp = so.FindProperty("muzzleTransform");
                EditorGUILayout.PropertyField(muzzleProp, new GUIContent("Muzzle Transform"));

                if (so.hasModifiedProperties)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_selectedPrefab);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No WeaponAudioBridge component found.", MessageType.Warning);
                if (GUILayout.Button("Add WeaponAudioBridge"))
                {
                    AddWeaponAudioBridge();
                }
            }

            EditorGUILayout.Space(15);

            // Audio Config asset
            EditorGUILayout.LabelField("Audio Configuration Asset", EditorStyles.boldLabel);

            _selectedAudioConfig = (WeaponAudioConfig)EditorGUILayout.ObjectField(
                "Existing Config", _selectedAudioConfig, typeof(WeaponAudioConfig), false);

            if (_selectedAudioConfig != null && audioBridge != null)
            {
                if (GUILayout.Button("Assign Config to Bridge"))
                {
                    var so = new SerializedObject(audioBridge);
                    so.FindProperty("audioConfig").objectReferenceValue = _selectedAudioConfig;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_selectedPrefab);
                }
            }

            EditorGUILayout.Space(10);

            // Create new config
            EditorGUILayout.LabelField("Create New Audio Config", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _newConfigName = EditorGUILayout.TextField("Name", _newConfigName);
            if (GUILayout.Button("Create", GUILayout.Width(80)))
            {
                CreateNewAudioConfig();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);

            // Audio clip requirements
            DrawAudioRequirements();
        }

        private void DrawAudioRequirements()
        {
            EditorGUILayout.LabelField("Required Audio Clips", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "For full audio support, your WeaponAudioConfig should have:\n" +
                "- Fire clips (3-5 variations recommended)\n" +
                "- Dry fire click\n" +
                "- Reload sounds (mag out, mag in, bolt pull)\n" +
                "- Shell casing bounce sounds\n" +
                "- Equip/Unequip sounds (optional)",
                MessageType.Info);

            if (_selectedAudioConfig != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical("box");

                DrawClipStatus("Fire Clips", _selectedAudioConfig.FireClips);
                DrawClipStatus("Dry Fire", _selectedAudioConfig.DryFireClips);
                DrawClipStatus("Mag Out", _selectedAudioConfig.MagOutClips);
                DrawClipStatus("Mag In", _selectedAudioConfig.MagInClips);
                DrawClipStatus("Bolt Pull", _selectedAudioConfig.BoltPullClips);
                DrawClipStatus("Shell Bounce", _selectedAudioConfig.ShellBounceClips);
                DrawClipStatus("Equip", _selectedAudioConfig.EquipClips);

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawClipStatus(string name, AudioClip[] clips)
        {
            EditorGUILayout.BeginHorizontal();

            bool hasClips = clips != null && clips.Length > 0 && clips.Any(c => c != null);
            GUIContent icon = hasClips
                ? EditorGUIUtility.IconContent("TestPassed")
                : EditorGUIUtility.IconContent("console.warnicon.sml");

            GUILayout.Label(icon, GUILayout.Width(20));
            EditorGUILayout.LabelField($"{name}: {(hasClips ? $"{clips.Count(c => c != null)} clip(s)" : "None")}");

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region VFX Setup Tab

        private void DrawVFXSetup()
        {
            if (_selectedPrefab == null)
            {
                EditorGUILayout.HelpBox("Select a weapon prefab first.", MessageType.Warning);
                return;
            }

            // ItemVFXAuthoring component
            EditorGUILayout.LabelField("VFX Component", EditorStyles.boldLabel);
            var vfxAuthoring = _selectedPrefab.GetComponent<ItemVFXAuthoring>();

            if (vfxAuthoring == null)
            {
                vfxAuthoring = _selectedPrefab.GetComponentInChildren<ItemVFXAuthoring>();
            }

            if (vfxAuthoring != null)
            {
                EditorGUILayout.HelpBox("ItemVFXAuthoring found on prefab.", MessageType.Info);

                // Show VFX entries
                var so = new SerializedObject(vfxAuthoring);
                var definitionsProp = so.FindProperty("_definitions");

                if (definitionsProp != null)
                {
                    EditorGUILayout.PropertyField(definitionsProp, new GUIContent("VFX Definitions"), true);
                    if (so.hasModifiedProperties)
                    {
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_selectedPrefab);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No ItemVFXAuthoring component found.", MessageType.Warning);
                if (GUILayout.Button("Add ItemVFXAuthoring"))
                {
                    _selectedPrefab.AddComponent<ItemVFXAuthoring>();
                    EditorUtility.SetDirty(_selectedPrefab);
                }
            }

            EditorGUILayout.Space(15);

            // VFX Requirements
            EditorGUILayout.LabelField("Recommended VFX Entries", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "For shootable weapons, add these VFX entries:\n" +
                "- 'Fire' - Muzzle flash particle system\n" +
                "- 'ShellEject' - Shell casing ejection\n" +
                "- 'Tracer' - Bullet tracer (optional)\n" +
                "- 'Impact' - Hit effect (usually handled by system)",
                MessageType.Info);

            // Check for common VFX entries
            if (vfxAuthoring != null)
            {
                CheckVFXEntry(vfxAuthoring, "Fire", "Muzzle flash");
                CheckVFXEntry(vfxAuthoring, "ShellEject", "Shell ejection");
            }
        }

        private void CheckVFXEntry(ItemVFXAuthoring vfx, string id, string description)
        {
            var so = new SerializedObject(vfx);
            var definitionsProp = so.FindProperty("_definitions");

            bool hasEntry = false;
            if (definitionsProp != null && definitionsProp.isArray)
            {
                for (int i = 0; i < definitionsProp.arraySize; i++)
                {
                    var entry = definitionsProp.GetArrayElementAtIndex(i);
                    var idProp = entry.FindPropertyRelative("ID");
                    if (idProp != null && idProp.stringValue == id)
                    {
                        hasEntry = true;
                        break;
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUIContent icon = hasEntry
                ? EditorGUIUtility.IconContent("TestPassed")
                : EditorGUIUtility.IconContent("console.warnicon.sml");
            GUILayout.Label(icon, GUILayout.Width(20));
            EditorGUILayout.LabelField($"'{id}' entry ({description}): {(hasEntry ? "Found" : "Missing")}");
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Transform Setup Tab

        private void DrawTransformSetup()
        {
            if (_selectedPrefab == null)
            {
                EditorGUILayout.HelpBox("Select a weapon prefab first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Required Transforms", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Weapon prefabs need specific transforms for VFX and audio positioning:\n" +
                "- Muzzle point (for muzzle flash, audio origin)\n" +
                "- Ejection port (for shell casings)\n" +
                "- Magazine (for reload animations)",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Find transforms
            FindTransforms();

            // Muzzle
            DrawTransformField("Muzzle Point", ref _muzzleTransform,
                new[] { "Muzzle", "MuzzlePoint", "FirePoint", "Muzzle_Flash", "muzzle" });

            // Ejection
            DrawTransformField("Ejection Port", ref _ejectionTransform,
                new[] { "EjectionPort", "ShellEject", "Ejection", "Shell_Eject", "ejection" });

            // Magazine
            DrawTransformField("Magazine", ref _magazineTransform,
                new[] { "Magazine", "Mag", "Clip", "mag", "magazine" });

            EditorGUILayout.Space(15);

            // Auto-create missing transforms
            EditorGUILayout.LabelField("Create Missing Transforms", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (_muzzleTransform == null && GUILayout.Button("Create Muzzle"))
            {
                CreateTransform("MuzzlePoint", new Vector3(0, 0, 0.5f));
            }
            if (_ejectionTransform == null && GUILayout.Button("Create Ejection"))
            {
                CreateTransform("EjectionPort", new Vector3(0.05f, 0.05f, 0.1f));
            }
            if (_magazineTransform == null && GUILayout.Button("Create Magazine"))
            {
                CreateTransform("Magazine", new Vector3(0, -0.1f, 0));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Assign to components
            if (_muzzleTransform != null)
            {
                var audioBridge = _selectedPrefab.GetComponentInChildren<WeaponAudioBridge>();
                if (audioBridge != null)
                {
                    var so = new SerializedObject(audioBridge);
                    var muzzleProp = so.FindProperty("muzzleTransform");
                    if (muzzleProp.objectReferenceValue == null)
                    {
                        if (GUILayout.Button("Assign Muzzle to Audio Bridge"))
                        {
                            muzzleProp.objectReferenceValue = _muzzleTransform;
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_selectedPrefab);
                        }
                    }
                }
            }
        }

        private void FindTransforms()
        {
            if (_selectedPrefab == null) return;

            _muzzleTransform = FindChildTransform(_selectedPrefab.transform,
                new[] { "Muzzle", "MuzzlePoint", "FirePoint", "Muzzle_Flash", "muzzle" });
            _ejectionTransform = FindChildTransform(_selectedPrefab.transform,
                new[] { "EjectionPort", "ShellEject", "Ejection", "Shell_Eject", "ejection" });
            _magazineTransform = FindChildTransform(_selectedPrefab.transform,
                new[] { "Magazine", "Mag", "Clip", "mag", "magazine" });
        }

        private void DrawTransformField(string label, ref Transform transform, string[] searchNames)
        {
            EditorGUILayout.BeginHorizontal();

            GUIContent icon = transform != null
                ? EditorGUIUtility.IconContent("TestPassed")
                : EditorGUIUtility.IconContent("console.warnicon.sml");
            GUILayout.Label(icon, GUILayout.Width(20));

            EditorGUI.BeginChangeCheck();
            transform = (Transform)EditorGUILayout.ObjectField(label, transform, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck() && transform != null)
            {
                // Validate transform is child of prefab
                if (!transform.IsChildOf(_selectedPrefab.transform) && transform != _selectedPrefab.transform)
                {
                    Debug.LogWarning($"Transform must be a child of the weapon prefab");
                    transform = null;
                }
            }

            if (transform == null && GUILayout.Button("Find", GUILayout.Width(50)))
            {
                transform = FindChildTransform(_selectedPrefab.transform, searchNames);
                if (transform == null)
                {
                    Debug.Log($"Could not find transform matching: {string.Join(", ", searchNames)}");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CreateTransform(string name, Vector3 localPosition)
        {
            var newTransform = new GameObject(name).transform;
            newTransform.SetParent(_selectedPrefab.transform);
            newTransform.localPosition = localPosition;
            newTransform.localRotation = Quaternion.identity;

            EditorUtility.SetDirty(_selectedPrefab);
            FindTransforms();

            Debug.Log($"Created transform '{name}' on {_selectedPrefab.name}");
        }

        #endregion

        #region Analysis and Fix Methods

        private void AnalyzePrefab()
        {
            _setupItems.Clear();
            if (_selectedPrefab == null) return;

            // Find transforms first
            FindTransforms();

            // Core components
            var weaponAuthoring = _selectedPrefab.GetComponent<WeaponAuthoring>();
            _setupItems.Add(new SetupItem(
                "Core", "WeaponAuthoring", "",
                weaponAuthoring != null ? SetupStatus.Complete : SetupStatus.Missing,
                () => _selectedPrefab.AddComponent<WeaponAuthoring>(), "Add"));

            // Audio components
            var audioBridge = _selectedPrefab.GetComponentInChildren<WeaponAudioBridge>();
            _setupItems.Add(new SetupItem(
                "Audio", "WeaponAudioBridge", "Required for weapon sounds",
                audioBridge != null ? SetupStatus.Complete : SetupStatus.Missing,
                () => AddWeaponAudioBridge(), "Add"));

            if (audioBridge != null)
            {
                var so = new SerializedObject(audioBridge);
                var configProp = so.FindProperty("audioConfig");
                bool hasConfig = configProp.objectReferenceValue != null;

                _setupItems.Add(new SetupItem(
                    "Audio", "Audio Config", "WeaponAudioConfig asset assigned",
                    hasConfig ? SetupStatus.Complete : SetupStatus.Incomplete,
                    null, ""));

                var muzzleProp = so.FindProperty("muzzleTransform");
                bool hasMuzzle = muzzleProp.objectReferenceValue != null;
                _setupItems.Add(new SetupItem(
                    "Audio", "Muzzle Transform", "For 3D audio positioning",
                    hasMuzzle ? SetupStatus.Complete : SetupStatus.Incomplete,
                    () => AssignMuzzleToAudioBridge(audioBridge), "Assign"));
            }

            // VFX components
            var vfxAuthoring = _selectedPrefab.GetComponentInChildren<ItemVFXAuthoring>();
            _setupItems.Add(new SetupItem(
                "VFX", "ItemVFXAuthoring", "Required for muzzle flash, shell eject",
                vfxAuthoring != null ? SetupStatus.Complete : SetupStatus.Missing,
                () => _selectedPrefab.AddComponent<ItemVFXAuthoring>(), "Add"));

            if (vfxAuthoring != null)
            {
                bool hasFireVFX = HasVFXEntry(vfxAuthoring, "Fire");
                _setupItems.Add(new SetupItem(
                    "VFX", "'Fire' VFX Entry", "Muzzle flash effect",
                    hasFireVFX ? SetupStatus.Complete : SetupStatus.Incomplete,
                    null, ""));
            }

            // Transforms
            _setupItems.Add(new SetupItem(
                "Transforms", "Muzzle Point", "VFX and audio spawn point",
                _muzzleTransform != null ? SetupStatus.Complete : SetupStatus.Missing,
                () => CreateTransform("MuzzlePoint", new Vector3(0, 0, 0.5f)), "Create"));

            _setupItems.Add(new SetupItem(
                "Transforms", "Ejection Port", "Shell casing spawn point",
                _ejectionTransform != null ? SetupStatus.Complete : SetupStatus.Incomplete,
                () => CreateTransform("EjectionPort", new Vector3(0.05f, 0.05f, 0.1f)), "Create"));

            _hasAnalyzed = true;
        }

        private void FixAllMissing()
        {
            foreach (var item in _setupItems.Where(i => i.Status == SetupStatus.Missing && i.FixAction != null))
            {
                item.FixAction();
            }
            AnalyzePrefab();
        }

        private void AddWeaponAudioBridge()
        {
            if (_selectedPrefab == null) return;

            var bridge = _selectedPrefab.AddComponent<WeaponAudioBridge>();

            // Try to auto-assign muzzle
            if (_muzzleTransform != null)
            {
                var so = new SerializedObject(bridge);
                so.FindProperty("muzzleTransform").objectReferenceValue = _muzzleTransform;
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(_selectedPrefab);
            Debug.Log($"Added WeaponAudioBridge to {_selectedPrefab.name}");
        }

        private void AssignMuzzleToAudioBridge(WeaponAudioBridge bridge)
        {
            if (bridge == null || _muzzleTransform == null) return;

            var so = new SerializedObject(bridge);
            so.FindProperty("muzzleTransform").objectReferenceValue = _muzzleTransform;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedPrefab);
        }

        private void CreateNewAudioConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Weapon Audio Config",
                _newConfigName,
                "asset",
                "Choose location for the new audio config");

            if (string.IsNullOrEmpty(path)) return;

            var config = ScriptableObject.CreateInstance<WeaponAudioConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            _selectedAudioConfig = config;

            // Auto-assign to bridge if present
            var bridge = _selectedPrefab?.GetComponentInChildren<WeaponAudioBridge>();
            if (bridge != null)
            {
                var so = new SerializedObject(bridge);
                so.FindProperty("audioConfig").objectReferenceValue = config;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedPrefab);
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;

            Debug.Log($"Created WeaponAudioConfig: {path}");
        }

        private bool HasVFXEntry(ItemVFXAuthoring vfx, string id)
        {
            var so = new SerializedObject(vfx);
            var definitionsProp = so.FindProperty("_definitions");

            if (definitionsProp != null && definitionsProp.isArray)
            {
                for (int i = 0; i < definitionsProp.arraySize; i++)
                {
                    var entry = definitionsProp.GetArrayElementAtIndex(i);
                    var idProp = entry.FindPropertyRelative("ID");
                    if (idProp != null && idProp.stringValue == id)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private Transform FindChildTransform(Transform parent, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                // Direct child
                var found = parent.Find(name);
                if (found != null) return found;

                // Recursive search with case-insensitive matching
                foreach (Transform child in parent)
                {
                    if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                        return child;

                    if (child.name.ToLower().Contains(name.ToLower()))
                        return child;

                    var recursive = FindChildTransform(child, new[] { name });
                    if (recursive != null) return recursive;
                }
            }
            return null;
        }

        #endregion
    }
}
