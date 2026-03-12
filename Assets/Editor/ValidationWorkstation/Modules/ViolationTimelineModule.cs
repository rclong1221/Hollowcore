using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Validation.Editor.Modules
{
    /// <summary>
    /// EPIC 17.11: Horizontal timeline showing violation events as colored dots.
    /// Reads from ValidationTelemetryQueue. Click event for details.
    /// </summary>
    public class ViolationTimelineModule : IValidationWorkstationModule
    {
        public string ModuleName => "Violation Timeline";

        private Vector2 _scroll;
        private readonly List<ValidationTelemetryEntry> _history = new List<ValidationTelemetryEntry>();
        private const int MaxHistory = 500;
        private int _selectedIndex = -1;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Violation Timeline", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Drain telemetry queue into history
            while (ValidationTelemetryQueue.Queue.TryDequeue(out var entry))
            {
                _history.Add(entry);
                if (_history.Count > MaxHistory)
                    _history.RemoveAt(0);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Events: {_history.Count}");
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _history.Clear();
                _selectedIndex = -1;
            }
            EditorGUILayout.EndHorizontal();

            if (_history.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    Application.isPlaying
                        ? "No violation events recorded yet. Events appear when players trigger violations."
                        : "Enter Play mode to record violation events.",
                    MessageType.Info);
                return;
            }

            // Timeline visualization
            EditorGUILayout.Space(4);
            var timelineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(60));
            EditorGUI.DrawRect(timelineRect, new Color(0.15f, 0.15f, 0.15f));

            float dotSize = 8f;
            float spacing = Mathf.Max(dotSize + 2, timelineRect.width / Mathf.Max(_history.Count, 1));

            for (int i = 0; i < _history.Count; i++)
            {
                float x = timelineRect.x + (i * spacing);
                if (x > timelineRect.xMax - dotSize) break;

                var entry = _history[i];
                Color dotColor = GetPenaltyColor(entry.PenaltyLevel);
                float y = timelineRect.y + timelineRect.height * 0.5f - dotSize * 0.5f;

                var dotRect = new Rect(x, y, dotSize, dotSize);
                EditorGUI.DrawRect(dotRect, dotColor);

                // Click detection
                if (Event.current.type == EventType.MouseDown && dotRect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = i;
                    Event.current.Use();
                }
            }

            // Selected event details
            EditorGUILayout.Space(8);
            if (_selectedIndex >= 0 && _selectedIndex < _history.Count)
            {
                var selected = _history[_selectedIndex];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Event Details", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Network ID: {selected.NetworkId}");
                EditorGUILayout.LabelField($"Violation Score: {selected.ViolationScore:F2}");
                EditorGUILayout.LabelField($"Penalty Level: {(PenaltyLevel)selected.PenaltyLevel}");
                EditorGUILayout.LabelField($"Warnings: {selected.WarningCount}");
                EditorGUILayout.LabelField($"Consecutive Kicks: {selected.ConsecutiveKicks}");
                EditorGUILayout.LabelField($"Server Tick: {selected.ServerTick}");
                EditorGUILayout.EndVertical();
            }

            // Event log list
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Event Log", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(200));

            // Most recent first
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                var entry = _history[i];
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = GetPenaltyColor(entry.PenaltyLevel);

                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label($"T:{entry.ServerTick}", GUILayout.Width(80));
                GUILayout.Label($"Net:{entry.NetworkId}", GUILayout.Width(70));
                GUILayout.Label($"Score:{entry.ViolationScore:F1}", GUILayout.Width(80));
                GUILayout.Label($"{(PenaltyLevel)entry.PenaltyLevel}", GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.EndScrollView();
        }

        private static Color GetPenaltyColor(byte penaltyLevel)
        {
            switch ((PenaltyLevel)penaltyLevel)
            {
                case PenaltyLevel.None: return new Color(0.3f, 0.8f, 0.3f);
                case PenaltyLevel.Warn: return new Color(1f, 0.9f, 0.3f);
                case PenaltyLevel.Kick: return new Color(1f, 0.5f, 0.2f);
                case PenaltyLevel.TempBan: return new Color(1f, 0.3f, 0.3f);
                case PenaltyLevel.PermaBan: return new Color(0.8f, 0.1f, 0.1f);
                default: return Color.gray;
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
