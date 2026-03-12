#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace DIG.Map.Editor.Modules
{
    /// <summary>
    /// EPIC 17.6: List all POIRegistrySO entries. Add/remove/edit POIs.
    /// "Select in Scene" button. Duplicate ID validator. Discovery radius visualizer.
    /// </summary>
    public class POIManagerModule : IMapWorkstationModule
    {
        public string ModuleName => "POI Manager";

        private POIRegistrySO _registry;
        private UnityEditor.Editor _registryEditor;
        private bool _showValidator = true;
        private Vector2 _listScroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Point of Interest Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            if (_registry == null)
                _registry = Resources.Load<POIRegistrySO>("POIRegistry");

            if (_registry == null)
            {
                EditorGUILayout.HelpBox("No POIRegistry found at Resources/POIRegistry.\nUse DIG > Map Workstation > Create Default Assets.", MessageType.Warning);
                return;
            }

            // Summary
            int count = _registry.POIs != null ? _registry.POIs.Length : 0;
            EditorGUILayout.LabelField($"Registered POIs: {count}");
            EditorGUILayout.LabelField($"Auto-Discover Radius: {_registry.AutoDiscoverRadius:F0}m");
            EditorGUILayout.LabelField($"Discovery XP Reward: {_registry.DiscoverXPReward:F0}");
            EditorGUILayout.Space(8);

            // POI list
            if (_registry.POIs != null && _registry.POIs.Length > 0)
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.MaxHeight(300));

                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("ID", GUILayout.Width(40));
                GUILayout.Label("Label", GUILayout.Width(120));
                GUILayout.Label("Type", GUILayout.Width(80));
                GUILayout.Label("Position", GUILayout.Width(160));
                GUILayout.Label("FastTravel", GUILayout.Width(70));
                GUILayout.Label("Discovery", GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();

                foreach (var poi in _registry.POIs)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(poi.POIId.ToString(), GUILayout.Width(40));
                    GUILayout.Label(poi.Label ?? "(none)", GUILayout.Width(120));
                    GUILayout.Label(poi.Type.ToString(), GUILayout.Width(80));
                    GUILayout.Label($"({poi.WorldPosition.x:F0}, {poi.WorldPosition.y:F0}, {poi.WorldPosition.z:F0})", GUILayout.Width(160));
                    GUILayout.Label(poi.IsFastTravel ? "Yes" : "No", GUILayout.Width(70));
                    GUILayout.Label(poi.RequiresDiscovery ? "Yes" : "No", GUILayout.Width(70));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(8);

            // Duplicate ID validator
            _showValidator = EditorGUILayout.Foldout(_showValidator, "Validation", true);
            if (_showValidator)
            {
                var duplicates = FindDuplicateIds();
                if (duplicates.Count > 0)
                {
                    EditorGUILayout.HelpBox($"Duplicate POI IDs found: {string.Join(", ", duplicates)}", MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox("No duplicate POI IDs. All entries valid.", MessageType.Info);
                }

                // Check for empty labels
                bool hasEmptyLabels = false;
                if (_registry.POIs != null)
                {
                    foreach (var poi in _registry.POIs)
                    {
                        if (string.IsNullOrWhiteSpace(poi.Label))
                        {
                            hasEmptyLabels = true;
                            break;
                        }
                    }
                }
                if (hasEmptyLabels)
                    EditorGUILayout.HelpBox("Some POIs have empty labels.", MessageType.Warning);
            }

            EditorGUILayout.Space(8);

            // Full inspector for editing
            if (_registryEditor == null || _registryEditor.target != _registry)
                _registryEditor = UnityEditor.Editor.CreateEditor(_registry);

            EditorGUILayout.LabelField("Full Editor", EditorStyles.boldLabel);
            _registryEditor.OnInspectorGUI();
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (_registry == null || _registry.POIs == null) return;

            Handles.color = new Color(0, 1, 1, 0.3f);
            foreach (var poi in _registry.POIs)
            {
                // Draw discovery radius sphere
                Handles.DrawWireDisc(poi.WorldPosition, Vector3.up, _registry.AutoDiscoverRadius);
                Handles.Label(poi.WorldPosition + Vector3.up * 2f, $"{poi.Label} (ID:{poi.POIId})");
            }
        }

        private List<int> FindDuplicateIds()
        {
            var seen = new HashSet<int>();
            var duplicates = new List<int>();

            if (_registry.POIs == null) return duplicates;

            foreach (var poi in _registry.POIs)
            {
                if (!seen.Add(poi.POIId))
                    duplicates.Add(poi.POIId);
            }
            return duplicates;
        }
    }
}
#endif
