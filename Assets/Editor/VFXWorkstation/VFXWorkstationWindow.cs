using UnityEditor;
using UnityEngine;
using DIG.VFX;

namespace DIG.VFX.Editor
{
    /// <summary>
    /// EPIC 16.7 Phase 6: Editor window for VFX pipeline debugging.
    /// 4 tabs: Budget Monitor, LOD Visualizer, Request Log, Registry Browser.
    /// </summary>
    public class VFXWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private static readonly string[] TabNames = { "Budget", "LOD", "Log", "Registry" };
        private Vector2 _scrollPos;
        private double _nextRepaintTime;
        private const double RepaintInterval = 0.25; // 4 Hz
        private VFXTypeDatabase _cachedDatabase;

        [MenuItem("DIG/VFX Workstation")]
        public static void ShowWindow()
        {
            GetWindow<VFXWorkstationWindow>("VFX Workstation");
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
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawBudgetMonitor(); break;
                case 1: DrawLODVisualizer(); break;
                case 2: DrawRequestLog(); break;
                case 3: DrawRegistryBrowser(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBudgetMonitor()
        {
            EditorGUILayout.LabelField("Per-Category Budget Usage", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live budget data.", MessageType.Info);
                return;
            }

            string[] categoryNames = { "Combat", "Environment", "Ability", "Death", "UI", "Ambient", "Interaction" };
            int[] defaultBudgets = { 16, 24, 12, 8, 20, 10, 8 };

            for (int i = 0; i < 7; i++)
            {
                int requested = VFXTelemetry.RequestedPerCategory[i];
                int executed = VFXTelemetry.ExecutedPerCategory[i];
                int culled = VFXTelemetry.CulledPerCategory[i];
                int budget = defaultBudgets[i];

                float ratio = budget > 0 ? (float)requested / budget : 0f;
                var color = ratio > 1f ? Color.red : (ratio > 0.75f ? Color.yellow : Color.green);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(categoryNames[i], GUILayout.Width(100));

                var rect = EditorGUILayout.GetControlRect(GUILayout.Height(18));
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                var filled = rect;
                filled.width *= Mathf.Min(ratio, 1f);
                EditorGUI.DrawRect(filled, color);
                EditorGUI.LabelField(rect, $"  {executed}/{budget} (req:{requested} cull:{culled})",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Frame Totals", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Requested: {VFXTelemetry.RequestedThisFrame}");
            EditorGUILayout.LabelField($"Executed: {VFXTelemetry.ExecutedThisFrame}");
            EditorGUILayout.LabelField($"Culled (Budget): {VFXTelemetry.CulledByBudgetThisFrame}");
            EditorGUILayout.LabelField($"Culled (LOD): {VFXTelemetry.CulledByLODThisFrame}");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Session Totals", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Requested: {VFXTelemetry.TotalRequested}");
            EditorGUILayout.LabelField($"Total Executed: {VFXTelemetry.TotalExecuted}");
            EditorGUILayout.LabelField($"Total Culled: {VFXTelemetry.TotalCulled}");
        }

        private void DrawLODVisualizer()
        {
            EditorGUILayout.LabelField("VFX LOD Distance Thresholds", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Full (all effects)", "0 - 15m");
            EditorGUILayout.LabelField("Reduced (50% particles)", "15 - 40m");
            EditorGUILayout.LabelField("Minimal (billboard)", "40 - 80m");
            EditorGUILayout.LabelField("Culled", "80m+");

            EditorGUILayout.Space(8);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see LOD distribution.", MessageType.Info);
                return;
            }

            string[] tierNames = { "Full", "Reduced", "Minimal", "Culled" };
            for (int i = 0; i < 4; i++)
            {
                EditorGUILayout.LabelField($"{tierNames[i]}: {VFXTelemetry.ExecutedPerLODTier[i]} this frame");
            }
        }

        private void DrawRequestLog()
        {
            EditorGUILayout.LabelField("VFX Request Log", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see request log.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Request logging is available via the VFXTelemetry static counters.\n" +
                "For per-request detail logging, enable VFXManager.EnableDebugLogging.",
                MessageType.Info);
        }

        private void DrawRegistryBrowser()
        {
            EditorGUILayout.LabelField("VFX Type Database Browser", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_cachedDatabase == null)
                _cachedDatabase = Resources.Load<VFXTypeDatabase>("VFXTypeDatabase");

            if (_cachedDatabase == null)
            {
                EditorGUILayout.HelpBox(
                    "No VFXTypeDatabase found in Resources/.\n" +
                    "Create one via Assets > Create > DIG > VFX > VFX Type Database.",
                    MessageType.Warning);

                if (GUILayout.Button("Create VFX Type Database"))
                {
                    var asset = ScriptableObject.CreateInstance<VFXTypeDatabase>();
                    if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                        AssetDatabase.CreateFolder("Assets", "Resources");
                    AssetDatabase.CreateAsset(asset, "Assets/Resources/VFXTypeDatabase.asset");
                    AssetDatabase.SaveAssets();
                    EditorGUIUtility.PingObject(asset);
                    _cachedDatabase = asset;
                }
                return;
            }

            if (GUILayout.Button("Select Database Asset"))
                EditorGUIUtility.PingObject(_cachedDatabase);

            EditorGUILayout.Space(4);

            var entries = _cachedDatabase.AllEntries;
            EditorGUILayout.LabelField($"Entries: {entries.Count}");
            EditorGUILayout.Space(4);

            foreach (var entry in entries)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"[{entry.TypeId}]", GUILayout.Width(60));
                EditorGUILayout.LabelField(entry.Name ?? "(unnamed)", GUILayout.Width(150));
                EditorGUILayout.LabelField(entry.DefaultCategory.ToString(), GUILayout.Width(90));

                if (entry.Prefab != null)
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(entry.Prefab);
                }
                else
                {
                    EditorGUILayout.LabelField("NO PREFAB", new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = Color.red } }, GUILayout.Width(70));
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
