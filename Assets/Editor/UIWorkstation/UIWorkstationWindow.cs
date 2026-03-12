using System.Collections.Generic;
using System.IO;
using System.Linq;
using DIG.UI.Core.Navigation;
using DIG.UI.Core.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Editor
{
    /// <summary>
    /// EPIC 18.1: Editor window for setting up, configuring, and debugging the UI service layer.
    /// Tabs: Setup, Catalog, Theme, Screen Stack, Focus Debug.
    /// </summary>
    public class UIWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private static readonly string[] TabNames = { "Setup", "Catalog", "Theme", "Stack", "Focus" };
        private Vector2 _scrollPos;
        private double _nextRepaintTime;
        private const double RepaintInterval = 0.25;

        // Cached references
        private ScreenManifestSO _cachedManifest;
        private UIThemeSO _previewTheme;
        private SerializedObject _manifestSO;
        private SerializedObject _themeSO;
        private UIThemeSO _themeSoTarget;

        // Setup tab state
        private bool _showBootstrapSection = true;
        private bool _showManifestSection = true;
        private bool _showTransitionSection = true;
        private bool _showThemeSection = true;

        // Catalog tab state — new screen entry fields
        private string _newScreenId = "";
        private UILayer _newScreenLayer = UILayer.Screen;
        private string _newScreenUXMLPath = "";
        private bool _newScreenPoolable = true;
        private bool _newScreenBlocksInput;

        // Transition creation
        private string _newTransitionName = "Fade_200ms";
        private TransitionType _newTransitionType = TransitionType.Fade;
        private float _newTransitionDuration = 0.2f;

        // Theme creation
        private string _newThemeName = "Default";

        // Cached UXML validation to avoid Resources.Load in OnGUI
        private readonly Dictionary<string, bool> _uxmlExistsCache = new();

        [MenuItem("DIG/UI Workstation")]
        public static void ShowWindow()
        {
            GetWindow<UIWorkstationWindow>("UI Workstation");
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying && EditorApplication.timeSinceStartup >= _nextRepaintTime)
            {
                _nextRepaintTime = EditorApplication.timeSinceStartup + RepaintInterval;
                Repaint();
            }
        }

        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames);
            EditorGUILayout.Space(4);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawSetup(); break;
                case 1: DrawCatalog(); break;
                case 2: DrawThemeTab(); break;
                case 3: DrawScreenStack(); break;
                case 4: DrawFocusDebug(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // =====================================================================
        // TAB 0: SETUP — One-click creation of all required assets + scene setup
        // =====================================================================

        private void DrawSetup()
        {
            EditorGUILayout.LabelField("UI Service Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tab helps you create all required assets and scene objects for the UI Service Layer. " +
                "Green checkmarks indicate assets/objects that already exist.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // --- Bootstrap ---
            _showBootstrapSection = EditorGUILayout.Foldout(_showBootstrapSection, "1. Scene Bootstrap", true, EditorStyles.foldoutHeader);
            if (_showBootstrapSection)
            {
                EditorGUI.indentLevel++;
                var existingBootstrap = FindAnyObjectByType<UIServiceBootstrap>();
                if (existingBootstrap != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("UIServiceBootstrap", EditorStyles.boldLabel);
                    GUILayout.Label("Found", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button("Select in Scene", GUILayout.Width(150)))
                        Selection.activeGameObject = existingBootstrap.gameObject;
                }
                else
                {
                    EditorGUILayout.HelpBox("No UIServiceBootstrap in the current scene.", MessageType.Warning);
                    if (GUILayout.Button("Create UIServiceBootstrap in Scene", GUILayout.Width(280)))
                    {
                        var go = new GameObject("UIServiceBootstrap");
                        go.AddComponent<UIServiceBootstrap>();
                        Undo.RegisterCreatedObjectUndo(go, "Create UIServiceBootstrap");
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // --- Manifest ---
            _showManifestSection = EditorGUILayout.Foldout(_showManifestSection, "2. Screen Manifest", true, EditorStyles.foldoutHeader);
            if (_showManifestSection)
            {
                EditorGUI.indentLevel++;
                var manifest = FindManifest();
                if (manifest != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("ScreenManifest", EditorStyles.boldLabel);
                    GUILayout.Label($"{manifest.AllScreens.Count} screens", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button("Select Asset", GUILayout.Width(150)))
                    {
                        Selection.activeObject = manifest;
                        EditorGUIUtility.PingObject(manifest);
                    }

                    // Validate Resources path
                    string assetPath = AssetDatabase.GetAssetPath(manifest);
                    if (!assetPath.Contains("/Resources/"))
                    {
                        EditorGUILayout.HelpBox(
                            $"Manifest is at '{assetPath}' but must be inside a Resources/ folder " +
                            "for runtime loading. Move it to Assets/Resources/ScreenManifest.asset.",
                            MessageType.Error);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No ScreenManifestSO found in the project.", MessageType.Warning);
                    if (GUILayout.Button("Create Screen Manifest", GUILayout.Width(200)))
                    {
                        CreateManifest();
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // --- Transition Profiles ---
            _showTransitionSection = EditorGUILayout.Foldout(_showTransitionSection, "3. Transition Profiles", true, EditorStyles.foldoutHeader);
            if (_showTransitionSection)
            {
                EditorGUI.indentLevel++;

                var transitionGuids = AssetDatabase.FindAssets("t:TransitionProfileSO");
                EditorGUILayout.LabelField($"Found: {transitionGuids.Length} transition profile(s)");

                foreach (var guid in transitionGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var profile = AssetDatabase.LoadAssetAtPath<TransitionProfileSO>(path);
                    if (profile == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  {profile.name}", GUILayout.Width(200));
                    EditorGUILayout.LabelField($"{profile.Type} | {profile.Duration:F2}s", EditorStyles.miniLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = profile;
                        EditorGUIUtility.PingObject(profile);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Create New Transition", EditorStyles.boldLabel);

                _newTransitionName = EditorGUILayout.TextField("Name", _newTransitionName);
                _newTransitionType = (TransitionType)EditorGUILayout.EnumPopup("Type", _newTransitionType);
                _newTransitionDuration = EditorGUILayout.Slider("Duration (s)", _newTransitionDuration, 0f, 2f);

                if (GUILayout.Button("Create Transition Profile", GUILayout.Width(200)))
                {
                    CreateTransitionProfile(_newTransitionName, _newTransitionType, _newTransitionDuration);
                }

                if (transitionGuids.Length == 0)
                {
                    EditorGUILayout.Space(4);
                    if (GUILayout.Button("Create Recommended Presets (Fade, SlideUp, Scale)", GUILayout.Width(350)))
                    {
                        CreateTransitionProfile("Fade_Fast", TransitionType.Fade, 0.15f);
                        CreateTransitionProfile("Fade_Normal", TransitionType.Fade, 0.25f);
                        CreateTransitionProfile("SlideUp_Modal", TransitionType.SlideUp, 0.3f);
                        CreateTransitionProfile("Scale_Popup", TransitionType.Scale, 0.2f);
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // --- Theme ---
            _showThemeSection = EditorGUILayout.Foldout(_showThemeSection, "4. Theme", true, EditorStyles.foldoutHeader);
            if (_showThemeSection)
            {
                EditorGUI.indentLevel++;

                var themeGuids = AssetDatabase.FindAssets("t:UIThemeSO");
                EditorGUILayout.LabelField($"Found: {themeGuids.Length} theme(s)");

                foreach (var guid in themeGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(path);
                    if (theme == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  {theme.ThemeName}", GUILayout.Width(200));
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = theme;
                        EditorGUIUtility.PingObject(theme);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);
                _newThemeName = EditorGUILayout.TextField("New Theme Name", _newThemeName);
                if (GUILayout.Button("Create Theme", GUILayout.Width(150)))
                {
                    CreateTheme(_newThemeName);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(16);

            // --- Quick Setup All ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Creates everything needed in one click: manifest, default transitions, default theme, and scene bootstrap.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("Run Full Setup", GUILayout.Height(30)))
            {
                RunFullSetup();
            }
            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        // TAB 1: CATALOG — View and add screen entries to the manifest
        // =====================================================================

        private void DrawCatalog()
        {
            EditorGUILayout.LabelField("Screen Manifest Catalog", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var manifest = FindManifest();
            if (manifest == null)
            {
                EditorGUILayout.HelpBox("No ScreenManifestSO found. Use the Setup tab to create one.", MessageType.Warning);
                return;
            }

            // Editable manifest reference
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Registered Screens: {manifest.AllScreens.Count}");
            if (GUILayout.Button("Open Inspector", GUILayout.Width(120)))
            {
                Selection.activeObject = manifest;
                EditorGUIUtility.PingObject(manifest);
            }
            EditorGUILayout.EndHorizontal();

            // Default transitions (editable)
            EditorGUILayout.Space(4);
            var so = GetManifestSO(manifest);
            if (so != null)
            {
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("DefaultOpenTransition"));
                EditorGUILayout.PropertyField(so.FindProperty("DefaultCloseTransition"));
                EditorGUILayout.PropertyField(so.FindProperty("DefaultTheme"));
                if (so.hasModifiedProperties)
                    so.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(8);

            // Existing screens
            foreach (var screen in manifest.AllScreens)
            {
                if (screen == null) continue;
                DrawScreenEntry(screen, manifest);
            }

            // Add new screen entry
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Add New Screen", EditorStyles.boldLabel);

            _newScreenId = EditorGUILayout.TextField("Screen Id", _newScreenId);
            _newScreenLayer = (UILayer)EditorGUILayout.EnumPopup("Layer", _newScreenLayer);
            _newScreenUXMLPath = EditorGUILayout.TextField("UXML Path (Resources/)", _newScreenUXMLPath);
            _newScreenPoolable = EditorGUILayout.Toggle("Poolable", _newScreenPoolable);
            _newScreenBlocksInput = EditorGUILayout.Toggle("Blocks Input", _newScreenBlocksInput);

            bool canAdd = !string.IsNullOrWhiteSpace(_newScreenId) && !string.IsNullOrWhiteSpace(_newScreenUXMLPath);
            EditorGUI.BeginDisabledGroup(!canAdd);
            if (GUILayout.Button("Add to Manifest"))
            {
                AddScreenToManifest(manifest);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private void DrawScreenEntry(ScreenDefinition screen, ScreenManifestSO manifest)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(screen.ScreenId ?? "(unnamed)", EditorStyles.boldLabel);

            // Validate UXML exists (cached to avoid Resources.Load in OnGUI)
            if (!string.IsNullOrEmpty(screen.UXMLPath))
            {
                if (!_uxmlExistsCache.TryGetValue(screen.UXMLPath, out bool exists))
                {
                    exists = Resources.Load<VisualTreeAsset>(screen.UXMLPath) != null;
                    _uxmlExistsCache[screen.UXMLPath] = exists;
                }
                if (!exists)
                {
                    GUILayout.Label("UXML MISSING", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Layer", screen.Layer.ToString());
            EditorGUILayout.LabelField("UXML", screen.UXMLPath ?? "(none)");
            if (!string.IsNullOrEmpty(screen.USSPath))
                EditorGUILayout.LabelField("USS", screen.USSPath);
            EditorGUILayout.LabelField("Poolable", screen.Poolable.ToString());
            if (screen.BlocksInput)
                EditorGUILayout.LabelField("Blocks Input", "true");
            if (!string.IsNullOrEmpty(screen.InitialFocusElement))
                EditorGUILayout.LabelField("Focus", screen.InitialFocusElement);

            // Play mode controls
            if (Application.isPlaying && UIServices.IsInitialized)
            {
                bool isOpen = UIServices.Screen.IsOpen(screen.ScreenId);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Status", isOpen ? "OPEN" : "Closed");
                if (!isOpen && GUILayout.Button("Test Open", GUILayout.Width(80)))
                    UIServices.Screen.OpenScreen(screen.ScreenId);
                if (isOpen && GUILayout.Button("Close", GUILayout.Width(80)))
                {
                    var handle = UIServices.Screen.GetHandle(screen.ScreenId);
                    if (handle.IsValid)
                        UIServices.Screen.CloseScreen(handle);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // =====================================================================
        // TAB 2: THEME — Create, preview, and apply themes
        // =====================================================================

        private void DrawThemeTab()
        {
            EditorGUILayout.LabelField("Theme Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _previewTheme = (UIThemeSO)EditorGUILayout.ObjectField("Theme Asset", _previewTheme, typeof(UIThemeSO), false);

            if (_previewTheme == null)
            {
                EditorGUILayout.HelpBox("Drag a UIThemeSO asset here to preview and edit its colors. Or create one from the Setup tab.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            // Editable theme via cached SerializedObject
            if (_themeSO == null || _themeSoTarget != _previewTheme)
            {
                _themeSO = new SerializedObject(_previewTheme);
                _themeSoTarget = _previewTheme;
            }
            var themeSO = _themeSO;
            themeSO.Update();

            EditorGUILayout.PropertyField(themeSO.FindProperty("ThemeName"));
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Primary Colors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(themeSO.FindProperty("Primary"));
            EditorGUILayout.PropertyField(themeSO.FindProperty("PrimaryLight"));
            EditorGUILayout.PropertyField(themeSO.FindProperty("PrimaryDark"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Background Colors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(themeSO.FindProperty("Background"));
            EditorGUILayout.PropertyField(themeSO.FindProperty("Surface"));
            EditorGUILayout.PropertyField(themeSO.FindProperty("BackgroundPanel"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Text Colors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(themeSO.FindProperty("TextPrimary"));
            EditorGUILayout.PropertyField(themeSO.FindProperty("TextSecondary"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Semantic Colors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(themeSO.FindProperty("Success"));
            EditorGUILayout.PropertyField(themeSO.FindProperty("Warning"));
            EditorGUILayout.PropertyField(themeSO.FindProperty("Error"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Font Overrides", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(themeSO.FindProperty("PrimaryFont"));
            EditorGUILayout.PropertyField(themeSO.FindProperty("HeadingFont"));

            if (themeSO.hasModifiedProperties)
                themeSO.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            // Color swatches preview
            EditorGUILayout.LabelField("Color Preview", EditorStyles.boldLabel);
            DrawColorSwatch("Primary", _previewTheme.Primary);
            DrawColorSwatch("Background", _previewTheme.Background);
            DrawColorSwatch("Text", _previewTheme.TextPrimary);
            DrawColorSwatch("Success", _previewTheme.Success);
            DrawColorSwatch("Warning", _previewTheme.Warning);
            DrawColorSwatch("Error", _previewTheme.Error);

            EditorGUILayout.Space(8);

            if (Application.isPlaying && UIServices.IsInitialized)
            {
                if (GUILayout.Button("Apply Theme (Live)", GUILayout.Height(25)))
                    UIServices.Screen.SetTheme(_previewTheme);
            }

            // Assign to manifest
            var manifest = FindManifest();
            if (manifest != null && manifest.DefaultTheme != _previewTheme)
            {
                if (GUILayout.Button("Set as Default Theme on Manifest"))
                {
                    var mso = GetManifestSO(manifest);
                    mso.Update();
                    mso.FindProperty("DefaultTheme").objectReferenceValue = _previewTheme;
                    mso.ApplyModifiedProperties();
                }
            }
        }

        private void DrawColorSwatch(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            EditorGUILayout.ColorField(GUIContent.none, color, false, true, false, GUILayout.Width(60), GUILayout.Height(16));
            EditorGUILayout.LabelField($"#{ColorUtility.ToHtmlStringRGB(color)}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // =====================================================================
        // TAB 3: SCREEN STACK — Live play-mode debug view
        // =====================================================================

        private void DrawScreenStack()
        {
            EditorGUILayout.LabelField("Live Screen Stack", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see the live screen stack.", MessageType.Info);
                return;
            }

            if (!UIServices.IsInitialized)
            {
                EditorGUILayout.HelpBox("UIServiceBootstrap has not initialized. Add it to your scene via the Setup tab.", MessageType.Warning);
                return;
            }

            var nav = NavigationManager.Instance;
            if (nav == null)
            {
                EditorGUILayout.HelpBox("NavigationManager not found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"Stack Depth: {nav.StackDepth}");
            EditorGUILayout.Space(4);

            var currentScreen = nav.CurrentScreen;
            if (currentScreen != null)
                DrawNavEntry("Active Screen", currentScreen, new Color(0.2f, 0.6f, 0.2f));
            else
                EditorGUILayout.LabelField("No active screen.", EditorStyles.miniLabel);

            if (nav.HasOpenModal)
            {
                EditorGUILayout.Space(4);
                var currentModal = nav.CurrentModal;
                if (currentModal != null)
                    DrawNavEntry("Active Modal", currentModal, new Color(0.6f, 0.4f, 0.2f));
            }
        }

        private void DrawNavEntry(string label, NavigationEntry entry, Color color)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Id", entry.Id ?? "(null)");
            EditorGUILayout.LabelField("Layer", entry.Layer.ToString());
            EditorGUILayout.LabelField("Visible", entry.IsVisible.ToString());
            if (entry.Handle.IsValid)
                EditorGUILayout.LabelField("Handle", entry.Handle.ToString());
            if (entry.NavigationData != null)
                EditorGUILayout.LabelField("Data", entry.NavigationData.ToString());
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        // TAB 4: FOCUS DEBUG
        // =====================================================================

        private void DrawFocusDebug()
        {
            EditorGUILayout.LabelField("Focus Manager Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see focus state.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Focus Stack Depth: {FocusManager.Depth}");
        }

        // =====================================================================
        // ASSET CREATION HELPERS
        // =====================================================================

        private void CreateManifest()
        {
            EnsureDirectoryExists("Assets/Resources");

            var manifest = CreateInstance<ScreenManifestSO>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/ScreenManifest.asset");
            AssetDatabase.CreateAsset(manifest, path);
            AssetDatabase.SaveAssets();
            _cachedManifest = manifest;

            Selection.activeObject = manifest;
            EditorGUIUtility.PingObject(manifest);
            Debug.Log($"[UI Workstation] Created ScreenManifest at {path}");
        }

        private void CreateTransitionProfile(string profileName, TransitionType type, float duration)
        {
            string dir = "Assets/UI/Transitions";
            EnsureDirectoryExists(dir);

            var profile = CreateInstance<TransitionProfileSO>();
            profile.Type = type;
            profile.Duration = duration;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{profileName}.asset");
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            Debug.Log($"[UI Workstation] Created TransitionProfile at {path}");
        }

        private void CreateTheme(string themeName)
        {
            string dir = "Assets/UI/Themes";
            EnsureDirectoryExists(dir);

            var theme = CreateInstance<UIThemeSO>();
            theme.ThemeName = themeName;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{themeName}.asset");
            AssetDatabase.CreateAsset(theme, path);
            AssetDatabase.SaveAssets();

            _previewTheme = theme;
            Selection.activeObject = theme;
            EditorGUIUtility.PingObject(theme);
            Debug.Log($"[UI Workstation] Created Theme at {path}");
        }

        private void AddScreenToManifest(ScreenManifestSO manifest)
        {
            var so = GetManifestSO(manifest);
            so.Update();

            var screensProp = so.FindProperty("_screens");
            screensProp.InsertArrayElementAtIndex(screensProp.arraySize);
            var newElement = screensProp.GetArrayElementAtIndex(screensProp.arraySize - 1);

            newElement.FindPropertyRelative("ScreenId").stringValue = _newScreenId;
            newElement.FindPropertyRelative("Layer").enumValueIndex = (int)_newScreenLayer;
            newElement.FindPropertyRelative("UXMLPath").stringValue = _newScreenUXMLPath;
            newElement.FindPropertyRelative("Poolable").boolValue = _newScreenPoolable;
            newElement.FindPropertyRelative("BlocksInput").boolValue = _newScreenBlocksInput;

            so.ApplyModifiedProperties();

            Debug.Log($"[UI Workstation] Added screen '{_newScreenId}' to manifest.");
            _uxmlExistsCache.Clear();
            _newScreenId = "";
            _newScreenUXMLPath = "";
        }

        private void RunFullSetup()
        {
            // 1. Manifest
            if (FindManifest() == null)
                CreateManifest();

            // 2. Default transitions
            var transitionGuids = AssetDatabase.FindAssets("t:TransitionProfileSO");
            TransitionProfileSO fadeNormal = null;
            if (transitionGuids.Length == 0)
            {
                CreateTransitionProfile("Fade_Fast", TransitionType.Fade, 0.15f);
                CreateTransitionProfile("Fade_Normal", TransitionType.Fade, 0.25f);
                CreateTransitionProfile("SlideUp_Modal", TransitionType.SlideUp, 0.3f);
                CreateTransitionProfile("Scale_Popup", TransitionType.Scale, 0.2f);
                AssetDatabase.Refresh();
            }

            // Find Fade_Normal to assign as default
            transitionGuids = AssetDatabase.FindAssets("t:TransitionProfileSO");
            foreach (var guid in transitionGuids)
            {
                var p = AssetDatabase.LoadAssetAtPath<TransitionProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null && p.name.Contains("Fade_Normal"))
                {
                    fadeNormal = p;
                    break;
                }
            }

            // 3. Default theme
            var themeGuids = AssetDatabase.FindAssets("t:UIThemeSO");
            UIThemeSO defaultTheme = null;
            if (themeGuids.Length == 0)
            {
                CreateTheme("Default");
                themeGuids = AssetDatabase.FindAssets("t:UIThemeSO");
            }
            if (themeGuids.Length > 0)
                defaultTheme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(AssetDatabase.GUIDToAssetPath(themeGuids[0]));

            // 4. Assign defaults to manifest
            var manifest = FindManifest();
            if (manifest != null)
            {
                var mso = GetManifestSO(manifest);
                mso.Update();
                if (fadeNormal != null)
                {
                    mso.FindProperty("DefaultOpenTransition").objectReferenceValue = fadeNormal;
                    mso.FindProperty("DefaultCloseTransition").objectReferenceValue = fadeNormal;
                }
                if (defaultTheme != null)
                    mso.FindProperty("DefaultTheme").objectReferenceValue = defaultTheme;
                mso.ApplyModifiedProperties();
            }

            // 5. Scene bootstrap
            if (FindAnyObjectByType<UIServiceBootstrap>() == null)
            {
                var go = new GameObject("UIServiceBootstrap");
                go.AddComponent<UIServiceBootstrap>();
                Undo.RegisterCreatedObjectUndo(go, "Create UIServiceBootstrap");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[UI Workstation] Full setup complete! Manifest, transitions, theme, and bootstrap are ready.");
        }

        // =====================================================================
        // UTILITY
        // =====================================================================

        private ScreenManifestSO FindManifest()
        {
            if (_cachedManifest != null) return _cachedManifest;

            _cachedManifest = Resources.Load<ScreenManifestSO>("ScreenManifest");
            if (_cachedManifest != null) return _cachedManifest;

            var guids = AssetDatabase.FindAssets("t:ScreenManifestSO");
            if (guids.Length > 0)
                _cachedManifest = AssetDatabase.LoadAssetAtPath<ScreenManifestSO>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return _cachedManifest;
        }

        private SerializedObject GetManifestSO(ScreenManifestSO manifest)
        {
            if (_manifestSO == null || _manifestSO.targetObject != manifest)
                _manifestSO = new SerializedObject(manifest);
            return _manifestSO;
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
