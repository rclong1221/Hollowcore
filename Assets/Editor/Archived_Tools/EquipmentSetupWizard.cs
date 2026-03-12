using UnityEditor;
using UnityEngine;
using DIG.Items.Definitions;
using System.Collections.Generic;
using System.IO;

namespace DIG.Items.Editor.Wizards
{
    /// <summary>
    /// EPIC14.6 Phase 1 - Equipment System Setup Wizard
    /// Multi-step wizard for initial project configuration.
    /// </summary>
    public class EquipmentSetupWizard : EditorWindow
    {
        // Wizard state
        private int _currentStep = 0;
        private const int TOTAL_STEPS = 5;

        // Step 1: Game Type
        private GameTypeTemplate _selectedTemplate = GameTypeTemplate.ThirdPersonShooter;

        // Step 2: Animation Backend
        private AnimationBackend _selectedBackend = AnimationBackend.OpsiveUCC;

        // Step 3: Input Configuration
        private InputConfigPreset _selectedInputConfig = InputConfigPreset.DefaultFPS;

        // Step 5: Player Prefab
        private GameObject _playerPrefab;
        private bool _addProviderComponent = true;
        private bool _assignSlotDefinitions = true;

        // Scroll position
        private Vector2 _scrollPos;

        // Validation
        private List<string> _validationMessages = new List<string>();

        public enum GameTypeTemplate
        {
            ThirdPersonShooter,
            SoulsLikeAction,
            SurvivalGame,
            FullRPG,
            Custom
        }

        public enum AnimationBackend
        {
            OpsiveUCC,
            StandardMecanim,
            Custom
        }

        public enum InputConfigPreset
        {
            DefaultFPS,
            GamepadFriendly,
            Custom
        }

        [MenuItem("DIG/Wizards/1. Setup: Project Configuration")]
        public static void ShowWindow()
        {
            var window = GetWindow<EquipmentSetupWizard>("Equipment Setup Wizard");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(10);

            DrawProgressBar();
            EditorGUILayout.Space(20);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentStep)
            {
                case 0: DrawStep1_GameType(); break;
                case 1: DrawStep2_AnimationBackend(); break;
                case 2: DrawStep3_InputConfiguration(); break;
                case 3: DrawStep4_FolderStructure(); break;
                case 4: DrawStep5_PlayerPrefab(); break;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(20);
            DrawNavigationButtons();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Equipment System Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Configure your equipment system in a few simple steps.", EditorStyles.helpBox);
        }

