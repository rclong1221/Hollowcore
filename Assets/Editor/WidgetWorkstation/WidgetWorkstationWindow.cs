using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DIG.Widgets.Adapters;
using DIG.Widgets.Config;
using DIG.Widgets.Debug;
using DIG.Widgets.Editor;
using DIG.Editor.Utilities;

namespace DIG.Editor.WidgetWorkstation
{
    /// <summary>
    /// EPIC 15.26: Widget Workstation — one-click setup for the Widget Framework.
    /// Creates the WidgetFramework GameObject with selected adapters/managers,
    /// creates required ScriptableObject assets, and auto-wires inspector references.
    /// </summary>
    public class WidgetWorkstationWindow : EditorWindow
    {
        // ── Constants ────────────────────────────────────────────────
        private const string GoName = "WidgetFramework";
        private const string AssetFolder = "Assets/Settings/Widgets";

        private static readonly string ProfilePath = $"{AssetFolder}/WidgetProfile_Shooter.asset";
        private static readonly string StylePath = $"{AssetFolder}/WidgetStyle_Standard.asset";
        private static readonly string AccessibilityPath = $"{AssetFolder}/WidgetAccessibility_Default.asset";
        private static readonly string PaletteDeutPath = $"{AssetFolder}/ColorblindPalette_Deuteranopia.asset";
        private static readonly string PaletteProtPath = $"{AssetFolder}/ColorblindPalette_Protanopia.asset";
        private static readonly string PaletteTritPath = $"{AssetFolder}/ColorblindPalette_Tritanopia.asset";
        private static readonly string OffScreenPath = $"{AssetFolder}/OffScreenIndicatorConfig.asset";

        // ── Scene Setup Toggles ──────────────────────────────────────
        private bool _addHealthBarAdapter = true;
        private bool _addParadigmConfig = true;
        private bool _addDamageNumberAdapter = true;
        private bool _addFloatingTextAdapter;
        private bool _addInteractAdapter;
        private bool _addAccessibilityManager;
        private bool _addDebugOverlay;

        // ── Asset Setup Toggles ──────────────────────────────────────
        private bool _createProfile = true;
        private bool _createStyle;
        private bool _createAccessibility;
        private bool _createPaletteDeuteranopia;
        private bool _createPaletteProtanopia;
        private bool _createPaletteTritanopia;
        private bool _createOffScreen;

        private Vector2 _scrollPos;

        // ─────────────────────────────────────────────────────────────

        [MenuItem("DIG/Widget Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<WidgetWorkstationWindow>("Widget Workstation");
            window.minSize = new Vector2(500, 600);
        }

        private void OnGUI()
        {
            DrawHeader();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawSceneStatus();
            EditorWindowUtilities.DrawSeparator();
            DrawSceneSetup();
            EditorWindowUtilities.DrawSeparator();
            DrawAssetSetup();
            EditorWindowUtilities.DrawSeparator();
            DrawQuickActions();

            EditorGUILayout.EndScrollView();
        }

        // ── Header ──────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("DIG Widget Workstation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                Repaint();
            EditorGUILayout.EndHorizontal();
        }

        // ── Scene Status ────────────────────────────────────────────

        private void DrawSceneStatus()
        {
            EditorWindowUtilities.DrawSectionHeader("Scene Status");

            var go = GameObject.Find(GoName);
            bool hasGo = go != null;

            DrawStatusWithSelect("WidgetFramework GameObject", hasGo, go);

            if (hasGo)
            {
                DrawComponentStatus<ParadigmWidgetConfig>("ParadigmWidgetConfig", go);
                DrawComponentStatus<HealthBarWidgetAdapter>("HealthBarWidgetAdapter", go);
                DrawComponentStatus<DamageNumberWidgetAdapter>("DamageNumberWidgetAdapter", go);
                DrawComponentStatus<FloatingTextWidgetAdapter>("FloatingTextWidgetAdapter", go);
                DrawComponentStatus<InteractWidgetAdapter>("InteractWidgetAdapter", go);
                DrawComponentStatus<WidgetAccessibilityManager>("AccessibilityManager", go);
                DrawComponentStatus<WidgetDebugOverlay>("DebugOverlay", go);
            }
        }

