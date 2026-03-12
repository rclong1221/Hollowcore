using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using DIG.Targeting.Theming;
using TMPro;

namespace DIG.Editor.Setup
{
    /// <summary>
    /// EPIC 15.9: Combat UI Setup Tool
    /// One-click setup for all Combat Feedback & Floating UI systems.
    /// Unity 6.2+: Uses UI Toolkit with WorldSpace mode + URP shaders.
    /// Menu: DIG/Setup/Combat UI
    /// </summary>
    public class CombatUISetupTool : EditorWindow
    {
        // Paths
        private const string PREFAB_ROOT = "Assets/Prefabs/UI";
        private const string CONFIG_ROOT = "Assets/Data/Config";
        private const string DAMAGE_NUMBERS_PATH = "Assets/Prefabs/UI/DamageNumbers";
        private const string WORLD_SPACE_PATH = "Assets/Prefabs/UI/WorldSpace";
        private const string FLOATING_TEXT_PATH = "Assets/Prefabs/UI/FloatingText";
        private const string SHADER_PATH = "Assets/Shaders/UI";
        private const string MATERIAL_PATH = "Assets/Materials/UI";
        private const string UXML_PATH = "Assets/UI/Combat";
        
        // Status tracking
        private bool _hasDamageNumbersPro;
        private bool _hasFEEL;
        private bool _hasBootstrapInScene;
        private bool _hasConfigAssets;
        private bool _hasPrefabs;
        private bool _hasViews;
        private bool _hasShaders;
        private bool _hasMaterials;
        private bool _hasOldUGUIFiles;
        private bool _hasValidPanelSettings;
        
        private Vector2 _scrollPos;
        
        [MenuItem("DIG/Setup/Combat UI")]
        public static void ShowWindow()
        {
            var window = GetWindow<CombatUISetupTool>("Combat UI Setup");
            window.minSize = new Vector2(450, 600);
            window.RefreshStatus();
        }
        
        private void OnEnable()
        {
            RefreshStatus();
        }
        
        private void OnFocus()
        {
            RefreshStatus();
        }
        
        private void RefreshStatus()
        {
            _hasDamageNumbersPro = Directory.Exists("Assets/DamageNumbersPro") || 
                                   AssetDatabase.FindAssets("t:Script DamageNumber").Length > 0;
            _hasFEEL = AssetDatabase.FindAssets("t:Script MMF_Player").Length > 0;
            _hasBootstrapInScene = Object.FindFirstObjectByType<DIG.Combat.UI.CombatUIBootstrap>() != null;
            _hasConfigAssets = CheckConfigAssets();
            _hasPrefabs = CheckPrefabs();
            _hasViews = CheckViews();
            _hasShaders = CheckShaders();
            _hasMaterials = CheckMaterials();
            _hasOldUGUIFiles = CheckOldUGUIFiles();
            _hasValidPanelSettings = CheckPanelSettings();
            Repaint();
        }
        
        private bool CheckPanelSettings()
        {
            // Check if the World Space Panel Settings asset exists and is correct
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Data/UI/WorldSpacePanelSettings.asset");
            if (settings == null) return false;
            
            // Should be ConstantPixelSize for predictable world scaling
            return settings.scaleMode == PanelScaleMode.ConstantPixelSize;
        }
        
        private bool CheckConfigAssets()
        {
            return AssetDatabase.FindAssets("t:ScriptableObject", new[] { CONFIG_ROOT }).Length >= 3;
        }
        
        private bool CheckPrefabs()
        {
            return Directory.Exists(WORLD_SPACE_PATH) && 
                   Directory.GetFiles(WORLD_SPACE_PATH, "*.prefab").Length >= 1;
        }
        
        private bool CheckViews()
        {
            // Check if any combat UI views exist in scene
            return Object.FindFirstObjectByType<DIG.Combat.UI.Views.EnhancedHitmarkerView>() != null ||
                   Object.FindFirstObjectByType<DIG.Combat.UI.Views.ComboCounterView>() != null;
        }
        
        private bool CheckShaders()
        {
            return File.Exists($"{SHADER_PATH}/CombatUI_HealthBar.shader") &&
                   File.Exists($"{SHADER_PATH}/CombatUI_RadialFill.shader") &&
                   File.Exists($"{SHADER_PATH}/CombatUI_Glow.shader");
        }
        
        private bool CheckMaterials()
        {
            return File.Exists($"{MATERIAL_PATH}/CombatUI_HealthBar.mat") &&
                   File.Exists($"{MATERIAL_PATH}/CombatUI_RadialFill.mat");
        }
        
        private bool CheckOldUGUIFiles()
        {
            // Check for old UGUI-based prefabs that should be cleaned up
            var oldPrefabs = new[] {
                $"{WORLD_SPACE_PATH}/EnemyHealthBar_UGUI.prefab",
                $"{FLOATING_TEXT_PATH}/FloatingTextElement_UGUI.prefab",
                $"{WORLD_SPACE_PATH}/InteractionRing_UGUI.prefab"
            };
            foreach (var path in oldPrefabs)
            {
                if (File.Exists(path)) return true;
            }
            return false;
        }
        
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawPrerequisites();
            EditorGUILayout.Space(10);
            
            DrawShaderAndMaterials();
            EditorGUILayout.Space(10);
            
            DrawSceneSetup();
            EditorGUILayout.Space(10);
            
            DrawAssetCreation();
            EditorGUILayout.Space(10);
            
            DrawCleanup();
            EditorGUILayout.Space(10);
            
