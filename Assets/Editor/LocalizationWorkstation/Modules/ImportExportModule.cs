using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DIG.Localization.Editor.Modules
{
    public class ImportExportModule : ILocalizationWorkstationModule
    {
        private LocalizationDatabase _db;
        private StringTableSO _selectedTable;
        private string _lastExportPath = "";
        private string _importReport = "";

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Import / Export", EditorStyles.boldLabel);

            _db = LocalizationWorkstationWindow.LoadDatabase();
            if (_db == null)
            {
                EditorGUILayout.HelpBox("No LocalizationDatabase found.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            _selectedTable = (StringTableSO)EditorGUILayout.ObjectField(
                "String Table", _selectedTable, typeof(StringTableSO), false);

            if (_selectedTable == null && _db.StringTables.Count > 0)
                _selectedTable = _db.StringTables[0];

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export Selected Table as CSV"))
                ExportTableCSV(_selectedTable);
            if (GUILayout.Button("Export All Tables"))
                ExportAllCSV();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastExportPath))
                EditorGUILayout.LabelField($"Last export: {_lastExportPath}", EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Import", EditorStyles.boldLabel);

            if (GUILayout.Button("Import CSV into Selected Table"))
                ImportCSV();

            if (!string.IsNullOrEmpty(_importReport))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Import Report", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_importReport, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void ExportTableCSV(StringTableSO table)
        {
            if (table == null) return;

            string path = EditorUtility.SaveFilePanel(
                "Export String Table CSV", "", $"{table.TableId ?? "strings"}.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var locales = new List<string>();
            foreach (var l in _db.Locales)
                if (l != null) locales.Add(l.LocaleCode);

            // Build key -> locale -> value map
            var keyMap = new Dictionary<string, Dictionary<string, string>>();
            var keyNotes = new Dictionary<string, string>();

            foreach (var entry in table.Entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                if (!keyMap.ContainsKey(entry.Key))
                    keyMap[entry.Key] = new Dictionary<string, string>();
                keyMap[entry.Key][entry.Locale] = entry.Value ?? "";
                if (!string.IsNullOrEmpty(entry.Notes))
                    keyNotes[entry.Key] = entry.Notes;
            }

            var sb = new StringBuilder();
            sb.Append("Key");
            foreach (var locale in locales)
                sb.Append($",{locale}");
            sb.AppendLine(",Notes");

            var sortedKeys = new List<string>(keyMap.Keys);
            sortedKeys.Sort();

            foreach (var key in sortedKeys)
            {
                sb.Append(EscapeCsv(key));
                foreach (var locale in locales)
                {
                    sb.Append(',');
                    if (keyMap[key].TryGetValue(locale, out var val))
                        sb.Append(EscapeCsv(val));
                }
                sb.Append(',');
                if (keyNotes.TryGetValue(key, out var notes))
                    sb.Append(EscapeCsv(notes));
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            _lastExportPath = path;
            AssetDatabase.Refresh();
        }

        private void ExportAllCSV()
        {
            string folder = EditorUtility.SaveFolderPanel("Export All Tables", "", "");
            if (string.IsNullOrEmpty(folder)) return;

            foreach (var table in _db.StringTables)
            {
                if (table == null) continue;
                string path = Path.Combine(folder, $"{table.TableId ?? "unnamed"}.csv");

                var locales = new List<string>();
                foreach (var l in _db.Locales)
                    if (l != null) locales.Add(l.LocaleCode);

                var keyMap = new Dictionary<string, Dictionary<string, string>>();
                foreach (var entry in table.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    if (!keyMap.ContainsKey(entry.Key))
                        keyMap[entry.Key] = new Dictionary<string, string>();
                    keyMap[entry.Key][entry.Locale] = entry.Value ?? "";
                }

                var sb = new StringBuilder();
                sb.Append("Key");
                foreach (var locale in locales)
                    sb.Append($",{locale}");
                sb.AppendLine();

                var sortedKeys = new List<string>(keyMap.Keys);
                sortedKeys.Sort();

                foreach (var key in sortedKeys)
                {
                    sb.Append(EscapeCsv(key));
                    foreach (var locale in locales)
                    {
                        sb.Append(',');
                        if (keyMap[key].TryGetValue(locale, out var val))
                            sb.Append(EscapeCsv(val));
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }

            _lastExportPath = folder;
            AssetDatabase.Refresh();
        }

        private void ImportCSV()
        {
            if (_selectedTable == null)
            {
                _importReport = "No table selected.";
                return;
            }

            string path = EditorUtility.OpenFilePanel("Import CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 2)
            {
                _importReport = "CSV file has no data rows.";
                return;
            }

            var headers = ParseCsvLine(lines[0]);
            if (headers.Length < 2 || headers[0] != "Key")
            {
                _importReport = "Invalid CSV format. First column must be 'Key'.";
                return;
            }

            int added = 0, updated = 0;

            // Build existing key+locale lookup
            var existing = new Dictionary<string, int>();
            for (int i = 0; i < _selectedTable.Entries.Count; i++)
            {
                var e = _selectedTable.Entries[i];
                string lookupKey = e.Key + "|" + e.Locale;
                existing[lookupKey] = i;
            }

            for (int row = 1; row < lines.Length; row++)
            {
                var cols = ParseCsvLine(lines[row]);
                if (cols.Length < 2) continue;

                string key = cols[0];
                if (string.IsNullOrEmpty(key)) continue;

                for (int col = 1; col < headers.Length && col < cols.Length; col++)
                {
                    string locale = headers[col];
                    if (locale == "Notes") continue;
                    string value = cols[col];

                    string lookupKey = key + "|" + locale;
                    if (existing.TryGetValue(lookupKey, out int idx))
                    {
                        var entry = _selectedTable.Entries[idx];
                        if (entry.Value != value)
                        {
                            entry.Value = value;
                            _selectedTable.Entries[idx] = entry;
                            updated++;
                        }
                    }
                    else
                    {
                        _selectedTable.Entries.Add(new StringTableEntry
                        {
                            Key = key,
                            Locale = locale,
                            Value = value,
                            PluralForm = PluralCategory.Other,
                            Notes = ""
                        });
                        added++;
                    }
                }
            }

            EditorUtility.SetDirty(_selectedTable);
            _importReport = $"Import complete. Added: {added}, Updated: {updated}";
        }

        private static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            if (input.Contains(',') || input.Contains('"') || input.Contains('\n'))
                return "\"" + input.Replace("\"", "\"\"") + "\"";
            return input;
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}