        private void DrawStatusWithSelect(string label, bool found, GameObject go)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(found ? "\u2705" : "\u274C", GUILayout.Width(20));
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            if (found && go != null)
            {
                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                    Selection.activeGameObject = go;
            }
            else
            {
                GUILayout.Label("not in scene", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawComponentStatus<T>(string label, GameObject go) where T : Component
        {
            bool has = go.GetComponent<T>() != null;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(has ? "\u2705" : "\u274C", GUILayout.Width(20));
            GUILayout.Label(label);
            EditorGUILayout.EndHorizontal();
        }

        // ── Scene Setup ─────────────────────────────────────────────

        private void DrawSceneSetup()
        {
            EditorWindowUtilities.DrawSectionHeader("Scene Setup");

            EditorGUILayout.HelpBox(
                "Select which components to add to the WidgetFramework GameObject.\n" +
                "Health Bar Adapter and Paradigm Config are required for the framework to activate.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            GUI.enabled = false;
            _addHealthBarAdapter = EditorGUILayout.ToggleLeft("Health Bar Adapter (required)", _addHealthBarAdapter);
            _addParadigmConfig = EditorGUILayout.ToggleLeft("Paradigm Widget Config (required)", _addParadigmConfig);
            GUI.enabled = true;

            _addDamageNumberAdapter = EditorGUILayout.ToggleLeft("Damage Number Adapter (recommended)", _addDamageNumberAdapter);
            _addFloatingTextAdapter = EditorGUILayout.ToggleLeft("Floating Text Adapter", _addFloatingTextAdapter);
            _addInteractAdapter = EditorGUILayout.ToggleLeft("Interact Adapter", _addInteractAdapter);
            _addAccessibilityManager = EditorGUILayout.ToggleLeft("Accessibility Manager", _addAccessibilityManager);
            _addDebugOverlay = EditorGUILayout.ToggleLeft("Debug Overlay (F10 in Play Mode)", _addDebugOverlay);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Create / Update Widget Framework GameObject", GUILayout.Height(28)))
                CreateWidgetFrameworkGameObject();
        }

        // ── Asset Setup ─────────────────────────────────────────────

        private void DrawAssetSetup()
        {
            EditorWindowUtilities.DrawSectionHeader("Asset Setup");

            EditorGUILayout.HelpBox(
                $"ScriptableObject assets are created in:\n{AssetFolder}/",
                MessageType.Info);

            EditorGUILayout.Space(4);

            DrawAssetToggle(ref _createProfile, "Paradigm Widget Profile (Shooter)", ProfilePath);
            DrawAssetToggle(ref _createStyle, "Widget Style Config (Standard)", StylePath);
            DrawAssetToggle(ref _createAccessibility, "Widget Accessibility Config", AccessibilityPath);
            DrawAssetToggle(ref _createPaletteDeuteranopia, "Colorblind Palette (Deuteranopia)", PaletteDeutPath);
            DrawAssetToggle(ref _createPaletteProtanopia, "Colorblind Palette (Protanopia)", PaletteProtPath);
            DrawAssetToggle(ref _createPaletteTritanopia, "Colorblind Palette (Tritanopia)", PaletteTritPath);
            DrawAssetToggle(ref _createOffScreen, "Off-Screen Indicator Config", OffScreenPath);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Create Selected Assets", GUILayout.Height(28)))
                CreateSelectedAssets();
        }

        private void DrawAssetToggle(ref bool toggle, string label, string path)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null;

            EditorGUILayout.BeginHorizontal();
            toggle = EditorGUILayout.ToggleLeft(label, toggle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(exists ? "\u2705 exists" : "\u274C missing",
                exists ? EditorStyles.miniLabel : EditorStyles.miniLabel,
                GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
        }

        // ── Quick Actions ────────────────────────────────────────────

        private void DrawQuickActions()
        {
            EditorWindowUtilities.DrawSectionHeader("Quick Actions");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Widget Debug Window"))
                WidgetDebugWindow.ShowWindow();

            if (GUILayout.Button("Ping Settings Folder"))
            {
                EnsureAssetFolder();
                var folder = AssetDatabase.LoadAssetAtPath<Object>(AssetFolder);
                if (folder != null)
                    EditorGUIUtility.PingObject(folder);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Create Logic: Scene ──────────────────────────────────────

        private void CreateWidgetFrameworkGameObject()
        {
            var go = GameObject.Find(GoName);
            bool isNew = go == null;

            if (isNew)
            {
                go = new GameObject(GoName);
                Undo.RegisterCreatedObjectUndo(go, "Create WidgetFramework");
            }
            else
            {
                Undo.RecordObject(go, "Update WidgetFramework");
            }

            // Required components (always added)
            EnsureComponent<HealthBarWidgetAdapter>(go);
            var config = EnsureComponent<ParadigmWidgetConfig>(go);

            // Optional components
            if (_addDamageNumberAdapter) EnsureComponent<DamageNumberWidgetAdapter>(go);
            if (_addFloatingTextAdapter) EnsureComponent<FloatingTextWidgetAdapter>(go);
            if (_addInteractAdapter) EnsureComponent<InteractWidgetAdapter>(go);
            var accessMgr = _addAccessibilityManager ? EnsureComponent<WidgetAccessibilityManager>(go) : null;
            if (_addDebugOverlay) EnsureComponent<WidgetDebugOverlay>(go);

            // Auto-wire ParadigmWidgetConfig
            AutoWireParadigmConfig(config);

            // Auto-wire WidgetAccessibilityManager
            if (accessMgr != null)
                AutoWireAccessibilityManager(accessMgr);

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);

            string verb = isNew ? "Created" : "Updated";
            Debug.Log($"[Widget Workstation] {verb} WidgetFramework GameObject with {go.GetComponents<Component>().Length - 1} components.");
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null)
                comp = Undo.AddComponent<T>(go);
            return comp;
        }

        private void AutoWireParadigmConfig(ParadigmWidgetConfig config)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ParadigmWidgetProfile>(ProfilePath);
            if (profile == null) return;

            var so = new SerializedObject(config);
            var profilesProp = so.FindProperty("_profiles");

            // Only wire if the array is empty (don't overwrite user edits)
            if (profilesProp.arraySize == 0)
            {
                profilesProp.arraySize = 1;
                profilesProp.GetArrayElementAtIndex(0).objectReferenceValue = profile;
            }

            var fallbackProp = so.FindProperty("_fallbackProfile");
            if (fallbackProp.objectReferenceValue == null)
                fallbackProp.objectReferenceValue = profile;

            so.ApplyModifiedProperties();
        }

        private void AutoWireAccessibilityManager(WidgetAccessibilityManager manager)
        {
            var accessConfig = AssetDatabase.LoadAssetAtPath<WidgetAccessibilityConfig>(AccessibilityPath);
            if (accessConfig == null) return;

            var so = new SerializedObject(manager);
            var configProp = so.FindProperty("_defaultConfig");
            if (configProp.objectReferenceValue == null)
                configProp.objectReferenceValue = accessConfig;
            so.ApplyModifiedProperties();
        }

        // ── Create Logic: Assets ─────────────────────────────────────

        private void CreateSelectedAssets()
        {
            EnsureAssetFolder();
            int created = 0;

            if (_createProfile)
            {
                if (CreateIfMissing<ParadigmWidgetProfile>(ProfilePath) != null)
                    created++;
            }

            if (_createStyle)
            {
                if (CreateIfMissing<WidgetStyleConfig>(StylePath) != null)
                    created++;
            }

            if (_createAccessibility)
            {
                if (CreateIfMissing<WidgetAccessibilityConfig>(AccessibilityPath) != null)
                    created++;
            }

            if (_createPaletteDeuteranopia)
            {
                var palette = CreateIfMissing<ColorblindPalette>(PaletteDeutPath);
                if (palette != null)
                {
                    palette.Mode = ColorblindMode.Deuteranopia;
                    palette.HealthFull = new Color(0.2f, 0.4f, 1f, 1f);   // green → blue
                    palette.HealthLow = new Color(1f, 0.6f, 0.1f, 1f);    // red → orange
                    palette.DamageText = new Color(1f, 0.6f, 0.1f, 1f);   // red → orange
                    palette.HealingText = new Color(0.2f, 0.8f, 1f, 1f);  // green → cyan
                    EditorUtility.SetDirty(palette);
                    created++;
                }
            }

            if (_createPaletteProtanopia)
            {
                var palette = CreateIfMissing<ColorblindPalette>(PaletteProtPath);
                if (palette != null)
                {
                    palette.Mode = ColorblindMode.Protanopia;
                    palette.HealthFull = new Color(0.2f, 0.85f, 0.2f, 1f);
                    palette.HealthLow = new Color(1f, 0.9f, 0.2f, 1f);    // red → yellow
                    palette.DamageText = new Color(1f, 0.9f, 0.2f, 1f);
                    palette.HealingText = new Color(0.2f, 0.85f, 0.2f, 1f);
                    EditorUtility.SetDirty(palette);
                    created++;
                }
            }

            if (_createPaletteTritanopia)
            {
                var palette = CreateIfMissing<ColorblindPalette>(PaletteTritPath);
                if (palette != null)
                {
                    palette.Mode = ColorblindMode.Tritanopia;
                    palette.HealthFull = new Color(0.2f, 0.85f, 0.2f, 1f);
                    palette.HealthLow = new Color(0.85f, 0.2f, 0.2f, 1f);
                    palette.ShieldColor = new Color(0.8f, 0.3f, 0.8f, 1f); // blue → pink
                    EditorUtility.SetDirty(palette);
                    created++;
                }
            }

            if (_createOffScreen)
            {
                if (CreateIfMissing<OffScreenIndicatorConfig>(OffScreenPath) != null)
                    created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Widget Workstation] Created {created} asset(s) in {AssetFolder}/");
        }

        // ── Utility ──────────────────────────────────────────────────

        /// <summary>
        /// Creates a ScriptableObject asset if it doesn't already exist.
        /// Returns the instance (existing or new), or null if path was skipped.
        /// </summary>
        private static T CreateIfMissing<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return null; // already exists, don't count as created

            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
            Debug.Log($"[Widget Workstation] Created {path}");
            return inst;
        }

        private static void EnsureAssetFolder()
        {
            if (AssetDatabase.IsValidFolder(AssetFolder)) return;

            // Create path segments
            string parent = "Assets";
            string[] parts = AssetFolder.Replace("Assets/", "").Split('/');
            foreach (string part in parts)
            {
                string next = $"{parent}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, part);
                parent = next;
            }
        }
    }
}
