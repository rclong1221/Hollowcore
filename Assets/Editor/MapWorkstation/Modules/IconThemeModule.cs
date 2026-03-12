#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Map.Editor.Modules
{
    /// <summary>
    /// EPIC 17.6: Edit MapIconThemeSO — per-type color pickers, sprite fields, compass toggle,
    /// sort order. Preview grid showing all 13 icon types with current theme.
    /// </summary>
    public class IconThemeModule : IMapWorkstationModule
    {
        public string ModuleName => "Icon Theme";

        private MapIconThemeSO _theme;
        private UnityEditor.Editor _themeEditor;
        private bool _showPreview = true;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Map Icon Theme", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            if (_theme == null)
                _theme = Resources.Load<MapIconThemeSO>("MapIconTheme");

            if (_theme == null)
            {
                EditorGUILayout.HelpBox("No MapIconTheme found at Resources/MapIconTheme.\nUse DIG > Map Workstation > Create Default Assets.", MessageType.Warning);
                return;
            }

            if (_themeEditor == null || _themeEditor.target != _theme)
                _themeEditor = UnityEditor.Editor.CreateEditor(_theme);

            _themeEditor.OnInspectorGUI();

            EditorGUILayout.Space(12);
            _showPreview = EditorGUILayout.Foldout(_showPreview, "Icon Type Preview Grid", true);
            if (_showPreview && _theme.Entries != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.Label("Color", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label("Compass", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label("World Map", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label("Scale", EditorStyles.boldLabel, GUILayout.Width(50));
                GUILayout.Label("Sort", EditorStyles.boldLabel, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                foreach (var entry in _theme.Entries)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(entry.IconType.ToString(), GUILayout.Width(100));
                    EditorGUILayout.ColorField(GUIContent.none, entry.DefaultColor, false, true, false, GUILayout.Width(60));
                    GUILayout.Label(entry.ShowOnCompass ? "Yes" : "No", GUILayout.Width(60));
                    GUILayout.Label(entry.ShowOnWorldMap ? "Yes" : "No", GUILayout.Width(70));
                    GUILayout.Label($"{entry.ScaleMultiplier:F1}x", GUILayout.Width(50));
                    GUILayout.Label(entry.SortOrder.ToString(), GUILayout.Width(40));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