            DrawQuickSetup();
            EditorGUILayout.Space(10);
            
            DrawConfigurationCheck();
            EditorGUILayout.Space(10);
            
            DrawRefreshButton();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("EPIC 15.9: Combat UI Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Combat Feedback & Floating UI Systems (Unity 6.2+)\n" +
                "• ALL UI uses UI Toolkit (screen-space & world-space)\n" +
                "• Custom URP shaders for glow, radial fill, health bars\n" +
                "• No UGUI dependencies for Combat UI",
                MessageType.Info);
        }
        
        private void DrawPrerequisites()
        {
            EditorGUILayout.LabelField("Prerequisites", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusRow("Damage Numbers Pro", _hasDamageNumbersPro, 
                    "Install from Asset Store");
                DrawStatusRow("FEEL (More Mountains)", _hasFEEL, 
                    "Install from Asset Store");
                DrawStatusRow("Unity 6.2+ (UI Toolkit WorldSpace)", true, 
                    "Required for world-space UI Toolkit");
            }
        }
        
        private void DrawShaderAndMaterials()
        {
            EditorGUILayout.LabelField("Shaders & Materials", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusRow("URP Combat UI Shaders", _hasShaders);
                DrawStatusRow("Shader Materials", _hasMaterials);
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Create Shader Materials", GUILayout.Height(25)))
                {
                    CreateShaderMaterials();
                }
                
                EditorGUILayout.HelpBox(
                    "Shaders located in: Assets/Shaders/UI/\n" +
                    "• CombatUI_HealthBar - Health bars with trail damage indicator\n" +
                    "• CombatUI_RadialFill - Radial progress rings\n" +
                    "• CombatUI_Glow - Glowing UI elements\n" +
                    "• CombatUI_Hitmarker - Animated hitmarkers",
                    MessageType.None);
            }
        }
        
        private void DrawSceneSetup()
        {
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusRow("CombatUIBootstrap in Scene", _hasBootstrapInScene);
                
                EditorGUILayout.Space(5);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Combat UI Manager", GUILayout.Height(30)))
                    {
                        CreateCombatUIManager();
                    }
                    
                    EditorGUI.BeginDisabledGroup(!_hasBootstrapInScene);
                    if (GUILayout.Button("Select in Scene", GUILayout.Height(30)))
                    {
                        SelectCombatUIManager();
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }
        }
        
        private void DrawAssetCreation()
        {
            EditorGUILayout.LabelField("Asset Creation", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusRow("Config ScriptableObjects", _hasConfigAssets);
                DrawStatusRow("UI Toolkit Prefabs", _hasPrefabs);
                DrawStatusRow("Combat UI Views", _hasViews);
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Create Folder Structure", GUILayout.Height(25)))
                {
                    CreateFolderStructure();
                }
                
                if (GUILayout.Button("Create Config Assets", GUILayout.Height(25)))
                {
                    CreateConfigAssets();
                }
                
                EditorGUI.BeginDisabledGroup(!_hasDamageNumbersPro);
                if (GUILayout.Button("Create Damage Feedback System (Prefabs + Profile)", GUILayout.Height(30)))
                {
                    CreateDamageFeedbackSystem();
                }
                EditorGUI.EndDisabledGroup();
                
                if (GUILayout.Button("Create UI Toolkit Prefabs", GUILayout.Height(25)))
                {
                    CreateUIToolkitPrefabs();
                }
            }
        }
        
