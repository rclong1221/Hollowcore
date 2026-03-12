using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Localization.Editor.Modules
{
    public class CoverageHeatmapModule : ILocalizationWorkstationModule
    {
        private LocalizationDatabase _db;
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Coverage Heatmap", EditorStyles.boldLabel);

            _db = LocalizationWorkstationWindow.LoadDatabase();
            if (_db == null)
            {
                EditorGUILayout.HelpBox("No LocalizationDatabase found.", MessageType.Info);
                return;
            }

            if (_db.Locales.Count == 0 || _db.StringTables.Count == 0)
            {
                EditorGUILayout.HelpBox("Add locales and string tables to see coverage.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Table", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Keys", EditorStyles.boldLabel, GUILayout.Width(50));
            foreach (var locale in _db.Locales)
            {
                if (locale == null) continue;
                EditorGUILayout.LabelField(locale.LocaleCode, EditorStyles.boldLabel, GUILayout.Width(70));
            }
            EditorGUILayout.EndHorizontal();

            foreach (var table in _db.StringTables)
            {
                if (table == null) continue;

                var allKeys = new HashSet<string>();
                var perLocale = new Dictionary<string, int>();

                foreach (var locale in _db.Locales)
                    if (locale != null) perLocale[locale.LocaleCode] = 0;

                foreach (var entry in table.Entries)
                {
                    allKeys.Add(entry.Key);
                    if (!string.IsNullOrEmpty(entry.Value) && perLocale.ContainsKey(entry.Locale))
                        perLocale[entry.Locale]++;
                }

                int totalKeys = allKeys.Count;
                if (totalKeys == 0) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(table.TableId ?? "(unnamed)", GUILayout.Width(120));
                EditorGUILayout.LabelField(totalKeys.ToString(), GUILayout.Width(50));

                foreach (var locale in _db.Locales)
                {
                    if (locale == null) continue;
                    int count = perLocale.GetValueOrDefault(locale.LocaleCode, 0);
                    int pct = (int)(count / (float)totalKeys * 100f);

                    var prevBg = GUI.backgroundColor;
                    if (pct >= 100) GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f);
                    else if (pct >= 50) GUI.backgroundColor = new Color(0.9f, 0.9f, 0.3f);
                    else GUI.backgroundColor = new Color(0.9f, 0.4f, 0.3f);

                    EditorGUILayout.LabelField($"{pct}%", GUILayout.Width(70));
                    GUI.backgroundColor = prevBg;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Overall Coverage", EditorStyles.boldLabel);
            foreach (var locale in _db.Locales)
            {
                if (locale == null) continue;
                int pct = ComputeOverallCoverage(locale.LocaleCode);
                var bar = new Rect();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{locale.LocaleCode} ({locale.DisplayName})", GUILayout.Width(200));
                bar = GUILayoutUtility.GetRect(200, 18, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(bar, pct / 100f, $"{pct}%");
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private int ComputeOverallCoverage(string localeCode)
        {
            var allKeys = new HashSet<string>();
            var localeKeys = new HashSet<string>();

            foreach (var table in _db.StringTables)
            {
                if (table == null) continue;
                foreach (var entry in table.Entries)
                {
                    allKeys.Add(entry.Key);
                    if (entry.Locale == localeCode && !string.IsNullOrEmpty(entry.Value))
                        localeKeys.Add(entry.Key);
                }
            }

            if (allKeys.Count == 0) return 100;
            return (int)(localeKeys.Count / (float)allKeys.Count * 100f);
        }
    }
}
