using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using DIG.Combat.Abilities;

namespace DIG.Editor.CombatWorkstation.Modules
{
    /// <summary>
    /// EPIC 18.19: Ability Management module for the Combat Workstation.
    /// Create, edit, and manage player ability definitions and loadouts.
    /// Handles AbilityDefinitionSO, AbilityLoadoutSO, and Resources/ setup.
    /// </summary>
    public class AbilityManagementModule : ICombatModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _listScrollPosition;

        // Cached asset lists
        private AbilityDefinitionSO[] _abilityDefs;
        private AbilityLoadoutSO[] _loadouts;
        private int _selectedDefIndex = -1;
        private int _selectedLoadoutIndex = -1;

        // Tab within this module
        private int _subTab = 0;
        private readonly string[] _subTabs = { "Definitions", "Loadouts", "Quick Setup" };

        // Quick-create state
        private string _newAbilityName = "NewAbility";
        private AbilityCategory _newAbilityCategory = AbilityCategory.Attack;
        private AbilityTargetType _newAbilityTargetType = AbilityTargetType.SingleTarget;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Ability Management", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Create and manage player ability definitions and loadouts. " +
                "Use Quick Setup to auto-create the Resources/AbilityLoadout asset and sample abilities.",
                MessageType.Info);
            EditorGUILayout.Space(5);

            _subTab = GUILayout.Toolbar(_subTab, _subTabs);
            EditorGUILayout.Space(5);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_subTab)
            {
                case 0: DrawDefinitionsTab(); break;
                case 1: DrawLoadoutsTab(); break;
                case 2: DrawQuickSetupTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        // DEFINITIONS TAB
        // ============================================================

        private void DrawDefinitionsTab()
        {
            EditorGUILayout.BeginHorizontal();

            // Left panel — ability list
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200), GUILayout.ExpandHeight(true));
            DrawAbilityList();
            EditorGUILayout.EndVertical();

            // Right panel — selected ability detail
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawSelectedAbilityDetail();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAbilityList()
        {
            EditorGUILayout.LabelField("Ability Definitions", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh"))
                RefreshAbilityDefs();

            if (GUILayout.Button("+ New Ability"))
                CreateNewAbilityDefinition();

            EditorGUILayout.Space(5);

            if (_abilityDefs == null)
                RefreshAbilityDefs();

            _listScrollPosition = EditorGUILayout.BeginScrollView(_listScrollPosition, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _abilityDefs.Length; i++)
            {
                if (_abilityDefs[i] == null) continue;

                bool selected = i == _selectedDefIndex;
                var style = selected ? EditorStyles.boldLabel : EditorStyles.label;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"[{_abilityDefs[i].abilityId}] {_abilityDefs[i].displayName}", style))
                {
                    _selectedDefIndex = i;
                    Selection.activeObject = _abilityDefs[i];
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField($"{_abilityDefs.Length} definitions found", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSelectedAbilityDetail()
        {
            if (_abilityDefs == null || _selectedDefIndex < 0 || _selectedDefIndex >= _abilityDefs.Length ||
                _abilityDefs[_selectedDefIndex] == null)
            {
                EditorGUILayout.HelpBox("Select an ability from the list to view details.", MessageType.Info);
                return;
            }

            var def = _abilityDefs[_selectedDefIndex];
            EditorGUILayout.LabelField($"Editing: {def.displayName}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Draw inline inspector
            var editor = UnityEditor.Editor.CreateEditor(def);
            editor.OnInspectorGUI();
            Object.DestroyImmediate(editor);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping in Project"))
                EditorGUIUtility.PingObject(def);
            if (GUILayout.Button("Select in Inspector"))
                Selection.activeObject = def;
            if (GUILayout.Button("Duplicate"))
                DuplicateAbility(def);
            EditorGUILayout.EndHorizontal();
        }

        // ============================================================
        // LOADOUTS TAB
        // ============================================================

        private void DrawLoadoutsTab()
        {
            EditorGUILayout.LabelField("Ability Loadouts", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh"))
                RefreshLoadouts();
            if (GUILayout.Button("+ New Loadout"))
                CreateNewLoadout();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (_loadouts == null)
                RefreshLoadouts();

            // Check for Resources/ loadout
            var resourcesLoadout = UnityEngine.Resources.Load<AbilityLoadoutSO>("AbilityLoadout");
            if (resourcesLoadout == null)
            {
                EditorGUILayout.HelpBox(
                    "No AbilityLoadout found in Assets/Resources/. The bootstrap system will create an empty database. " +
                    "Use Quick Setup or create one manually.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Active loadout: Resources/AbilityLoadout ({(resourcesLoadout.abilities?.Length ?? 0)} abilities)",
                    MessageType.None);
            }

            EditorGUILayout.Space(5);

            for (int i = 0; i < _loadouts.Length; i++)
            {
                if (_loadouts[i] == null) continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                bool isActive = resourcesLoadout == _loadouts[i];
                string prefix = isActive ? "[ACTIVE] " : "";
                EditorGUILayout.LabelField($"{prefix}{_loadouts[i].name}", EditorStyles.boldLabel);

                if (GUILayout.Button("Select", GUILayout.Width(60)))
                    Selection.activeObject = _loadouts[i];

                if (!isActive && GUILayout.Button("Set Active", GUILayout.Width(80)))
                    SetAsActiveLoadout(_loadouts[i]);

                EditorGUILayout.EndHorizontal();

                int abilityCount = _loadouts[i].abilities?.Length ?? 0;
                EditorGUILayout.LabelField($"  {abilityCount} abilities, 6 slots");

                // Show slot assignments
                if (_loadouts[i].defaultSlotAbilityIds != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int s = 0; s < 6 && s < _loadouts[i].defaultSlotAbilityIds.Length; s++)
                    {
                        int id = _loadouts[i].defaultSlotAbilityIds[s];
                        string slotLabel = id >= 0 ? $"[{id}]" : "---";
                        GUILayout.Label(slotLabel, EditorStyles.miniLabel, GUILayout.Width(30));
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        // ============================================================
        // QUICK SETUP TAB
        // ============================================================

        private void DrawQuickSetupTab()
        {
            EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "One-click setup for the ability system. Creates required folders, assets, and wires everything up.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Status checks
            DrawSetupStatus();

            EditorGUILayout.Space(10);

            // Quick create single ability
            EditorGUILayout.LabelField("Quick Create Ability", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            _newAbilityName = EditorGUILayout.TextField("Name", _newAbilityName);
            _newAbilityCategory = (AbilityCategory)EditorGUILayout.EnumPopup("Category", _newAbilityCategory);
            _newAbilityTargetType = (AbilityTargetType)EditorGUILayout.EnumPopup("Target Type", _newAbilityTargetType);

            if (GUILayout.Button("Create Ability Definition"))
                QuickCreateAbility();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Full auto-setup
            EditorGUILayout.LabelField("Full Auto-Setup", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.HelpBox(
                "Creates:\n" +
                "- Assets/Resources/ folder (if needed)\n" +
                "- Assets/Resources/AbilityLoadout.asset\n" +
                "- Assets/Data/Abilities/ folder for definitions\n" +
                "- 3 sample ability definitions (Basic Attack, Fireball, Heal)\n" +
                "- Assigns samples to loadout slots 0-2",
                MessageType.None);

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Run Full Auto-Setup", GUILayout.Height(30)))
                RunFullAutoSetup();
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Player prefab setup helper
            EditorGUILayout.LabelField("Player Prefab Setup", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.HelpBox(
                "The player prefab needs an AbilityLoadoutAuthoring component " +
                "referencing the loadout. Select the player prefab and click below.",
                MessageType.None);

            if (GUILayout.Button("Find & Wire Player Prefab"))
                FindAndWirePlayerPrefab();

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupStatus()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            bool resourcesFolderExists = Directory.Exists("Assets/Resources");
            bool loadoutExists = UnityEngine.Resources.Load<AbilityLoadoutSO>("AbilityLoadout") != null;
            bool dataFolderExists = Directory.Exists("Assets/Data/Abilities");

            DrawStatusRow("Assets/Resources/ folder", resourcesFolderExists);
            DrawStatusRow("Resources/AbilityLoadout.asset", loadoutExists);
            DrawStatusRow("Assets/Data/Abilities/ folder", dataFolderExists);

            RefreshAbilityDefs();
            DrawStatusRow($"Ability definitions ({_abilityDefs.Length} found)", _abilityDefs.Length > 0);

            // Check player prefab
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab Warrok_Server");
            bool hasPrefab = prefabGuids.Length > 0;
            if (hasPrefab)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                bool hasAuthoring = prefab != null && prefab.GetComponentInChildren<AbilityLoadoutAuthoring>() != null;
                DrawStatusRow("Player prefab AbilityLoadoutAuthoring", hasAuthoring);
            }
            else
            {
                DrawStatusRow("Player prefab found", false);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusRow(string label, bool ok)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ok ? "OK" : "MISSING", ok
                ? new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.green } }
                : new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.5f, 0f) } },
                GUILayout.Width(50));
            EditorGUILayout.LabelField(label);
            EditorGUILayout.EndHorizontal();
        }

        // ============================================================
        // ACTIONS
        // ============================================================

        private void RefreshAbilityDefs()
        {
            var guids = AssetDatabase.FindAssets("t:AbilityDefinitionSO");
            var defs = new List<AbilityDefinitionSO>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // Only include player ability definitions (not AI ones)
                if (path.Contains("/AI/")) continue;
                var def = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(path);
                if (def != null) defs.Add(def);
            }
            defs.Sort((a, b) => a.abilityId.CompareTo(b.abilityId));
            _abilityDefs = defs.ToArray();
        }

        private void RefreshLoadouts()
        {
            var guids = AssetDatabase.FindAssets("t:AbilityLoadoutSO");
            var loadouts = new List<AbilityLoadoutSO>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var loadout = AssetDatabase.LoadAssetAtPath<AbilityLoadoutSO>(path);
                if (loadout != null) loadouts.Add(loadout);
            }
            _loadouts = loadouts.ToArray();
        }

        private void CreateNewAbilityDefinition()
        {
            EnsureDirectory("Assets/Data/Abilities");

            // Find next available ID
            RefreshAbilityDefs();
            int nextId = 1;
            foreach (var def in _abilityDefs)
            {
                if (def != null && def.abilityId >= nextId)
                    nextId = def.abilityId + 1;
            }

            var newDef = ScriptableObject.CreateInstance<AbilityDefinitionSO>();
            newDef.abilityId = nextId;
            newDef.displayName = $"Ability {nextId}";

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Ability Definition", $"Ability_{nextId}", "asset",
                "Choose location for the new ability definition",
                "Assets/Data/Abilities");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(newDef, path);
                AssetDatabase.SaveAssets();
                RefreshAbilityDefs();
                Selection.activeObject = newDef;
                Debug.Log($"[AbilityManagement] Created ability definition: {path}");
            }
            else
            {
                Object.DestroyImmediate(newDef);
            }
        }

        private void DuplicateAbility(AbilityDefinitionSO source)
        {
            EnsureDirectory("Assets/Data/Abilities");

            RefreshAbilityDefs();
            int nextId = 1;
            foreach (var def in _abilityDefs)
            {
                if (def != null && def.abilityId >= nextId)
                    nextId = def.abilityId + 1;
            }

            var copy = Object.Instantiate(source);
            copy.abilityId = nextId;
            copy.displayName = source.displayName + " (Copy)";

            string path = AssetDatabase.GetAssetPath(source);
            string dir = Path.GetDirectoryName(path);
            string newPath = Path.Combine(dir, $"{source.name}_Copy.asset");
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

            AssetDatabase.CreateAsset(copy, newPath);
            AssetDatabase.SaveAssets();
            RefreshAbilityDefs();
            Selection.activeObject = copy;
            Debug.Log($"[AbilityManagement] Duplicated ability: {newPath}");
        }

        private void CreateNewLoadout()
        {
            var loadout = ScriptableObject.CreateInstance<AbilityLoadoutSO>();

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Ability Loadout", "AbilityLoadout", "asset",
                "Choose location for the ability loadout",
                "Assets/Resources");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(loadout, path);
                AssetDatabase.SaveAssets();
                RefreshLoadouts();
                Selection.activeObject = loadout;
                Debug.Log($"[AbilityManagement] Created loadout: {path}");
            }
            else
            {
                Object.DestroyImmediate(loadout);
            }
        }

        private void SetAsActiveLoadout(AbilityLoadoutSO loadout)
        {
            EnsureDirectory("Assets/Resources");

            string currentPath = AssetDatabase.GetAssetPath(loadout);
            string targetPath = "Assets/Resources/AbilityLoadout.asset";

            if (currentPath == targetPath) return;

            // If there's already one at the target, delete it
            if (File.Exists(targetPath))
                AssetDatabase.DeleteAsset(targetPath);

            // Copy to Resources
            AssetDatabase.CopyAsset(currentPath, targetPath);
            AssetDatabase.SaveAssets();
            RefreshLoadouts();
            Debug.Log($"[AbilityManagement] Set active loadout: {targetPath}");
        }

        private void QuickCreateAbility()
        {
            EnsureDirectory("Assets/Data/Abilities");

            RefreshAbilityDefs();
            int nextId = 1;
            foreach (var def in _abilityDefs)
            {
                if (def != null && def.abilityId >= nextId)
                    nextId = def.abilityId + 1;
            }

            var newDef = ScriptableObject.CreateInstance<AbilityDefinitionSO>();
            newDef.abilityId = nextId;
            newDef.displayName = _newAbilityName;
            newDef.category = _newAbilityCategory;
            newDef.targetType = _newAbilityTargetType;

            // Set sensible defaults based on category
            switch (_newAbilityCategory)
            {
                case AbilityCategory.Attack:
                    newDef.damageBase = 50f;
                    newDef.activeDuration = 0.3f;
                    newDef.cooldown = 3f;
                    break;
                case AbilityCategory.Heal:
                    newDef.damageBase = -30f; // Negative = heal
                    newDef.targetType = AbilityTargetType.Self;
                    newDef.cooldown = 10f;
                    newDef.castTime = 1.5f;
                    break;
                case AbilityCategory.Buff:
                    newDef.targetType = AbilityTargetType.Self;
                    newDef.cooldown = 30f;
                    newDef.activeDuration = 10f;
                    break;
                case AbilityCategory.Movement:
                    newDef.targetType = AbilityTargetType.Self;
                    newDef.cooldown = 15f;
                    newDef.activeDuration = 0.2f;
                    break;
            }

            string fileName = _newAbilityName.Replace(" ", "_");
            string path = $"Assets/Data/Abilities/{fileName}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(newDef, path);
            AssetDatabase.SaveAssets();
            RefreshAbilityDefs();
            Selection.activeObject = newDef;
            _newAbilityName = "NewAbility";
            Debug.Log($"[AbilityManagement] Quick-created ability: {path}");
        }

        private void RunFullAutoSetup()
        {
            EnsureDirectory("Assets/Resources");
            EnsureDirectory("Assets/Data/Abilities");

            // Create 3 sample abilities
            var basicAttack = CreateSampleAbility(1, "Basic Attack", AbilityCategory.Attack,
                AbilityTargetType.SingleTarget, 30f, 5f, 0f, 0.3f, 0.2f, 1.5f, 0.5f);
            var fireball = CreateSampleAbility(2, "Fireball", AbilityCategory.Attack,
                AbilityTargetType.Projectile, 80f, 15f, 1.2f, 0.5f, 0.3f, 8f, 1f);
            var heal = CreateSampleAbility(3, "Heal", AbilityCategory.Heal,
                AbilityTargetType.Self, -50f, 10f, 2f, 0.5f, 0f, 15f, 1f);

            // Set fireball resource cost
            fireball.costResource = DIG.Combat.Resources.ResourceType.Mana;
            fireball.costAmount = 25f;
            EditorUtility.SetDirty(fireball);

            heal.costResource = DIG.Combat.Resources.ResourceType.Mana;
            heal.costAmount = 40f;
            EditorUtility.SetDirty(heal);

            // Create loadout
            var loadout = ScriptableObject.CreateInstance<AbilityLoadoutSO>();
            loadout.abilities = new[] { basicAttack, fireball, heal };
            loadout.defaultSlotAbilityIds = new[] { 1, 2, 3, -1, -1, -1 };

            string loadoutPath = "Assets/Resources/AbilityLoadout.asset";
            if (File.Exists(loadoutPath))
                AssetDatabase.DeleteAsset(loadoutPath);

            AssetDatabase.CreateAsset(loadout, loadoutPath);
            AssetDatabase.SaveAssets();

            RefreshAbilityDefs();
            RefreshLoadouts();

            Debug.Log("[AbilityManagement] Full auto-setup complete: 3 abilities + loadout in Resources/");
            EditorUtility.DisplayDialog("Setup Complete",
                "Created:\n" +
                "- Basic Attack (ID 1)\n" +
                "- Fireball (ID 2)\n" +
                "- Heal (ID 3)\n" +
                "- Resources/AbilityLoadout.asset\n\n" +
                "Next: Add AbilityLoadoutAuthoring to your player prefab.",
                "OK");
        }

        private AbilityDefinitionSO CreateSampleAbility(int id, string name, AbilityCategory category,
            AbilityTargetType targetType, float damage, float variance, float castTime,
            float activeDuration, float recovery, float cooldown, float gcd)
        {
            string fileName = name.Replace(" ", "_");
            string path = $"Assets/Data/Abilities/{fileName}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            var def = ScriptableObject.CreateInstance<AbilityDefinitionSO>();
            def.abilityId = id;
            def.displayName = name;
            def.category = category;
            def.targetType = targetType;
            def.damageBase = damage;
            def.damageVariance = variance;
            def.castTime = castTime;
            def.activeDuration = activeDuration;
            def.recoveryTime = recovery;
            def.cooldown = cooldown;
            def.globalCooldown = gcd;

            AssetDatabase.CreateAsset(def, path);
            return def;
        }

        private void FindAndWirePlayerPrefab()
        {
            var loadout = UnityEngine.Resources.Load<AbilityLoadoutSO>("AbilityLoadout");
            if (loadout == null)
            {
                EditorUtility.DisplayDialog("Missing Loadout",
                    "No AbilityLoadout found in Resources/. Run Full Auto-Setup first.", "OK");
                return;
            }

            // Search for player prefab
            var guids = AssetDatabase.FindAssets("t:Prefab Warrok_Server");
            if (guids.Length == 0)
            {
                guids = AssetDatabase.FindAssets("t:Prefab Player");
            }

            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Prefab Not Found",
                    "Could not find player prefab (searched for Warrok_Server, Player). " +
                    "Manually add AbilityLoadoutAuthoring to your player prefab.", "OK");
                return;
            }

            var prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null) return;

            // Check if already has it
            var existing = prefab.GetComponentInChildren<AbilityLoadoutAuthoring>();
            if (existing != null)
            {
                existing.loadout = loadout;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Updated",
                    $"Updated existing AbilityLoadoutAuthoring on {prefab.name} with loadout reference.", "OK");
                return;
            }

            // Add it
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                var authoring = instance.AddComponent<AbilityLoadoutAuthoring>();
                authoring.loadout = loadout;
                PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
                Object.DestroyImmediate(instance);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Success",
                    $"Added AbilityLoadoutAuthoring to {prefab.name} and assigned loadout.\n" +
                    "Remember to re-bake any subscenes containing this prefab.", "OK");
                Debug.Log($"[AbilityManagement] Added AbilityLoadoutAuthoring to {prefabPath}");
            }
        }

        // ============================================================
        // UTILITIES
        // ============================================================

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
