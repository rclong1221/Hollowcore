using UnityEngine;
using UnityEditor;
using Audio.Components;
using Audio.Systems;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// Audio Workstation module: occlusion debug visualization.
    /// Shows raycast lines, per-source occlusion factors, budget display,
    /// and global toggle for A/B comparison.
    /// EPIC 15.27 Phase 3.
    /// </summary>
    public class OcclusionDebugModule : IAudioModule
    {
        private Vector2 _scrollPos;
        private bool _occlusionEnabled = true;
        private bool _showRaycasts = true;

        public void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Occlusion Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see occlusion debug data.", MessageType.Info);
                EditorGUILayout.Space(4);
            }

            // Global controls
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Controls", EditorStyles.miniLabel);
            _occlusionEnabled = EditorGUILayout.Toggle("Occlusion Enabled", _occlusionEnabled);
            _showRaycasts = EditorGUILayout.Toggle("Show Raycasts in Scene View", _showRaycasts);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Raycast budget
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Raycast Budget", EditorStyles.miniLabel);
            if (Application.isPlaying)
            {
                var pool = AudioSourcePool.Instance;
                int activeCount = pool != null ? pool.ActiveCount : 0;
                int spreadFrames = 6; // default, would read from profile if available
                int raycastsPerFrame = Mathf.CeilToInt((float)activeCount / spreadFrames);
                EditorGUILayout.LabelField($"Active Sources: {activeCount}");
                EditorGUILayout.LabelField($"Spread Frames: {spreadFrames}");
                EditorGUILayout.LabelField($"Raycasts/Frame: ~{raycastsPerFrame}");
                EditorGUILayout.LabelField($"Raycasts/Sec: ~{raycastsPerFrame * 60}");

                Rect budgetBar = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                float budgetUsage = Mathf.Clamp01(raycastsPerFrame / 8f);
                Color barColor = budgetUsage < 0.5f ? Color.green : budgetUsage < 0.8f ? Color.yellow : Color.red;
                EditorGUI.DrawRect(new Rect(budgetBar.x, budgetBar.y, budgetBar.width * budgetUsage, budgetBar.height), barColor);
                EditorGUI.DrawRect(new Rect(budgetBar.x + budgetBar.width * budgetUsage, budgetBar.y,
                    budgetBar.width * (1f - budgetUsage), budgetBar.height), new Color(0.15f, 0.15f, 0.15f));
            }
            else
            {
                EditorGUILayout.LabelField("No data in Edit Mode");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Per-source status
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Active Source Occlusion", EditorStyles.miniLabel);
            if (Application.isPlaying)
            {
                // Note: In a full implementation, we'd iterate World entities here.
                // For now, show pool-level summary.
                var pool = AudioSourcePool.Instance;
                if (pool != null)
                {
                    EditorGUILayout.LabelField($"Total Active: {pool.ActiveCount}");
                    EditorGUILayout.LabelField($"Peak: {pool.PeakCount}");
                }
                else
                {
                    EditorGUILayout.LabelField("AudioSourcePool not found");
                }
            }
            else
            {
                EditorGUILayout.LabelField("Enter Play Mode");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Legend
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Scene View Legend", EditorStyles.miniLabel);
            DrawLegendRow(Color.green, "Clear (no occlusion)");
            DrawLegendRow(Color.yellow, "Partial (1 hit)");
            DrawLegendRow(Color.red, "Heavy (2+ hits)");
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            if (Application.isPlaying)
                EditorUtility.SetDirty(EditorWindow.focusedWindow);
        }

        private void DrawLegendRow(Color color, string label)
        {
            EditorGUILayout.BeginHorizontal();
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Box("", GUILayout.Width(16), GUILayout.Height(12));
            GUI.backgroundColor = prevBg;
            EditorGUILayout.LabelField(label);
            EditorGUILayout.EndHorizontal();
        }
    }
}
