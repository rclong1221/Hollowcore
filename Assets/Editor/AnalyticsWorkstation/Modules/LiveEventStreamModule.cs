using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Analytics.Editor.Modules
{
    /// <summary>
    /// Play-mode scrolling list of real-time analytics events.
    /// Category color coding, filtering, search, copy JSON.
    /// </summary>
    public class LiveEventStreamModule : IAnalyticsWorkstationModule
    {
        private Vector2 _scroll;
        private AnalyticsCategory _filterMask = AnalyticsCategory.All;
        private string _searchAction = "";
        private bool _autoScroll = true;
        private bool _paused;

        private static readonly Dictionary<AnalyticsCategory, Color> CategoryColors = new()
        {
            { AnalyticsCategory.Session, new Color(0.4f, 0.8f, 0.4f) },
            { AnalyticsCategory.Combat, new Color(0.9f, 0.3f, 0.3f) },
            { AnalyticsCategory.Economy, new Color(1f, 0.8f, 0.2f) },
            { AnalyticsCategory.Progression, new Color(0.3f, 0.6f, 1f) },
            { AnalyticsCategory.Quest, new Color(0.8f, 0.5f, 1f) },
            { AnalyticsCategory.Crafting, new Color(0.6f, 0.4f, 0.2f) },
            { AnalyticsCategory.Performance, new Color(1f, 0.5f, 0f) },
            { AnalyticsCategory.Custom, Color.gray }
        };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Live Event Stream", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view live analytics events.", MessageType.Info);
                return;
            }

            if (!AnalyticsAPI.IsInitialized)
            {
                EditorGUILayout.HelpBox("AnalyticsAPI is not initialized. Check AnalyticsProfile in Resources.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            _filterMask = (AnalyticsCategory)EditorGUILayout.EnumFlagsField("Filter", _filterMask);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _searchAction = EditorGUILayout.TextField("Search Action", _searchAction);
            _autoScroll = EditorGUILayout.ToggleLeft("Auto-scroll", _autoScroll, GUILayout.Width(90));
            _paused = EditorGUILayout.ToggleLeft("Pause", _paused, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            var events = AnalyticsAPI.RecentEvents;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];

                if ((_filterMask & evt.Category) == 0) continue;
                if (!string.IsNullOrEmpty(_searchAction) &&
                    !evt.Action.ToString().Contains(_searchAction, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                Color col = CategoryColors.TryGetValue(evt.Category, out var c) ? c : Color.white;
                var prevColor = GUI.contentColor;
                GUI.contentColor = col;

                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"[{evt.Category}]", GUILayout.Width(100));
                EditorGUILayout.LabelField(evt.Action.ToString(), GUILayout.Width(140));
                EditorGUILayout.LabelField(evt.PropertiesJson.ToString(), EditorStyles.miniLabel);

                if (GUILayout.Button("Copy", GUILayout.Width(45)))
                {
                    string json = $"{{\"cat\":\"{evt.Category}\",\"act\":\"{evt.Action}\",\"props\":{evt.PropertiesJson}}}";
                    EditorGUIUtility.systemCopyBuffer = json;
                }

                EditorGUILayout.EndHorizontal();
                GUI.contentColor = prevColor;
            }

            EditorGUILayout.EndScrollView();

            if (_autoScroll && !_paused)
                _scroll.y = float.MaxValue;
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