        private void DrawProgressBar()
        {
            EditorGUILayout.BeginHorizontal();
            string[] stepNames = { "Game Type", "Animation", "Input", "Folders", "Player" };
            for (int i = 0; i < TOTAL_STEPS; i++)
            {
                var style = i == _currentStep ? EditorStyles.toolbarButton : EditorStyles.miniButtonMid;
                var color = i < _currentStep ? Color.green : (i == _currentStep ? Color.cyan : Color.gray);
                GUI.backgroundColor = color;
                GUILayout.Button(stepNames[i], style, GUILayout.ExpandWidth(true));
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        #region Step 1: Game Type

        private void DrawStep1_GameType()
        {
            EditorGUILayout.LabelField("Step 1: Select Game Type", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Choose a template that best matches your game. This determines which equipment slots and weapon categories are created by default.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _selectedTemplate = (GameTypeTemplate)EditorGUILayout.EnumPopup("Game Template", _selectedTemplate);

            EditorGUILayout.Space(10);

            // Show template description
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            switch (_selectedTemplate)
            {
                case GameTypeTemplate.ThirdPersonShooter:
                    EditorGUILayout.LabelField("Third-Person Shooter", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Slots: MainHand, OffHand");
                    EditorGUILayout.LabelField("Categories: Gun, Pistol, Rifle, Knife, Grenade, Shield");
                    break;
                case GameTypeTemplate.SoulsLikeAction:
                    EditorGUILayout.LabelField("Souls-like Action", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Slots: MainHand, OffHand");
                    EditorGUILayout.LabelField("Categories: Sword, Katana, Shield, Magic, Bow");
                    break;
                case GameTypeTemplate.SurvivalGame:
                    EditorGUILayout.LabelField("Survival Game", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Slots: MainHand, OffHand, Tool");
                    EditorGUILayout.LabelField("Categories: Gun, Melee, Tool, Consumable");
                    break;
                case GameTypeTemplate.FullRPG:
                    EditorGUILayout.LabelField("Full RPG", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Slots: MainHand, OffHand, Head, Chest, Hands, Legs, Feet");
                    EditorGUILayout.LabelField("Categories: All weapon and armor types");
                    break;
                case GameTypeTemplate.Custom:
                    EditorGUILayout.LabelField("Custom", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("No slots or categories created automatically.");
                    EditorGUILayout.LabelField("Configure everything manually after wizard completion.");
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Step 2: Animation Backend

        private void DrawStep2_AnimationBackend()
        {
            EditorGUILayout.LabelField("Step 2: Animation Backend", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Select which animation system your project uses. This determines which bridge component is added to the player.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _selectedBackend = (AnimationBackend)EditorGUILayout.EnumPopup("Animation Backend", _selectedBackend);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            switch (_selectedBackend)
            {
                case AnimationBackend.OpsiveUCC:
                    EditorGUILayout.LabelField("Opsive Ultimate Character Controller", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Uses existing WeaponEquipVisualBridge and Opsive integration.");
                    EditorGUILayout.LabelField("Best for projects already using Opsive assets.");
                    break;
                case AnimationBackend.StandardMecanim:
                    EditorGUILayout.LabelField("Standard Unity Mecanim", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Uses MecanimAnimatorBridge for direct Animator control.");
                    EditorGUILayout.LabelField("Best for projects with custom animation controllers.");
                    break;
                case AnimationBackend.Custom:
                    EditorGUILayout.LabelField("Custom Implementation", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Creates a placeholder IAnimatorBridge for you to implement.");
                    EditorGUILayout.LabelField("Best for projects with unique animation requirements.");
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Step 3: Input Configuration

        private void DrawStep3_InputConfiguration()
        {
            EditorGUILayout.LabelField("Step 3: Input Configuration", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Configure how equipment slots are bound to input. This sets the RequiredModifier on slot definitions.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _selectedInputConfig = (InputConfigPreset)EditorGUILayout.EnumPopup("Input Preset", _selectedInputConfig);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            switch (_selectedInputConfig)
            {
                case InputConfigPreset.DefaultFPS:
                    EditorGUILayout.LabelField("Default FPS Bindings", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Main Hand: Keys 1-9 (no modifier)");
                    EditorGUILayout.LabelField("Off Hand: Shift + 1-9");
                    break;
                case InputConfigPreset.GamepadFriendly:
                    EditorGUILayout.LabelField("Gamepad-Friendly Bindings", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Main Hand: D-Pad directions");
                    EditorGUILayout.LabelField("Off Hand: Hold LB + D-Pad");
                    EditorGUILayout.LabelField("(Requires Input Action assets)");
                    break;
                case InputConfigPreset.Custom:
                    EditorGUILayout.LabelField("Custom Bindings", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("No default bindings configured.");
                    EditorGUILayout.LabelField("Modify slot definitions manually after wizard.");
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Step 4: Folder Structure

        private void DrawStep4_FolderStructure()
        {
            EditorGUILayout.LabelField("Step 4: Folder Structure", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "The wizard will create the following folder structure for equipment content.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Assets/", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("├── Content/");
            EditorGUILayout.LabelField("│   ├── Weapons/");
            EditorGUILayout.LabelField("│   │   ├── Categories/");
            EditorGUILayout.LabelField("│   │   ├── Prefabs/");
            EditorGUILayout.LabelField("│   │   └── InputProfiles/");
            EditorGUILayout.LabelField("│   └── Equipment/");
            EditorGUILayout.LabelField("│       ├── Definitions/");
            EditorGUILayout.LabelField("│       │   ├── Slots/");
            EditorGUILayout.LabelField("│       │   └── Categories/");
            EditorGUILayout.LabelField("│       └── Configs/");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create Folders Now", GUILayout.Height(30)))
            {
                CreateFolderStructure();
                EditorUtility.DisplayDialog("Success", "Folder structure created successfully!", "OK");
            }
        }

        #endregion

        #region Step 5: Player Prefab

        private void DrawStep5_PlayerPrefab()
        {
            EditorGUILayout.LabelField("Step 5: Player Prefab Configuration", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Select your player prefab to automatically add and configure the equipment provider component.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _playerPrefab = (GameObject)EditorGUILayout.ObjectField("Player Prefab", _playerPrefab, typeof(GameObject), false);

            if (_playerPrefab == null)
            {
                EditorGUILayout.HelpBox("Drag your player prefab here, or click 'Find in Scene' to locate it.", MessageType.Warning);

                if (GUILayout.Button("Find in Scene"))
                {
                    // Try to find a player in the scene
                    var player = GameObject.FindWithTag("Player");
                    if (player != null)
                    {
                        _playerPrefab = PrefabUtility.GetCorrespondingObjectFromSource(player);
                        if (_playerPrefab == null)
                        {
                            EditorUtility.DisplayDialog("Info", "Found player in scene but it's not a prefab instance. Please select the prefab asset directly.", "OK");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Not Found", "No GameObject with 'Player' tag found in scene.", "OK");
                    }
                }
            }
            else
            {
                EditorGUILayout.Space(10);

                _addProviderComponent = EditorGUILayout.Toggle("Add DIGEquipmentProvider", _addProviderComponent);
                _assignSlotDefinitions = EditorGUILayout.Toggle("Assign Slot Definitions", _assignSlotDefinitions);

                EditorGUILayout.Space(10);

                // Show current state
                var existingProvider = _playerPrefab.GetComponent<DIGEquipmentProvider>();
                if (existingProvider != null)
                {
                    EditorGUILayout.HelpBox("Player already has DIGEquipmentProvider component.", MessageType.Info);
                }
            }

            EditorGUILayout.Space(20);

            // Validation messages
            if (_validationMessages.Count > 0)
            {
                EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
                foreach (var msg in _validationMessages)
                {
                    EditorGUILayout.HelpBox(msg, MessageType.Warning);
                }
            }
        }

        #endregion

        #region Navigation

        private void DrawNavigationButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // Back button
            GUI.enabled = _currentStep > 0;
            if (GUILayout.Button("← Back", GUILayout.Height(30), GUILayout.Width(100)))
            {
                _currentStep--;
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            // Next / Finish button
            if (_currentStep < TOTAL_STEPS - 1)
            {
                if (GUILayout.Button("Next →", GUILayout.Height(30), GUILayout.Width(100)))
                {
                    _currentStep++;
                }
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Finish Setup", GUILayout.Height(30), GUILayout.Width(120)))
                {
                    ExecuteSetup();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Execution

        private void ExecuteSetup()
        {
            _validationMessages.Clear();

            // Step 1: Create folder structure
            CreateFolderStructure();

            // Step 2: Create slots based on template
            CreateSlotsForTemplate();

            // Step 3: Create categories based on template
            CreateCategoriesForTemplate();

            // Step 4: Apply input configuration
            ApplyInputConfiguration();

            // Step 5: Configure player prefab
            ConfigurePlayerPrefab();

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Setup Complete",
                "Equipment system has been configured!\n\n" +
                "• Folder structure created\n" +
                "• Slot definitions created\n" +
                "• Category definitions created\n" +
                "• Input bindings configured\n" +
                (_playerPrefab != null ? "• Player prefab configured" : "• Player prefab: Skipped (no prefab selected)"),
                "OK");

            Debug.Log("[EquipmentSetupWizard] Setup completed successfully!");

            // Prompt to open Weapon Creation Wizard
            if (EditorUtility.DisplayDialog("Next Steps",
                "Setup is complete! Would you like to open the Weapon Creation Wizard now to start adding content?",
                "Yes, Open Weapon Wizard", "No, I'm Done"))
            {
                EditorApplication.ExecuteMenuItem("DIG/Wizards/2. Create: New Weapon");
            }
        }

        private void CreateFolderStructure()
        {
            string[] folders = new string[]
            {
                "Assets/Content",
                "Assets/Content/Weapons",
                "Assets/Content/Weapons/Categories",
                "Assets/Content/Weapons/Prefabs",
                "Assets/Content/Weapons/InputProfiles",
                "Assets/Content/Equipment",
                "Assets/Content/Equipment/Definitions",
                "Assets/Content/Equipment/Definitions/Slots",
                "Assets/Content/Equipment/Definitions/Categories",
                "Assets/Content/Equipment/Configs"
            };

            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    var parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                    var name = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }

            Debug.Log("[EquipmentSetupWizard] Folder structure created.");
        }

        private void CreateSlotsForTemplate()
        {
            if (_selectedTemplate == GameTypeTemplate.Custom)
            {
                Debug.Log("[EquipmentSetupWizard] Custom template selected - no slots created.");
                return;
            }

            // Use existing Setup_EquipmentDefaults for MainHand and OffHand
            Setup_EquipmentDefaults.CreateDefaultSlots();

            // For RPG, create additional armor slots
            if (_selectedTemplate == GameTypeTemplate.FullRPG)
            {
                CreateArmorSlots();
            }

            // For Survival, create Tool slot
            if (_selectedTemplate == GameTypeTemplate.SurvivalGame)
            {
                CreateToolSlot();
            }

            Debug.Log("[EquipmentSetupWizard] Slots created for template: " + _selectedTemplate);
        }

        private void CreateArmorSlots()
        {
            string slotsPath = "Assets/Content/Equipment/Definitions/Slots";

            var armorSlots = new (string id, string displayName, HumanBodyBones bone, int index)[]
            {
                ("Head", "Head", HumanBodyBones.Head, 2),
                ("Chest", "Chest", HumanBodyBones.Chest, 3),
                ("Hands", "Hands", HumanBodyBones.RightHand, 4),
                ("Legs", "Legs", HumanBodyBones.RightUpperLeg, 5),
                ("Feet", "Feet", HumanBodyBones.RightFoot, 6)
            };

            foreach (var (id, displayName, bone, index) in armorSlots)
            {
                var slot = ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
                slot.SlotID = id;
                slot.DisplayName = displayName;
                slot.SlotIndex = index;
                slot.AttachmentBone = bone;
                slot.AnimatorParamPrefix = $"Slot{index}";
                slot.RenderMode = SlotRenderMode.AlwaysVisible;
                slot.UsesNumericKeys = false; // Armor uses equipment menu, not hotkeys

                string path = $"{slotsPath}/{id}.asset";
                if (!File.Exists(Application.dataPath.Replace("Assets", "") + path))
                {
                    AssetDatabase.CreateAsset(slot, path);
                }
            }
        }

        private void CreateToolSlot()
        {
            string slotsPath = "Assets/Content/Equipment/Definitions/Slots";

            var tool = ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
            tool.SlotID = "Tool";
            tool.DisplayName = "Tool";
            tool.SlotIndex = 2;
            tool.AttachmentBone = HumanBodyBones.RightHand;
            tool.AnimatorParamPrefix = "Slot2";
            tool.RenderMode = SlotRenderMode.OnlyWhenEquipped;
            tool.UsesNumericKeys = true;
            tool.RequiredModifier = ModifierKey.Ctrl; // Ctrl + 1-9 for tools

            string path = $"{slotsPath}/Tool.asset";
            if (!File.Exists(Application.dataPath.Replace("Assets", "") + path))
            {
                AssetDatabase.CreateAsset(tool, path);
            }
        }

        private void CreateCategoriesForTemplate()
        {
            if (_selectedTemplate == GameTypeTemplate.Custom)
            {
                Debug.Log("[EquipmentSetupWizard] Custom template selected - no categories created.");
                return;
            }

            // Use existing Setup_EquipmentDefaults for all categories
            Setup_EquipmentDefaults.CreateDefaultCategories();

            Debug.Log("[EquipmentSetupWizard] Categories created for template: " + _selectedTemplate);
        }

        private void ApplyInputConfiguration()
        {
            // For now, the input configuration is baked into the slot creation
            // Future: Could modify existing slot assets based on InputConfigPreset
            Debug.Log("[EquipmentSetupWizard] Input configuration: " + _selectedInputConfig);
        }

        private void ConfigurePlayerPrefab()
        {
            if (_playerPrefab == null)
            {
                Debug.Log("[EquipmentSetupWizard] No player prefab selected - skipping prefab configuration.");
                return;
            }

            // Open prefab for editing
            string prefabPath = AssetDatabase.GetAssetPath(_playerPrefab);
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var provider = prefabRoot.GetComponent<DIGEquipmentProvider>();

                if (_addProviderComponent && provider == null)
                {
                    provider = prefabRoot.AddComponent<DIGEquipmentProvider>();
                    Debug.Log("[EquipmentSetupWizard] Added DIGEquipmentProvider to player prefab.");
                }

                if (_assignSlotDefinitions && provider != null)
                {
                    // Load slot definitions and assign them
                    var slots = new List<EquipmentSlotDefinition>();

                    string slotsPath = "Assets/Content/Equipment/Definitions/Slots";
                    var slotGuids = AssetDatabase.FindAssets("t:EquipmentSlotDefinition", new[] { slotsPath });

                    foreach (var guid in slotGuids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var slot = AssetDatabase.LoadAssetAtPath<EquipmentSlotDefinition>(path);
                        if (slot != null)
                            slots.Add(slot);
                    }

                    // Sort by SlotIndex
                    slots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

                    // Use reflection to set the private _slotDefinitions field
                    var fieldInfo = typeof(DIGEquipmentProvider).GetField("_slotDefinitions",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (fieldInfo != null)
                    {
                        fieldInfo.SetValue(provider, slots.ToArray());
                        Debug.Log($"[EquipmentSetupWizard] Assigned {slots.Count} slot definitions to player prefab.");
                    }
                }

                // Save prefab changes
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        #endregion
    }
}
