using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Analytics.Editor.Modules
{
    /// <summary>
    /// Bar chart showing event counts per category over configurable time windows.
    /// </summary>
    public class EventFrequencyModule : IAnalyticsWorkstationModule
    {
        private int _timeWindowIndex;
        private readonly string[] _timeWindowLabels = { "1 min", "5 min", "15 min", "Session" };
        private readonly float[] _timeWindowSeconds = { 60f, 300f, 900f, float.MaxValue };
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Event Frequency", EditorStyles.boldLabel);

            if (!Application.isPlaying || !AnalyticsAPI.IsInitialized)
            {
                EditorGUILayout.HelpBox("Enter Play Mode with analytics initialized.", MessageType.Info);
                return;
            }

            _timeWindowIndex = GUILayout.Toolbar(_timeWindowIndex, _timeWindowLabels);
            EditorGUILayout.Space(8);

            var events = AnalyticsAPI.RecentEvents;
            if (events.Count == 0)
            {
                EditorGUILayout.HelpBox("No events recorded yet.", MessageType.Info);
                return;
            }

            long nowMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            float windowSec = _timeWindowSeconds[_timeWindowIndex];
            long cutoffMs = windowSec < float.MaxValue
                ? nowMs - (long)(windowSec * 1000)
                : 0;

            var counts = new Dictionary<AnalyticsCategory, int>();
            int total = 0;
            foreach (var evt in events)
            {
                if (evt.TimestampUtcMs < cutoffMs) continue;
                counts.TryGetValue(evt.Category, out int c);
                counts[evt.Category] = c + 1;
                total++;
            }

            EditorGUILayout.LabelField($"Total events in window: {total}");
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int maxCount = 1;
            foreach (var kv in counts)
                if (kv.Value > maxCount) maxCount = kv.Value;

            foreach (var kv in counts)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kv.Key.ToString(), GUILayout.Width(120));

                float ratio = (float)kv.Value / maxCount;
                Rect barRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

                var fillRect = new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height);
                Color barColor = kv.Key switch
                {
                    AnalyticsCategory.Combat => new Color(0.9f, 0.3f, 0.3f),
                    AnalyticsCategory.Economy => new Color(1f, 0.8f, 0.2f),
                    AnalyticsCategory.Progression => new Color(0.3f, 0.6f, 1f),
                    AnalyticsCategory.Session => new Color(0.4f, 0.8f, 0.4f),
                    AnalyticsCategory.Quest => new Color(0.8f, 0.5f, 1f),
                    AnalyticsCategory.Performance => new Color(1f, 0.5f, 0f),
                    _ => Color.gray
                };
                EditorGUI.DrawRect(fillRect, barColor);

                EditorGUILayout.LabelField(kv.Value.ToString(), GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