        private void DrawCleanup()
        {
            EditorGUILayout.LabelField("Cleanup", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusRow("Old UGUI Files", !_hasOldUGUIFiles, 
                    _hasOldUGUIFiles ? "Found old files" : "Clean");
                
                EditorGUILayout.Space(5);
                
                GUI.backgroundColor = _hasOldUGUIFiles ? new Color(1f, 0.6f, 0.4f) : Color.white;
                if (GUILayout.Button("🧹 Clean Old UGUI Files", GUILayout.Height(25)))
                {
                    CleanOldUGUIFiles();
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.HelpBox(
                    "Removes old UGUI-based prefabs that have been replaced by UI Toolkit versions.\n" +
                    "This is safe to run - it only removes known legacy files.",
                    MessageType.None);
            }
        }
        
        private void DrawQuickSetup()
        {
            EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "Full Setup will:\n" +
                    "• Create all folder structures\n" +
                    "• Create config ScriptableObjects\n" +
                    "• Create shader materials\n" +
                    "• Create UI Toolkit prefabs\n" +
                    "• Add CombatUIManager to scene\n" +
                    "• Clean old UGUI files (if any)",
                    MessageType.None);
                
                EditorGUILayout.Space(5);
                
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("▶ FULL SETUP", GUILayout.Height(40)))
                {
                    RunFullSetup();
                }
                GUI.backgroundColor = Color.white;
            }
        }
        
        private void DrawConfigurationCheck()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusRow("World Space Panel Settings", _hasValidPanelSettings,
                    "Required for World Space UI Toolkit (Enemy Health Bars)");
                
                if (!_hasValidPanelSettings)
                {
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Fix Panel Settings", GUILayout.Height(25)))
                    {
                        FixHealthBarVisibility();
                        RefreshStatus();
                    }
                }
            }
        }
        
        private void DrawRefreshButton()
        {
            if (GUILayout.Button("↻ Refresh Status", GUILayout.Height(25)))
            {
                RefreshStatus();
            }
        }
        
        private void DrawStatusRow(string label, bool isComplete, string tooltip = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var icon = isComplete ? "✅" : "❌";
                var color = isComplete ? Color.green : Color.red;
                
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = color;
                
                EditorGUILayout.LabelField($"{icon} {label}", style);
                
                if (!string.IsNullOrEmpty(tooltip) && !isComplete)
                {
                    EditorGUILayout.LabelField($"({tooltip})", EditorStyles.miniLabel, GUILayout.Width(150));
                }
            }
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Setup Actions
        // ─────────────────────────────────────────────────────────────────
        
        private void CreateCombatUIManager()
        {
            // Check if already exists
            var existing = Object.FindFirstObjectByType<DIG.Combat.UI.CombatUIBootstrap>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists", 
                    "CombatUIBootstrap already exists in the scene.", "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            
            // Create manager GameObject
            var manager = new GameObject("CombatUIManager");
            Undo.RegisterCreatedObjectUndo(manager, "Create Combat UI Manager");
            
            // Add bootstrap
            var bootstrap = manager.AddComponent<DIG.Combat.UI.CombatUIBootstrap>();
            
            // Add adapter if Damage Numbers Pro is available
            if (_hasDamageNumbersPro)
            {
                var adapterType = System.Type.GetType("DIG.Combat.UI.Adapters.DamageNumbersProAdapter, Assembly-CSharp");
                if (adapterType != null)
                {
                    manager.AddComponent(adapterType);
                }
            }
            
            // Add Combat UI Views
            AddCombatUIViews(manager);
            
            Selection.activeGameObject = manager;
            EditorGUIUtility.PingObject(manager);
            
            Debug.Log("[CombatUISetupTool] Created CombatUIManager with CombatUIBootstrap");
            RefreshStatus();
        }
        
        private void AddCombatUIViews(GameObject parent)
        {
            // Create a child for screen-space views
            var screenViews = new GameObject("ScreenSpaceViews");
            screenViews.transform.SetParent(parent.transform);
            
            // Add Hitmarker View
            var hitmarker = new GameObject("EnhancedHitmarkerView");
            hitmarker.transform.SetParent(screenViews.transform);
            hitmarker.AddComponent<DIG.Combat.UI.Views.EnhancedHitmarkerView>();
            
            // Add Directional Damage View
            var directional = new GameObject("DirectionalDamageIndicatorView");
            directional.transform.SetParent(screenViews.transform);
            directional.AddComponent<DIG.Combat.UI.Views.DirectionalDamageIndicatorView>();
            
            // Create a child for UI Toolkit views
            var uiToolkitViews = new GameObject("UIToolkitViews");
            uiToolkitViews.transform.SetParent(parent.transform);
            
            // Add Combo Counter View
            var combo = new GameObject("ComboCounterView");
            combo.transform.SetParent(uiToolkitViews.transform);
            combo.AddComponent<DIG.Combat.UI.Views.ComboCounterView>();
            
            // Add Kill Feed View
            var killFeed = new GameObject("KillFeedView");
            killFeed.transform.SetParent(uiToolkitViews.transform);
            killFeed.AddComponent<DIG.Combat.UI.Views.KillFeedView>();
            
            // Add Status Effect Bar View
            var statusEffects = new GameObject("StatusEffectBarView");
            statusEffects.transform.SetParent(uiToolkitViews.transform);
            statusEffects.AddComponent<DIG.Combat.UI.Views.StatusEffectBarView>();
            
            // Add Combat Log View
            var combatLog = new GameObject("CombatLogView");
            combatLog.transform.SetParent(uiToolkitViews.transform);
            combatLog.AddComponent<DIG.Combat.UI.Views.CombatLogView>();
            
            // Add Boss Health Bar View
            var bossHealth = new GameObject("BossHealthBarView");
            bossHealth.transform.SetParent(uiToolkitViews.transform);
            bossHealth.AddComponent<DIG.Combat.UI.Views.BossHealthBarView>();
            
            Debug.Log("[CombatUISetupTool] Added 7 Combat UI Views to CombatUIManager");
        }
        
        private void SelectCombatUIManager()
        {
            var bootstrap = Object.FindFirstObjectByType<DIG.Combat.UI.CombatUIBootstrap>();
            if (bootstrap != null)
            {
                Selection.activeGameObject = bootstrap.gameObject;
                EditorGUIUtility.PingObject(bootstrap.gameObject);
            }
        }
        
        private void CreateFolderStructure()
        {
            CreateFolderIfNeeded("Assets/Prefabs");
            CreateFolderIfNeeded("Assets/Prefabs/UI");
            CreateFolderIfNeeded(DAMAGE_NUMBERS_PATH);
            CreateFolderIfNeeded(WORLD_SPACE_PATH);
            CreateFolderIfNeeded(FLOATING_TEXT_PATH);
            CreateFolderIfNeeded("Assets/Data");
            CreateFolderIfNeeded(CONFIG_ROOT);
            CreateFolderIfNeeded("Assets/Data/Feedback");
            CreateFolderIfNeeded("Assets/Data/Feedback/Combat");
            CreateFolderIfNeeded("Assets/UI");
            CreateFolderIfNeeded("Assets/UI/Combat");
            
            AssetDatabase.Refresh();
            Debug.Log("[CombatUISetupTool] Created folder structure");
            RefreshStatus();
        }
        
        private void CreateFolderIfNeeded(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path).Replace("\\", "/");
                var folderName = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
        
        private void CreateConfigAssets()
        {
            CreateFolderIfNeeded(CONFIG_ROOT);
            
            // Create HitmarkerConfig
            CreateConfigIfNeeded<DIG.Combat.UI.Config.HitmarkerConfig>(
                $"{CONFIG_ROOT}/HitmarkerConfig.asset");
            
            // Create DamageNumberConfig
            CreateConfigIfNeeded<DIG.Combat.UI.Config.DamageNumberConfig>(
                $"{CONFIG_ROOT}/DamageNumberConfig.asset");
            
            // Create EnemyUIConfig
            CreateConfigIfNeeded<DIG.Combat.UI.Config.EnemyUIConfig>(
                $"{CONFIG_ROOT}/EnemyUIConfig.asset");
            
            // Create CombatFeedbackConfig
            CreateConfigIfNeeded<DIG.Combat.UI.Config.CombatFeedbackConfig>(
                $"{CONFIG_ROOT}/CombatFeedbackConfig.asset");
            
            // Create FloatingTextStyleConfig
            CreateConfigIfNeeded<DIG.Combat.UI.FloatingText.FloatingTextStyleConfig>(
                $"{CONFIG_ROOT}/FloatingTextStyleConfig.asset");

            // EPIC 15.22: DamageFeedbackProfile is created via CreateDamageFeedbackSystem()
            // which also creates and wires up the prefabs.

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CombatUISetupTool] Created config ScriptableObjects");
            RefreshStatus();
        }
        
        private void CreateConfigIfNeeded<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return;
            
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[CombatUISetupTool] Created {Path.GetFileName(path)}");
        }
        
        /// <summary>
        /// EPIC 15.22: One-click setup for the entire damage feedback system.
        /// Creates all DamageNumberMesh prefabs, creates the DamageFeedbackProfile,
        /// and wires all prefabs into the profile automatically.
        /// </summary>
        [MenuItem("DIG/Setup/Create Damage Feedback System")]
        public static void CreateDamageFeedbackSystem()
        {
            // Check for Damage Numbers Pro
            bool hasDNP = Directory.Exists("Assets/DamageNumbersPro") ||
                          AssetDatabase.FindAssets("t:Script DamageNumber").Length > 0;
            if (!hasDNP)
            {
                EditorUtility.DisplayDialog("Missing Dependency",
                    "Damage Numbers Pro is not installed. Please install it from the Asset Store first.",
                    "OK");
                return;
            }

            // Ensure folders
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            if (!AssetDatabase.IsValidFolder(DAMAGE_NUMBERS_PATH))
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "DamageNumbers");
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(CONFIG_ROOT))
                AssetDatabase.CreateFolder("Assets/Data", "Config");

            // ── Step 1: Create or load all prefabs ──────────────────────────
            var normalPrefab   = GetOrCreateDamageNumberPrefab("DamageNumber_Normal");
            var criticalPrefab = GetOrCreateDamageNumberPrefab("DamageNumber_Critical");
            var grazePrefab    = GetOrCreateDamageNumberPrefab("DamageNumber_Graze");
            var missPrefab     = GetOrCreateDamageNumberPrefab("DamageNumber_Miss");
            var executePrefab  = GetOrCreateDamageNumberPrefab("DamageNumber_Execute");
            var blockPrefab    = GetOrCreateDamageNumberPrefab("DamageNumber_Block");
            var parriedPrefab  = GetOrCreateDamageNumberPrefab("DamageNumber_Parried");
            var immunePrefab   = GetOrCreateDamageNumberPrefab("DamageNumber_Immune");
            var healPrefab     = GetOrCreateDamageNumberPrefab("DamageNumber_Heal");
            var absorbPrefab   = GetOrCreateDamageNumberPrefab("DamageNumber_Absorb");
            var dotPrefab      = GetOrCreateDamageNumberPrefab("DamageNumber_DOT");

            // ── Step 2: Create or load the profile ──────────────────────────
            string profilePath = $"{CONFIG_ROOT}/DefaultDamageFeedbackProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<DIG.Combat.UI.Config.DamageFeedbackProfile>(profilePath);
            bool isNewProfile = profile == null;

            if (isNewProfile)
            {
                profile = ScriptableObject.CreateInstance<DIG.Combat.UI.Config.DamageFeedbackProfile>();
            }

            // ── Step 3: Wire prefabs into profile ───────────────────────────
            profile.NormalHit = WirePrefab(profile.NormalHit, normalPrefab);
            profile.CriticalHit = WirePrefab(profile.CriticalHit, criticalPrefab);
            profile.GrazeHit = WirePrefab(profile.GrazeHit, grazePrefab);
            profile.MissHit = WirePrefab(profile.MissHit, missPrefab);
            profile.ExecuteHit = WirePrefab(profile.ExecuteHit, executePrefab);
            profile.BlockedHit = WirePrefab(profile.BlockedHit, blockPrefab);
            profile.ParriedHit = WirePrefab(profile.ParriedHit, parriedPrefab);
            profile.ImmuneHit = WirePrefab(profile.ImmuneHit, immunePrefab);
            profile.HealPrefab = profile.HealPrefab != null ? profile.HealPrefab : healPrefab;
            profile.AbsorbPrefab = profile.AbsorbPrefab != null ? profile.AbsorbPrefab : absorbPrefab;
            profile.DOTPrefab = profile.DOTPrefab != null ? profile.DOTPrefab : dotPrefab;

            // Populate elemental damage types if empty
            if (profile.DamageTypes == null || profile.DamageTypes.Count == 0)
            {
                profile.DamageTypes = new List<DIG.Combat.UI.Config.DamageTypeProfile>
                {
                    new DIG.Combat.UI.Config.DamageTypeProfile { Type = DamageType.Physical,  DisplayName = "Physical",  Color = Color.white,                       SizeMultiplier = 1f },
                    new DIG.Combat.UI.Config.DamageTypeProfile { Type = DamageType.Fire,      DisplayName = "Fire",      Color = new Color(1f, 0.4f, 0.1f),         SizeMultiplier = 1.05f },
                    new DIG.Combat.UI.Config.DamageTypeProfile { Type = DamageType.Ice,       DisplayName = "Ice",       Color = new Color(0.3f, 0.7f, 1f),         SizeMultiplier = 1f },
                    new DIG.Combat.UI.Config.DamageTypeProfile { Type = DamageType.Lightning, DisplayName = "Lightning", Color = new Color(1f, 1f, 0.3f),           SizeMultiplier = 1.1f },
                    new DIG.Combat.UI.Config.DamageTypeProfile { Type = DamageType.Poison,    DisplayName = "Poison",    Color = new Color(0.4f, 0.9f, 0.2f),       SizeMultiplier = 0.95f },
                    new DIG.Combat.UI.Config.DamageTypeProfile { Type = DamageType.Holy,      DisplayName = "Holy",      Color = new Color(1f, 1f, 0.7f),           SizeMultiplier = 1.05f },
                    new DIG.Combat.UI.Config.DamageTypeProfile { Type = DamageType.Shadow,    DisplayName = "Shadow",    Color = new Color(0.5f, 0.2f, 0.7f),       SizeMultiplier = 1f },
                    new DIG.Combat.UI.Config.DamageTypeProfile { Type = DamageType.Arcane,    DisplayName = "Arcane",    Color = new Color(0.7f, 0.3f, 1f),         SizeMultiplier = 1.05f }
                };
            }

            // ── Step 4: Save ────────────────────────────────────────────────
            if (isNewProfile)
            {
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            else
            {
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);

            EditorUtility.DisplayDialog("Damage Feedback System Created",
                "Created and wired up:\n\n" +
                "• 11 DamageNumberMesh prefabs\n" +
                "• DefaultDamageFeedbackProfile (all prefabs assigned)\n\n" +
                "Next steps:\n" +
                "1. Configure each prefab's visual style in the Inspector\n" +
                "   (pooling, colors, fonts, animations)\n" +
                "2. Assign the profile to the DamageNumbersProAdapter\n" +
                "   on your CombatUIManager",
                "OK");

            Debug.Log("[CombatUISetupTool] Damage Feedback System created: 11 prefabs + profile with all wired up.");
        }

        /// <summary>
        /// Get or create a DamageNumberMesh prefab at the standard path.
        /// Ensures the prefab has proper DNP structure (TMP child, MeshA, MeshB)
        /// and a TMP font assigned so damage numbers actually render.
        /// </summary>
        private static DamageNumbersPro.DamageNumber GetOrCreateDamageNumberPrefab(string prefabName)
        {
            string path = $"{DAMAGE_NUMBERS_PATH}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null)
            {
                var dnp = existing.GetComponent<DamageNumbersPro.DamageNumber>();
                if (dnp != null)
                {
                    // Check if the prefab has proper DNP structure (TMP child for text rendering)
                    if (existing.transform.Find("TMP") != null)
                        return dnp; // Already set up properly

                    // Broken prefab (no TMP child) — fix via LoadPrefabContents
                    FixDamageNumberPrefab(path);
                    existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    return existing.GetComponent<DamageNumbersPro.DamageNumber>();
                }

                // Prefab exists but is missing DamageNumberMesh — fix it
                FixDamageNumberPrefab(path);
                existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return existing.GetComponent<DamageNumbersPro.DamageNumber>();
            }

            // Create new prefab with proper DNP structure and font
            var go = new GameObject(prefabName);
            go.AddComponent<DamageNumbersPro.DamageNumberMesh>();

            // Build DNP child structure: TMP (TextMeshPro), MeshA, MeshB, SortingGroup
            DamageNumbersPro.Internal.DNPEditorInternal.PrepareMeshStructure(go);

            // Assign a TMP font so numbers actually render
            var mesh = go.GetComponent<DamageNumbersPro.DamageNumber>();
            AssignDefaultFont(mesh);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[CombatUISetupTool] Created {prefabName}.prefab (with DNP structure + font)");

            return prefab.GetComponent<DamageNumbersPro.DamageNumber>();
        }

        /// <summary>
        /// Fix an existing DamageNumber prefab that's missing structure or font.
        /// Uses LoadPrefabContents for safe prefab asset editing.
        /// </summary>
        private static void FixDamageNumberPrefab(string path)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);

            // Ensure DamageNumberMesh component exists
            var dn = contents.GetComponent<DamageNumbersPro.DamageNumber>();
            if (dn == null)
            {
                contents.AddComponent<DamageNumbersPro.DamageNumberMesh>();
                dn = contents.GetComponent<DamageNumbersPro.DamageNumber>();
            }

            // Build structure if missing
            if (contents.transform.Find("TMP") == null)
                DamageNumbersPro.Internal.DNPEditorInternal.PrepareMeshStructure(contents);

            // Assign font if missing
            if (dn != null && dn.GetFontMaterial() == null)
                AssignDefaultFont(dn);

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log($"[CombatUISetupTool] Fixed {Path.GetFileName(path)} — added DNP structure + font");
        }

        /// <summary>
        /// Assign a default TMP font to a DamageNumber component.
        /// Tries DNP's built-in preset font first, falls back to LiberationSans SDF.
        /// </summary>
        private static void AssignDefaultFont(DamageNumbersPro.DamageNumber dn)
        {
            if (dn == null) return;

            TMP_FontAsset font = null;

            // Try DNP's default preset font
            var preset = Resources.Load<DamageNumbersPro.Internal.DNPPreset>("DNP/Style/Basic/Basic Default");
            if (preset != null && preset.fontAsset != null)
                font = preset.fontAsset;

            // Fallback to TMP's default font
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (font != null)
            {
                dn.SetFontMaterial(font);
                Debug.Log($"[CombatUISetupTool] Assigned font '{font.name}' to {dn.name}");
            }
            else
            {
                Debug.LogWarning($"[CombatUISetupTool] No TMP_FontAsset found for {dn.name}! Damage numbers will be invisible.");
            }
        }

        /// <summary>
        /// Wire a prefab into a DamageNumberProfile, preserving existing assignment.
        /// </summary>
        private static DIG.Combat.UI.Config.DamageNumberProfile WirePrefab(
            DIG.Combat.UI.Config.DamageNumberProfile hitProfile,
            DamageNumbersPro.DamageNumber prefab)
        {
            if (hitProfile.Prefab == null)
                hitProfile.Prefab = prefab;
            return hitProfile;
        }
        
        private void CreateWorldSpacePrefabs()
        {
            CreateFolderIfNeeded(WORLD_SPACE_PATH);
            CreateFolderIfNeeded(FLOATING_TEXT_PATH);
            
            // Legacy method - redirects to UI Toolkit version
            CreateUIToolkitPrefabs();
        }
        
        private void CreateUIToolkitPrefabs()
        {
            CreateFolderIfNeeded(WORLD_SPACE_PATH);
            CreateFolderIfNeeded(FLOATING_TEXT_PATH);
            CreateFolderIfNeeded(MATERIAL_PATH);
            
            // Create EnemyHealthBar prefab (UI Toolkit)
            CreateUIToolkitHealthBar();
            
            // Create FloatingTextElement prefab (UI Toolkit)
            CreateUIToolkitFloatingText();
            
            // Create InteractionRing prefab (UI Toolkit)
            CreateUIToolkitInteractionRing();
            
            AssetDatabase.Refresh();
            Debug.Log("[CombatUISetupTool] Created UI Toolkit prefabs");
            RefreshStatus();
        }
        
        private void CreateUIToolkitHealthBar()
        {
            string path = $"{WORLD_SPACE_PATH}/EnemyHealthBar.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;
            
            var go = new GameObject("EnemyHealthBar");
            
            // Mesh Renderer Setup (Replaces UI Toolkit)
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            
            // Assign Quad
            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mf.sharedMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tempQuad);
            
            // Assign Material
            string matPath = $"{MATERIAL_PATH}/CombatUI_HealthBar.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                var shader = Shader.Find("DIG/UI/CombatUI_HealthBar");
                if (shader != null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
            }
            if (mat != null) mr.sharedMaterial = mat;
            
            // Add Component
            go.AddComponent<DIG.Combat.UI.WorldSpace.EnemyHealthBar>();
            
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[CombatUISetupTool] Created EnemyHealthBar.prefab (Mesh Renderer)");
        }
        
        private void CreateUIToolkitFloatingText()
        {
            string path = $"{FLOATING_TEXT_PATH}/FloatingTextElement.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;
            
            var go = new GameObject("FloatingTextElement");
            
            // Add UIDocument for UI Toolkit world-space
            var uiDoc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            
            // Add FloatingTextElement component
            go.AddComponent<DIG.Combat.UI.FloatingText.FloatingTextElement>();
            
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[CombatUISetupTool] Created FloatingTextElement.prefab (UI Toolkit)");
        }
        
        private void CreateUIToolkitInteractionRing()
        {
            string path = $"{WORLD_SPACE_PATH}/InteractionRing.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;
            
            var go = new GameObject("InteractionRing");
            
            // Add UIDocument for UI Toolkit world-space
            var uiDoc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            
            // Add InteractionProgressRing component
            go.AddComponent<DIG.Combat.UI.WorldSpace.InteractionProgressRing>();
            
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[CombatUISetupTool] Created InteractionRing.prefab (UI Toolkit)");
        }
        
        private void CreateShaderMaterials()
        {
            CreateFolderIfNeeded("Assets/Materials");
            CreateFolderIfNeeded(MATERIAL_PATH);
            
            // Create materials from shaders
            CreateMaterialFromShader("CombatUI_HealthBar");
            CreateMaterialFromShader("CombatUI_RadialFill");
            CreateMaterialFromShader("CombatUI_Glow");
            CreateMaterialFromShader("CombatUI_Hitmarker");
            
            AssetDatabase.Refresh();
            Debug.Log("[CombatUISetupTool] Created shader materials");
            RefreshStatus();
        }
        
        private void CreateMaterialFromShader(string shaderName)
        {
            string matPath = $"{MATERIAL_PATH}/{shaderName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                return;
            
            var shader = Shader.Find($"DIG/UI/{shaderName}");
            if (shader == null)
            {
                Debug.LogWarning($"[CombatUISetupTool] Shader not found: DIG/UI/{shaderName}");
                return;
            }
            
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log($"[CombatUISetupTool] Created {shaderName}.mat");
        }
        
        private void CleanOldUGUIFiles()
        {
            var filesToDelete = new List<string>();
            
            // Known old UGUI prefabs
            string[] oldPrefabs = {
                $"{WORLD_SPACE_PATH}/EnemyHealthBar_UGUI.prefab",
                $"{FLOATING_TEXT_PATH}/FloatingTextElement_UGUI.prefab",
                $"{WORLD_SPACE_PATH}/InteractionRing_UGUI.prefab",
                $"{WORLD_SPACE_PATH}/EnemyHealthBar_Legacy.prefab",
                $"{FLOATING_TEXT_PATH}/FloatingTextElement_Legacy.prefab"
            };
            
            foreach (var path in oldPrefabs)
            {
                if (File.Exists(path))
                {
                    filesToDelete.Add(path);
                }
            }
            
            if (filesToDelete.Count == 0)
            {
                EditorUtility.DisplayDialog("Clean Complete", 
                    "No old UGUI files found. Everything is clean!", "OK");
                return;
            }
            
            bool confirm = EditorUtility.DisplayDialog("Confirm Cleanup",
                $"Delete {filesToDelete.Count} old UGUI file(s)?\n\n" +
                string.Join("\n", filesToDelete.ConvertAll(f => Path.GetFileName(f))),
                "Delete", "Cancel");
            
            if (!confirm) return;
            
            foreach (var path in filesToDelete)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[CombatUISetupTool] Deleted: {path}");
            }
            
            AssetDatabase.Refresh();
            RefreshStatus();
            
            EditorUtility.DisplayDialog("Cleanup Complete",
                $"Deleted {filesToDelete.Count} old UGUI file(s).", "OK");
        }
        
        private void RunFullSetup()
        {
            EditorUtility.DisplayProgressBar("Combat UI Setup", "Creating folders...", 0.05f);
            CreateFolderStructure();
            
            EditorUtility.DisplayProgressBar("Combat UI Setup", "Creating configs...", 0.15f);
            CreateConfigAssets();
            
            EditorUtility.DisplayProgressBar("Combat UI Setup", "Creating shader materials...", 0.30f);
            CreateShaderMaterials();
            
            EditorUtility.DisplayProgressBar("Combat UI Setup", "Creating UI Toolkit prefabs...", 0.45f);
            CreateUIToolkitPrefabs();
            
            if (_hasDamageNumbersPro)
            {
                EditorUtility.DisplayProgressBar("Combat UI Setup", "Creating damage feedback system...", 0.60f);
                CreateDamageFeedbackSystem();
            }
            
            EditorUtility.DisplayProgressBar("Combat UI Setup", "Cleaning old files...", 0.75f);
            // Silent cleanup - don't show dialog
            CleanOldUGUIFilesSilent();
            
            EditorUtility.DisplayProgressBar("Combat UI Setup", "Setting up scene...", 0.90f);
            CreateCombatUIManager();
            
            EditorUtility.ClearProgressBar();
            
            EditorUtility.DisplayDialog("Setup Complete",
                "Combat UI setup complete! (UI Toolkit + URP Shaders)\n\n" +
                "Next steps:\n" +
                "1. Assign shader materials to prefab components\n" +
                "2. Configure damage number prefabs (if using Damage Numbers Pro)\n" +
                "3. Create UXML/USS for custom UI Toolkit styling\n" +
                "4. Create FEEL feedback presets\n\n" +
                "See SETUP_GUIDE_15.9.md for details.",
                "OK");
            
            RefreshStatus();
        }
        
        private void CleanOldUGUIFilesSilent()
        {
            string[] oldPrefabs = {
                $"{WORLD_SPACE_PATH}/EnemyHealthBar_UGUI.prefab",
                $"{FLOATING_TEXT_PATH}/FloatingTextElement_UGUI.prefab",
                $"{WORLD_SPACE_PATH}/InteractionRing_UGUI.prefab"
            };
            
            foreach (var path in oldPrefabs)
            {
                if (File.Exists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[CombatUISetupTool] Cleaned old file: {path}");
                }
            }
        }
        private PanelSettings GetOrCreateWorldSpacePanelSettings()
        {
            string path = "Assets/Data/UI/WorldSpacePanelSettings.asset";
            CreateFolderIfNeeded("Assets/Data/UI");
            
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                
                // CORRECT SETTINGS FOR WORLD SPACE:
                // Use ConstantPixelSize so that 1 unit in UI = Scale factor in World.
                // Scale 0.01 means 100px width = 1 Unity Unit wide.
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.scale = 0.01f;
                
                settings.referenceDpi = 96;
                settings.fallbackDpi = 96;
                
                AssetDatabase.CreateAsset(settings, path);
                Debug.Log($"[CombatUISetupTool] Created WorldSpacePanelSettings.asset at {path}");
            }
            
            // Validate configuration - Update existing if wrong
            if (settings.scaleMode != PanelScaleMode.ConstantPixelSize || Mathf.Abs(settings.scale - 0.01f) > 0.001f)
            {
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.scale = 0.01f;
                EditorUtility.SetDirty(settings);
                Debug.LogWarning("[CombatUISetupTool] Fixed existing PanelSettings Asset ScaleMode to ConstantPixelSize (Scale 0.01)");
            }
            
            return settings;
        }

        [MenuItem("DIG/Setup/Fix Health Bar (Mesh Mode)", false, 50)]
        public static void FixHealthBarVisibility()
        {
            Debug.Log("[CombatUISetupTool] Converting Health Bars to Mesh Renderer...");
            
            // 1. Find Shader
            var shader = Shader.Find("DIG/UI/CombatUI_HealthBar");
            if (shader == null)
            {
                Debug.LogError("[CombatUISetupTool] Shader 'DIG/UI/CombatUI_HealthBar' not found!");
                return;
            }
            
            int fixedCount = 0;
            
            // 2. Fix commonly known path
            string defaultPath = "Assets/Prefabs/UI/WorldSpace/EnemyHealthBar.prefab";
            var defaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath);
            if (ConvertPrefabToMesh(defaultPrefab, shader)) fixedCount++;

            // 3. Fix prefab assigned to Pool in Scene
            var pool = Object.FindFirstObjectByType<DIG.Combat.UI.WorldSpace.EnemyHealthBarPool>();
            if (pool != null)
            {
                var so = new SerializedObject(pool);
                var prefabProp = so.FindProperty("healthBarPrefab");
                if (prefabProp != null && prefabProp.objectReferenceValue != null)
                {
                    var assignedPrefab = prefabProp.objectReferenceValue as GameObject;
                     if (assignedPrefab == null && prefabProp.objectReferenceValue is Component c) assignedPrefab = c.gameObject;
                    
                    if (assignedPrefab != null)
                    {
                        if (ConvertPrefabToMesh(assignedPrefab, shader)) fixedCount++;
                    }
                }
            }
            
            if (fixedCount > 0)
            {
                EditorUtility.DisplayDialog("Fix Complete", 
                    $"Converted {fixedCount} prefab(s) to Mesh Renderer.\n\nPlease RESTART the scene.", "OK");
            }
            else
            {
                 EditorUtility.DisplayDialog("Fix Attempted", 
                    "Could not find any EnemyHealthBar prefabs to fix.", "OK");
            }
            
            AssetDatabase.SaveAssets();
        }
        
        [MenuItem("DIG/Setup/Force Recreate Health Bar (Mesh Mode)", false, 51)]
        public static void ForceRecreateHealthBar()
        {
            if (!EditorUtility.DisplayDialog("Recreate Health Bar?", 
                "This will DELETE 'Assets/Prefabs/UI/WorldSpace/EnemyHealthBar.prefab' and recreate it as a Mesh Renderer prefab.\n\nUse this if the health bar is invisible or has layout issues.", 
                "Recreate", "Cancel"))
            {
                return;
            }

            // 1. Ensure Directory
            string folderPath = "Assets/Prefabs/UI/WorldSpace";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
            
            string prefabPath = $"{folderPath}/EnemyHealthBar.prefab";
            
            // 2. Delete existing
            AssetDatabase.DeleteAsset(prefabPath);
            
            // 3. Create new clean object
            var go = new GameObject("EnemyHealthBar");
            go.layer = 0; // Default layer
            go.transform.localScale = Vector3.one; 
            
            // 4. Add components
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<DIG.Combat.UI.WorldSpace.EnemyHealthBar>();
            
            // 5. Convert to Mesh setup (assign Quad/Material)
            var shader = Shader.Find("DIG/UI/CombatUI_HealthBar");
            ConvertPrefabToMesh(go, shader);
            
            // 6. Save as Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            GameObject.DestroyImmediate(go);
            
            // 7. Assign to Pool
            var pool = Object.FindFirstObjectByType<DIG.Combat.UI.WorldSpace.EnemyHealthBarPool>();
            if (pool != null)
            {
                var so = new SerializedObject(pool);
                var prefabProp = so.FindProperty("healthBarPrefab");
                if (prefabProp != null)
                {
                    prefabProp.objectReferenceValue = prefab;
                    // Apply, this supports undo if we used the proper undo system, but SO apply is standard for tools
                    so.ApplyModifiedProperties();
                }
            }
            
            AssetDatabase.SaveAssets();
            
            EditorUtility.DisplayDialog("Recreation Complete", 
                "EnemyHealthBar prefab has been recreated (Mesh Mode) and assigned.\n\nPlease RESTART the scene.", "OK");
        }
        
        private static bool ConvertPrefabToMesh(GameObject prefab, Shader shader)
        {
            if (prefab == null) return false;
            
            bool modified = false;

            // Remove UI Toolkit
            var doc = prefab.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (doc != null)
            {
                Object.DestroyImmediate(doc, true); // true = allowDestroyingAssets
                modified = true;
            }

            // Add Mesh Components
            var mf = prefab.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = prefab.AddComponent<MeshFilter>();
                modified = true;
            }

            // Assign Quad
            if (mf.sharedMesh == null || mf.sharedMesh.name != "Quad")
            {
                GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mf.sharedMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tempQuad);
                modified = true;
            }

            var mr = prefab.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = prefab.AddComponent<MeshRenderer>();
                modified = true;
            }

            // Assign Material
            if (mr.sharedMaterial == null || (shader != null && mr.sharedMaterial.shader != shader))
            {
                string matPath = $"{MATERIAL_PATH}/CombatUI_HealthBar.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                
                // If asset doesn't exist, create it via shader
                if (mat == null && shader != null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                
                if (mat != null)
                {
                    mr.sharedMaterial = mat;
                    modified = true;
                }
            }
            
            if (modified)
            {
                EditorUtility.SetDirty(prefab);
                if (AssetDatabase.Contains(prefab))
                    PrefabUtility.SavePrefabAsset(prefab);
            }
            
            return modified;
        }
    }
}
