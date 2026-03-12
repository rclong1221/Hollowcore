using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Localization.Editor.Modules
{
    public class StringBrowserModule : ILocalizationWorkstationModule
    {
        private LocalizationDatabase _db;
        private string _searchFilter = "";
        private string _tableFilter = "";
        private string _localeFilter = "";
        private Vector2 _scroll;
        private int _editingIndex = -1;
        private string _editValue = "";

        public void OnGUI()
        {
            EditorGUILayout.LabelField("String Browser", EditorStyles.boldLabel);

            _db = LocalizationWorkstationWindow.LoadDatabase();
            if (_db == null)
            {
                EditorGUILayout.HelpBox(
                    "No LocalizationDatabase found. Create one via Create → DIG → Localization → Localization Database " +
                    "and place it in a Resources folder.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
                _searchFilter = "";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawTableFilterDropdown();
            DrawLocaleFilterDropdown();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Table", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Key", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField("Locale", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Value", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            int globalIdx = 0;
            foreach (var table in _db.StringTables)
            {
                if (table == null) continue;
                if (!string.IsNullOrEmpty(_tableFilter) && table.TableId != _tableFilter) continue;

                for (int i = 0; i < table.Entries.Count; i++)
                {
                    var entry = table.Entries[i];
                    if (!string.IsNullOrEmpty(_localeFilter) && entry.Locale != _localeFilter) continue;

                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        bool match = (entry.Key != null && entry.Key.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase)) ||
                                     (entry.Value != null && entry.Value.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase));
                        if (!match) continue;
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(table.TableId ?? "", GUILayout.Width(80));
                    EditorGUILayout.LabelField(entry.Key ?? "", GUILayout.Width(200));
                    EditorGUILayout.LabelField(entry.Locale ?? "", GUILayout.Width(60));

                    if (_editingIndex == globalIdx)
                    {
                        _editValue = EditorGUILayout.TextField(_editValue);
                        if (GUILayout.Button("Save", GUILayout.Width(50)))
                        {
                            var e = table.Entries[i];
                            e.Value = _editValue;
                            table.Entries[i] = e;
                            EditorUtility.SetDirty(table);
                            _editingIndex = -1;
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField(entry.Value ?? "");
                        if (GUILayout.Button("Edit", GUILayout.Width(50)))
                        {
                            _editingIndex = globalIdx;
                            _editValue = entry.Value ?? "";
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    globalIdx++;
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total entries displayed: {globalIdx}", EditorStyles.miniLabel);
            if (GUILayout.Button("Add Entry...", GUILayout.Width(100)))
                AddEntryPopup();
            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawTableFilterDropdown()
        {
            var tables = new List<string> { "(All Tables)" };
            if (_db != null)
            {
                foreach (var t in _db.StringTables)
                    if (t != null && !tables.Contains(t.TableId))
                        tables.Add(t.TableId);
            }

            int selected = string.IsNullOrEmpty(_tableFilter) ? 0 : tables.IndexOf(_tableFilter);
            if (selected < 0) selected = 0;
            int newSelected = EditorGUILayout.Popup("Table", selected, tables.ToArray());
            _tableFilter = newSelected == 0 ? "" : tables[newSelected];
        }

        private void DrawLocaleFilterDropdown()
        {
            var locales = new List<string> { "(All Locales)" };
            if (_db != null)
            {
                foreach (var l in _db.Locales)
                    if (l != null && !locales.Contains(l.LocaleCode))
                        locales.Add(l.LocaleCode);
            }

            int selected = string.IsNullOrEmpty(_localeFilter) ? 0 : locales.IndexOf(_localeFilter);
            if (selected < 0) selected = 0;
            int newSelected = EditorGUILayout.Popup("Locale", selected, locales.ToArray());
            _localeFilter = newSelected == 0 ? "" : locales[newSelected];
        }

        private void AddEntryPopup()
        {
            if (_db == null || _db.StringTables.Count == 0)
            {
                EditorUtility.DisplayDialog("No Tables", "Create a StringTableSO first.", "OK");
                return;
            }

            var table = _db.StringTables[0];
            if (!string.IsNullOrEmpty(_tableFilter))
            {
                foreach (var t in _db.StringTables)
                    if (t != null && t.TableId == _tableFilter) { table = t; break; }
            }

            string defaultLocale = _db.DefaultLocaleCode;
            table.Entries.Add(new StringTableEntry
            {
                Key = "new_key_" + table.Entries.Count,
                Locale = defaultLocale,
                Value = "",
                PluralForm = PluralCategory.Other,
                Notes = ""
            });
            EditorUtility.SetDirty(table);
        }
    }
}
