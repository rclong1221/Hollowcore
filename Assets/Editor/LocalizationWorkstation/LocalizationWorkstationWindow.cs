using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Localization.Editor
{
    public class LocalizationWorkstationWindow : EditorWindow
    {
        private readonly string[] _tabNames =
        {
            "String Browser", "Coverage Heatmap", "Missing Key Scanner",
            "Font Preview", "Import / Export", "Pseudo-Loc"
        };

        private readonly List<ILocalizationWorkstationModule> _modules = new();
        private int _selectedTab;
        private Vector2 _sidebarScroll;

        [MenuItem("DIG/Localization Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalizationWorkstationWindow>("Localization Workstation");
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            _modules.Clear();
            _modules.Add(new Modules.StringBrowserModule());
            _modules.Add(new Modules.CoverageHeatmapModule());
            _modules.Add(new Modules.MissingKeyScannerModule());
            _modules.Add(new Modules.FontPreviewModule());
            _modules.Add(new Modules.ImportExportModule());
            _modules.Add(new Modules.PseudoLocModule());
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical("box", GUILayout.Width(170));
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);
            for (int i = 0; i < _tabNames.Length; i++)
            {
                var prevBg = GUI.backgroundColor;
                if (i == _selectedTab)
                    GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
                if (GUILayout.Button(_tabNames[i], GUILayout.Height(28)))
                    _selectedTab = i;
                GUI.backgroundColor = prevBg;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            if (_selectedTab >= 0 && _selectedTab < _modules.Count)
                _modules[_selectedTab].OnGUI();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        public static LocalizationDatabase LoadDatabase()
        {
            var db = Resources.Load<LocalizationDatabase>("LocalizationDatabase");
            if (db == null)
            {
                var guids = AssetDatabase.FindAssets("t:LocalizationDatabase");
                if (guids.Length > 0)
                    db = AssetDatabase.LoadAssetAtPath<LocalizationDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return db;
        }
    }
}
