using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Analytics.Editor.Modules
{
    /// <summary>
    /// Horizontal timeline showing session duration with event markers.
    /// Color-coded by category, hover for details.
    /// </summary>
    public class SessionTimelineModule : IAnalyticsWorkstationModule
    {
        private Vector2 _scroll;
        private float _zoom = 1f;
        private int _hoveredIndex = -1;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Session Timeline", EditorStyles.boldLabel);

            if (!Application.isPlaying || !AnalyticsAPI.IsInitialized)
            {
                EditorGUILayout.HelpBox("Enter Play Mode with analytics initialized to view session timeline.", MessageType.Info);
                return;
            }

            var events = AnalyticsAPI.RecentEvents;
            if (events.Count == 0)
            {
                EditorGUILayout.HelpBox("No events recorded yet.", MessageType.Info);
                return;
            }

            long firstTs = events[0].TimestampUtcMs;
            long lastTs = events[events.Count - 1].TimestampUtcMs;
            float durationSec = (lastTs - firstTs) / 1000f;

            EditorGUILayout.LabelField($"Session Duration: {durationSec:F1}s | Events: {events.Count}");
            _zoom = EditorGUILayout.Slider("Zoom", _zoom, 0.5f, 5f);
            EditorGUILayout.Space(4);

            // Session summary
            var counts = new Dictionary<AnalyticsCategory, int>();
            foreach (var evt in events)
            {
                counts.TryGetValue(evt.Category, out int c);
                counts[evt.Category] = c + 1;
            }

            EditorGUILayout.BeginHorizontal();
            foreach (var kv in counts)
            {
                EditorGUILayout.LabelField($"{kv.Key}: {kv.Value}", EditorStyles.miniLabel, GUILayout.Width(120));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Timeline bar
            float totalWidth = Mathf.Max(400f, durationSec * 20f * _zoom);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120));

            Rect timelineRect = GUILayoutUtility.GetRect(totalWidth, 60);
            EditorGUI.DrawRect(timelineRect, new Color(0.15f, 0.15f, 0.15f));

            _hoveredIndex = -1;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                float t = durationSec > 0 ? (evt.TimestampUtcMs - firstTs) / 1000f / durationSec : 0.5f;
                float x = timelineRect.x + t * timelineRect.width;

                Color col = evt.Category switch
                {
                    AnalyticsCategory.Combat => new Color(0.9f, 0.3f, 0.3f),
                    AnalyticsCategory.Progression => new Color(0.3f, 0.6f, 1f),
                    AnalyticsCategory.Economy => new Color(1f, 0.8f, 0.2f),
                    AnalyticsCategory.Session => new Color(0.4f, 0.8f, 0.4f),
                    AnalyticsCategory.Quest => new Color(0.8f, 0.5f, 1f),
                    AnalyticsCategory.Performance => new Color(1f, 0.5f, 0f),
                    _ => Color.gray
                };

                var markerRect = new Rect(x - 2, timelineRect.y, 4, timelineRect.height);
                EditorGUI.DrawRect(markerRect, col);

                if (markerRect.Contains(Event.current.mousePosition))
                    _hoveredIndex = i;
            }

            EditorGUILayout.EndScrollView();

            if (_hoveredIndex >= 0 && _hoveredIndex < events.Count)
            {
                var hovered = events[_hoveredIndex];
                float t = (hovered.TimestampUtcMs - firstTs) / 1000f;
                EditorGUILayout.HelpBox(
                    $"[{hovered.Category}] {hovered.Action} at {t:F2}s\n{hovered.PropertiesJson}",
                    MessageType.None);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
