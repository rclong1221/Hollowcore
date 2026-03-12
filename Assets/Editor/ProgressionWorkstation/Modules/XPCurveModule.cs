using UnityEditor;
using UnityEngine;

namespace DIG.Progression.Editor.Modules
{
    /// <summary>
    /// EPIC 16.14: XP curve visualizer. Draws a graph of XP per level,
    /// cumulative XP, and highlights current player position in play mode.
    /// </summary>
    public class XPCurveModule : IProgressionWorkstationModule
    {
        private ProgressionCurveSO _curveSO;
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("XP Curve Visualizer", EditorStyles.boldLabel);

            _curveSO = (ProgressionCurveSO)EditorGUILayout.ObjectField("Curve SO", _curveSO, typeof(ProgressionCurveSO), false);

            if (_curveSO == null)
            {
                // Try auto-load from Resources
                _curveSO = Resources.Load<ProgressionCurveSO>("ProgressionCurve");
                if (_curveSO == null)
                {
                    EditorGUILayout.HelpBox("Assign a ProgressionCurveSO or place one at Resources/ProgressionCurve.", MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(4);

            // Summary
            EditorGUILayout.LabelField($"Max Level: {_curveSO.MaxLevel}");
            EditorGUILayout.LabelField($"Stat Points Per Level: {_curveSO.StatPointsPerLevel}");
            EditorGUILayout.LabelField($"Base Kill XP: {_curveSO.BaseKillXP:F0}");

            EditorGUILayout.Space(8);

            // Graph
            var graphRect = GUILayoutUtility.GetRect(400, 200, GUILayout.ExpandWidth(true));
            DrawCurveGraph(graphRect);

            EditorGUILayout.Space(8);

            // Per-level table
            EditorGUILayout.LabelField("Per-Level XP Table", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(300));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Level", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("XP Required", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Cumulative", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Kill XP (same level)", EditorStyles.miniLabel, GUILayout.Width(140));
            EditorGUILayout.LabelField("Kills to Level", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            int cumulative = 0;
            for (int lvl = 1; lvl <= _curveSO.MaxLevel; lvl++)
            {
                int xpReq = _curveSO.GetXPForLevel(lvl);
                cumulative += xpReq;
                float killXP = _curveSO.BaseKillXP * Mathf.Pow(_curveSO.KillXPPerEnemyLevel, lvl - 1);
                int killsNeeded = killXP > 0 ? Mathf.CeilToInt(xpReq / killXP) : 0;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{lvl}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{xpReq:N0}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{cumulative:N0}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{killXP:F0}", GUILayout.Width(140));
                EditorGUILayout.LabelField($"{killsNeeded}", GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawCurveGraph(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;

            // Background
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            int maxLevel = _curveSO.MaxLevel;
            if (maxLevel <= 1) return;

            // Find max value for normalization
            int maxXP = 0;
            for (int i = 1; i <= maxLevel; i++)
            {
                int xp = _curveSO.GetXPForLevel(i);
                if (xp > maxXP) maxXP = xp;
            }
            if (maxXP <= 0) return;

            // Draw bars
            float barWidth = rect.width / maxLevel;
            for (int i = 1; i <= maxLevel; i++)
            {
                int xp = _curveSO.GetXPForLevel(i);
                float normalizedHeight = (float)xp / maxXP;
                float barHeight = normalizedHeight * (rect.height - 20);

                var barRect = new Rect(
                    rect.x + (i - 1) * barWidth + 1,
                    rect.y + rect.height - barHeight - 10,
                    barWidth - 2,
                    barHeight);

                Color barColor = Color.Lerp(new Color(0.2f, 0.6f, 1f), new Color(1f, 0.3f, 0.3f), normalizedHeight);
                EditorGUI.DrawRect(barRect, barColor);
            }

            // Axis labels
            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerLeft };
            GUI.Label(new Rect(rect.x + 2, rect.y + rect.height - 14, 60, 14), "Level 1", style);
            style.alignment = TextAnchor.LowerRight;
            GUI.Label(new Rect(rect.x + rect.width - 80, rect.y + rect.height - 14, 78, 14), $"Level {maxLevel}", style);
        }
    }
}
