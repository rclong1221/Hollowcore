using UnityEngine;
using UnityEditor;
using DIG.Widgets;
using DIG.Widgets.Systems;
using DIG.Widgets.Rendering;
using DIG.Widgets.Config;

namespace DIG.Widgets.Editor
{
    /// <summary>
    /// EPIC 15.26 Phase 8: Editor window for real-time widget framework inspection.
    /// Open via Window > DIG > Widget Debug.
    ///
    /// Shows:
    ///   - Framework status (active/inactive)
    ///   - Active paradigm profile and key settings
    ///   - Per-type widget counts
    ///   - Registered renderer list
    ///   - Live entity count and budget utilization
    /// </summary>
    public class WidgetDebugWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private bool _autoRefresh = true;

        [MenuItem("Window/DIG/Widget Debug")]
        public static void ShowWindow()
        {
            var window = GetWindow<WidgetDebugWindow>("Widget Debug");
            window.minSize = new Vector2(300, 400);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (_autoRefresh && Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Widget Framework Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _autoRefresh = EditorGUILayout.Toggle("Auto-Refresh (Play Mode)", _autoRefresh);
            EditorGUILayout.Space(8);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live widget data.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawFrameworkStatus();
            EditorGUILayout.Space(8);
            DrawParadigmInfo();
            EditorGUILayout.Space(8);
            DrawWidgetCounts();
            EditorGUILayout.Space(8);
            DrawRendererInfo();

            EditorGUILayout.EndScrollView();
        }

        private void DrawFrameworkStatus()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            bool active = WidgetProjectionSystem.FrameworkActive;
            var statusColor = active ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            var prevColor = GUI.color;
            GUI.color = statusColor;
            EditorGUILayout.LabelField("Framework", active ? "ACTIVE" : "INACTIVE");
            GUI.color = prevColor;

            EditorGUILayout.IntField("Total Projected", WidgetProjectionSystem.TotalProjectedCount);
            EditorGUILayout.IntField("Visible", WidgetProjectionSystem.VisibleCount);

            int budget = 40;
            if (ParadigmWidgetConfig.HasInstance && ParadigmWidgetConfig.Instance.ActiveProfile != null)
                budget = ParadigmWidgetConfig.Instance.ActiveProfile.MaxActiveWidgets;

            float util = budget > 0 ? (float)WidgetProjectionSystem.VisibleCount / budget * 100f : 0f;
            EditorGUILayout.IntField("Budget", budget);

            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, util / 100f, $"Budget Utilization: {util:F0}%");
        }

        private void DrawParadigmInfo()
        {
            EditorGUILayout.LabelField("Paradigm", EditorStyles.boldLabel);

            if (!ParadigmWidgetConfig.HasInstance)
            {
                EditorGUILayout.LabelField("ParadigmWidgetConfig", "Not Found");
                return;
            }

            var config = ParadigmWidgetConfig.Instance;
            var profile = config.ActiveProfile;

            if (profile == null)
            {
                EditorGUILayout.LabelField("Active Profile", "None (using defaults)");
                return;
            }

            EditorGUILayout.LabelField("Active Profile", profile.name);
            EditorGUILayout.LabelField("Paradigm", profile.Paradigm.ToString());
            EditorGUILayout.FloatField("LOD Distance Mult", profile.LODDistanceMultiplier);
            EditorGUILayout.FloatField("Widget Scale Mult", profile.WidgetScaleMultiplier);
            EditorGUILayout.FloatField("Distance Falloff", profile.DistanceFalloff);
            EditorGUILayout.LabelField("Billboard", profile.Billboard.ToString());
            EditorGUILayout.LabelField("Health Bar Style", profile.Style.ToString());
        }

        private void DrawWidgetCounts()
        {
            EditorGUILayout.LabelField("Widget Type Flags", EditorStyles.boldLabel);

            if (!WidgetProjectionSystem.FrameworkActive) return;
            var projected = WidgetProjectionSystem.ProjectedWidgets;
            if (!projected.IsCreated) return;

            int healthBar = 0, castBar = 0, buffRow = 0, bossPlate = 0, lootLabel = 0, offScreen = 0;

            using var values = projected.GetValueArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < values.Length; i++)
            {
                var flags = values[i].ActiveFlags;
                if ((flags & WidgetFlags.HealthBar) != 0) healthBar++;
                if ((flags & WidgetFlags.CastBar) != 0) castBar++;
                if ((flags & WidgetFlags.BuffRow) != 0) buffRow++;
                if ((flags & WidgetFlags.BossPlate) != 0) bossPlate++;
                if ((flags & WidgetFlags.LootLabel) != 0) lootLabel++;
                if ((flags & WidgetFlags.OffScreen) != 0) offScreen++;
            }

            EditorGUILayout.IntField("HealthBar", healthBar);
            EditorGUILayout.IntField("CastBar", castBar);
            EditorGUILayout.IntField("BuffRow", buffRow);
            EditorGUILayout.IntField("BossPlate", bossPlate);
            EditorGUILayout.IntField("LootLabel", lootLabel);
            EditorGUILayout.IntField("OffScreen", offScreen);
        }

        private void DrawRendererInfo()
        {
            EditorGUILayout.LabelField("Registered Renderers", EditorStyles.boldLabel);

            int total = WidgetRendererRegistry.RendererCount;
            EditorGUILayout.IntField("Total", total);

            DrawRendererTypeRow(WidgetType.HealthBar, "HealthBar");
            DrawRendererTypeRow(WidgetType.CastBar, "CastBar");
            DrawRendererTypeRow(WidgetType.BuffRow, "BuffRow");
            DrawRendererTypeRow(WidgetType.BossPlate, "BossPlate");
            DrawRendererTypeRow(WidgetType.LootLabel, "LootLabel");
            DrawRendererTypeRow(WidgetType.DamageNumber, "DamageNumber");
            DrawRendererTypeRow(WidgetType.FloatingText, "FloatingText");
            DrawRendererTypeRow(WidgetType.InteractPrompt, "InteractPrompt");
            DrawRendererTypeRow(WidgetType.OffScreenIndicator, "OffScreen");
        }

        private void DrawRendererTypeRow(WidgetType type, string label)
        {
            var renderers = WidgetRendererRegistry.GetRenderers(type);
            int count = renderers?.Count ?? 0;
            EditorGUILayout.LabelField($"  {label}", count > 0 ? $"{count} registered" : "---");
        }
    }
}
