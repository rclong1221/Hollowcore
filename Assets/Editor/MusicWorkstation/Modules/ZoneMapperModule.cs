#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace DIG.Music.Editor
{
    /// <summary>
    /// EPIC 17.5: Scene view overlay showing MusicZone volumes color-coded by track.
    /// Displays priority labels and overlap detection warnings.
    /// </summary>
    public class ZoneMapperModule : IMusicWorkstationModule
    {
        public string ModuleName => "Zone Mapper";

        private bool _showOverlay = true;
        private bool _showLabels = true;
        private bool _checkOverlaps = true;
        private List<MusicZoneAuthoring> _cachedZones = new List<MusicZoneAuthoring>();
        private float _lastCacheTime;

        private static readonly Color[] TrackColors = new Color[]
        {
            new Color(0.2f, 0.6f, 1f, 0.25f),   // Blue
            new Color(1f, 0.4f, 0.2f, 0.25f),    // Red-orange
            new Color(0.2f, 1f, 0.4f, 0.25f),    // Green
            new Color(1f, 1f, 0.2f, 0.25f),      // Yellow
            new Color(0.8f, 0.2f, 1f, 0.25f),    // Purple
            new Color(0.2f, 1f, 1f, 0.25f),      // Cyan
        };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Zone Mapper", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _showOverlay = EditorGUILayout.Toggle("Show Zone Overlay", _showOverlay);
            _showLabels = EditorGUILayout.Toggle("Show Labels", _showLabels);
            _checkOverlaps = EditorGUILayout.Toggle("Warn Overlaps", _checkOverlaps);

            EditorGUILayout.Space(8);

            // List zones in scene
            RefreshZoneCache();

            if (_cachedZones.Count == 0)
            {
                EditorGUILayout.HelpBox("No MusicZoneAuthoring components found in scene.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Zones in Scene: {_cachedZones.Count}", EditorStyles.boldLabel);
            for (int i = 0; i < _cachedZones.Count; i++)
            {
                var zone = _cachedZones[i];
                if (zone == null) continue;

                EditorGUILayout.BeginHorizontal();
                var color = TrackColors[zone.TrackId % TrackColors.Length];
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(color.r, color.g, color.b, 1f);
                EditorGUILayout.LabelField("  ", GUILayout.Width(16));
                GUI.backgroundColor = prevBg;

                EditorGUILayout.LabelField($"{zone.gameObject.name}", GUILayout.Width(150));
                EditorGUILayout.LabelField($"Track: {zone.TrackId}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"Priority: {zone.Priority}", GUILayout.Width(80));
                if (GUILayout.Button("Select", GUILayout.Width(50)))
                    Selection.activeGameObject = zone.gameObject;
                EditorGUILayout.EndHorizontal();
            }

            // Overlap warnings
            if (_checkOverlaps)
            {
                EditorGUILayout.Space(8);
                bool anyOverlap = false;
                for (int i = 0; i < _cachedZones.Count; i++)
                {
                    for (int j = i + 1; j < _cachedZones.Count; j++)
                    {
                        if (_cachedZones[i] == null || _cachedZones[j] == null) continue;
                        if (_cachedZones[i].Priority == _cachedZones[j].Priority &&
                            _cachedZones[i].TrackId != _cachedZones[j].TrackId)
                        {
                            var boundsA = GetBounds(_cachedZones[i].gameObject);
                            var boundsB = GetBounds(_cachedZones[j].gameObject);
                            if (boundsA.Intersects(boundsB))
                            {
                                EditorGUILayout.HelpBox(
                                    $"Overlap: '{_cachedZones[i].gameObject.name}' and '{_cachedZones[j].gameObject.name}' have same priority ({_cachedZones[i].Priority}) but different tracks.",
                                    MessageType.Warning);
                                anyOverlap = true;
                            }
                        }
                    }
                }
                if (!anyOverlap)
                    EditorGUILayout.LabelField("No priority conflicts detected.", EditorStyles.miniLabel);
            }

            if (_showOverlay)
                SceneView.RepaintAll();
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (!_showOverlay) return;

            RefreshZoneCache();

            for (int i = 0; i < _cachedZones.Count; i++)
            {
                var zone = _cachedZones[i];
                if (zone == null) continue;

                var color = TrackColors[zone.TrackId % TrackColors.Length];
                var bounds = GetBounds(zone.gameObject);

                // Draw filled wireframe box
                Handles.color = color;
                Handles.DrawWireCube(bounds.center, bounds.size);
                Handles.color = new Color(color.r, color.g, color.b, 0.1f);
                Handles.CubeHandleCap(0, bounds.center, Quaternion.identity, bounds.size.magnitude * 0.01f, EventType.Repaint);

                // Label
                if (_showLabels)
                {
                    Handles.Label(bounds.center + Vector3.up * bounds.extents.y,
                        $"Track:{zone.TrackId} P:{zone.Priority}",
                        EditorStyles.boldLabel);
                }
            }
        }

        private void RefreshZoneCache()
        {
            if (Time.realtimeSinceStartup - _lastCacheTime < 1f) return;
            _lastCacheTime = Time.realtimeSinceStartup;
            _cachedZones.Clear();
            _cachedZones.AddRange(Object.FindObjectsByType<MusicZoneAuthoring>(FindObjectsSortMode.None));
        }

        private Bounds GetBounds(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) return col.bounds;
            return new Bounds(go.transform.position, Vector3.one * 5f);
        }
    }
}
#endif
