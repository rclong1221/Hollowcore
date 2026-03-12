#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace DIG.Cinematic.Editor.Modules
{
    /// <summary>
    /// EPIC 17.9: Lists all CinematicDefinitionSO assets with type, skip policy,
    /// duration, dialogue tree ID. Filter by type. Select to inspect in Inspector.
    /// </summary>
    public class CinematicBrowserModule : ICinematicWorkstationModule
    {
        public string ModuleName => "Browser";

        private CinematicDefinitionSO[] _definitions;
        private CinematicDatabaseSO _database;
        private CinematicType _filterType = CinematicType.FullCinematic;
        private bool _useFilter;
        private Vector2 _listScroll;
        private int _selectedIndex = -1;
        private bool _needsRefresh = true;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Cinematic Browser", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Toolbar
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                _needsRefresh = true;

            _useFilter = EditorGUILayout.ToggleLeft("Filter by Type", _useFilter, GUILayout.Width(120));
            if (_useFilter)
                _filterType = (CinematicType)EditorGUILayout.EnumPopup(_filterType, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (_needsRefresh)
            {
                RefreshList();
                _needsRefresh = false;
            }

            // Database info
            if (_database != null)
            {
                EditorGUILayout.HelpBox(
                    $"Database: {_database.name} | {_database.Cinematics.Count} cinematics | " +
                    $"Default Skip: {_database.DefaultSkipPolicy} | Blend: {_database.BlendInDuration:F1}s/{_database.BlendOutDuration:F1}s",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("No CinematicDatabaseSO found at Resources/CinematicDatabase", MessageType.Warning);
            }

            EditorGUILayout.Space(4);

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("ID", EditorStyles.miniLabel, GUILayout.Width(40));
            GUILayout.Label("Name", EditorStyles.miniLabel, GUILayout.Width(180));
            GUILayout.Label("Type", EditorStyles.miniLabel, GUILayout.Width(100));
            GUILayout.Label("Skip", EditorStyles.miniLabel, GUILayout.Width(100));
            GUILayout.Label("Duration", EditorStyles.miniLabel, GUILayout.Width(60));
            GUILayout.Label("Dialogue", EditorStyles.miniLabel, GUILayout.Width(60));
            GUILayout.Label("Timeline", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // List
            if (_definitions == null || _definitions.Length == 0)
            {
                EditorGUILayout.LabelField("No cinematic definitions found.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            for (int i = 0; i < _definitions.Length; i++)
            {
                var def = _definitions[i];
                if (def == null) continue;
                if (_useFilter && def.CinematicType != _filterType) continue;

                bool isSelected = i == _selectedIndex;
                var bgColor = isSelected ? new Color(0.24f, 0.48f, 0.9f, 0.4f) : Color.clear;

                var rowRect = EditorGUILayout.BeginHorizontal();
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rowRect, bgColor);

                GUILayout.Label(def.CinematicId.ToString(), GUILayout.Width(40));
                GUILayout.Label(def.Name, GUILayout.Width(180));
                GUILayout.Label(def.CinematicType.ToString(), GUILayout.Width(100));
                GUILayout.Label(def.SkipPolicy.ToString(), GUILayout.Width(100));
                GUILayout.Label($"{def.Duration:F1}s", GUILayout.Width(60));
                GUILayout.Label(def.DialogueTreeId > 0 ? def.DialogueTreeId.ToString() : "-", GUILayout.Width(60));
                GUILayout.Label(def.TimelineAsset != null ? "Yes" : "No", GUILayout.Width(60));

                EditorGUILayout.EndHorizontal();

                // Click to select
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = i;
                    Selection.activeObject = def;
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void RefreshList()
        {
            _database = Resources.Load<CinematicDatabaseSO>("CinematicDatabase");

            var guids = AssetDatabase.FindAssets("t:CinematicDefinitionSO");
            _definitions = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<CinematicDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null)
                .OrderBy(d => d.CinematicId)
                .ToArray();
        }
    }
}
#endif
