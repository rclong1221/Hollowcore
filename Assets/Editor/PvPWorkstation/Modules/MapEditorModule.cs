#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.PvP.Editor.Modules
{
    /// <summary>
    /// EPIC 17.10: Visual spawn point placement, capture zone preview,
    /// team color coding, and map validation.
    /// </summary>
    public class MapEditorModule : IPvPWorkstationModule
    {
        public string ModuleName => "Map Editor";
        private PvPMapDefinitionSO _selectedMap;
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Map Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedMap = (PvPMapDefinitionSO)EditorGUILayout.ObjectField(
                "Map Definition", _selectedMap, typeof(PvPMapDefinitionSO), false);

            if (_selectedMap == null)
            {
                EditorGUILayout.HelpBox("Select a PvPMapDefinitionSO to edit.", MessageType.Info);

                // List all maps in project
                var guids = AssetDatabase.FindAssets("t:PvPMapDefinitionSO");
                if (guids.Length > 0)
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField($"Available Maps ({guids.Length})", EditorStyles.boldLabel);
                    for (int i = 0; i < guids.Length; i++)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                        var map = AssetDatabase.LoadAssetAtPath<PvPMapDefinitionSO>(path);
                        if (map != null && GUILayout.Button($"{map.MapName} (ID: {map.MapId})", GUILayout.Height(22)))
                            _selectedMap = map;
                    }
                }
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Map Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Name", _selectedMap.MapName);
            EditorGUILayout.LabelField("Map ID", _selectedMap.MapId.ToString());
            EditorGUILayout.LabelField("Max Players", _selectedMap.MaxPlayers.ToString());
            EditorGUILayout.LabelField("Team Count", _selectedMap.TeamCount.ToString());

            // Validation
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            int spawnCount = _selectedMap.SpawnPoints != null ? _selectedMap.SpawnPoints.Length : 0;
            bool spawnValid = spawnCount >= _selectedMap.MaxPlayers;
            EditorGUILayout.LabelField("Spawn Points",
                $"{spawnCount} / {_selectedMap.MaxPlayers} required {(spawnValid ? "(OK)" : "(INSUFFICIENT)")}");

            if (_selectedMap.SupportedModes != null)
            {
                EditorGUILayout.LabelField("Supported Modes", string.Join(", ",
                    System.Array.ConvertAll(_selectedMap.SupportedModes, m => m.ToString())));
            }

            bool hasCaptureMode = false;
            if (_selectedMap.SupportedModes != null)
            {
                for (int i = 0; i < _selectedMap.SupportedModes.Length; i++)
                    if (_selectedMap.SupportedModes[i] == PvPGameMode.CapturePoint)
                        hasCaptureMode = true;
            }

            if (hasCaptureMode)
            {
                int zoneCount = _selectedMap.CaptureZones != null ? _selectedMap.CaptureZones.Length : 0;
                EditorGUILayout.LabelField("Capture Zones", $"{zoneCount} {(zoneCount >= 3 ? "(OK)" : "(Need 3+)")}");
            }

            // Spawn points list
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Spawn Points", EditorStyles.boldLabel);
            if (_selectedMap.SpawnPoints != null)
            {
                for (int i = 0; i < _selectedMap.SpawnPoints.Length; i++)
                {
                    var sp = _selectedMap.SpawnPoints[i];
                    Color teamColor = GetTeamColor(sp.TeamId);
                    GUI.color = teamColor;
                    EditorGUILayout.LabelField($"  [{sp.SpawnIndex}] Team {sp.TeamId}: {sp.Position}");
                    GUI.color = Color.white;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (_selectedMap == null || _selectedMap.SpawnPoints == null) return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < _selectedMap.SpawnPoints.Length; i++)
            {
                var sp = _selectedMap.SpawnPoints[i];
                Color c = GetTeamColor(sp.TeamId);
                Handles.color = c;
                Handles.SphereHandleCap(0, sp.Position, Quaternion.identity, 1f, EventType.Repaint);
                Handles.Label(sp.Position + Vector3.up * 1.5f, $"Spawn T{sp.TeamId}[{sp.SpawnIndex}]");
            }

            if (_selectedMap.CaptureZones != null)
            {
                for (int i = 0; i < _selectedMap.CaptureZones.Length; i++)
                {
                    var cz = _selectedMap.CaptureZones[i];
                    Handles.color = new Color(1f, 0.8f, 0f, 0.3f);
                    Handles.DrawWireDisc(cz.Position, Vector3.up, cz.Radius);
                    Handles.Label(cz.Position + Vector3.up * 2f, $"Zone {cz.ZoneId}");
                }
            }
        }

        private static Color GetTeamColor(byte teamId)
        {
            return teamId switch
            {
                1 => new Color(1f, 0.3f, 0.3f),
                2 => new Color(0.3f, 0.3f, 1f),
                3 => new Color(0.3f, 1f, 0.3f),
                4 => new Color(1f, 1f, 0.3f),
                _ => Color.white
            };
        }
    }
}
#endif
