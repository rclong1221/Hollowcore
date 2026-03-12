using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Localization.Editor.Modules
{
    public class MissingKeyScannerModule : ILocalizationWorkstationModule
    {
        private LocalizationDatabase _db;
        private Vector2 _scroll;
        private string _scanLocale = "en-US";
        private List<string> _missingKeys = new();
        private List<string> _orphanedKeys = new();
        private bool _hasScanned;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Missing Key Scanner", EditorStyles.boldLabel);

            _db = LocalizationWorkstationWindow.LoadDatabase();
            if (_db == null)
            {
                EditorGUILayout.HelpBox("No LocalizationDatabase found.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            var localeOptions = new List<string>();
            foreach (var l in _db.Locales)
                if (l != null) localeOptions.Add(l.LocaleCode);

            if (localeOptions.Count == 0)
            {
                EditorGUILayout.HelpBox("No locales defined in database.", MessageType.Info);
                return;
            }

            int selectedIdx = localeOptions.IndexOf(_scanLocale);
            if (selectedIdx < 0) selectedIdx = 0;
            selectedIdx = EditorGUILayout.Popup("Scan Locale", selectedIdx, localeOptions.ToArray());
            _scanLocale = localeOptions[selectedIdx];

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Run Scan", GUILayout.Height(28)))
                RunScan();

            if (!_hasScanned) return;

            EditorGUILayout.Space(8);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _missingKeys.Count > 0 ? new Color(0.9f, 0.4f, 0.3f) : new Color(0.3f, 0.9f, 0.3f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;
            EditorGUILayout.LabelField($"Missing Keys for {_scanLocale}: {_missingKeys.Count}", EditorStyles.boldLabel);
            foreach (var key in _missingKeys)
                EditorGUILayout.LabelField($"  {key}", EditorStyles.miniLabel);
            if (_missingKeys.Count == 0)
                EditorGUILayout.LabelField("  All keys translated!", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _orphanedKeys.Count > 0 ? new Color(0.9f, 0.7f, 0.3f) : new Color(0.3f, 0.9f, 0.3f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;
            EditorGUILayout.LabelField($"Orphaned Keys (in tables but unreferenced): {_orphanedKeys.Count}", EditorStyles.boldLabel);
            foreach (var key in _orphanedKeys)
                EditorGUILayout.LabelField($"  {key}", EditorStyles.miniLabel);
            if (_orphanedKeys.Count == 0)
                EditorGUILayout.LabelField("  No orphaned keys.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void RunScan()
        {
            _missingKeys.Clear();
            _orphanedKeys.Clear();
            _hasScanned = true;

            var allTableKeys = new HashSet<string>();
            var localeKeys = new HashSet<string>();

            foreach (var table in _db.StringTables)
            {
                if (table == null) continue;
                foreach (var entry in table.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    allTableKeys.Add(entry.Key);
                    if (entry.Locale == _scanLocale && !string.IsNullOrEmpty(entry.Value))
                        localeKeys.Add(entry.Key);
                }
            }

            // Keys that exist in other locales but not in the scan locale
            foreach (var key in allTableKeys)
            {
                if (!localeKeys.Contains(key))
                    _missingKeys.Add(key);
            }

            // Scan SOs for string-like fields to detect referenced keys
            // (simplified: report table keys not matching common naming patterns as potential orphans)
            var referencedKeys = new HashSet<string>();

            // Scan all LocalizedText components in loaded scenes
            var localizedTexts = Resources.FindObjectsOfTypeAll<LocalizedText>();
            foreach (var lt in localizedTexts)
            {
                if (!string.IsNullOrEmpty(lt.StringKey))
                    referencedKeys.Add(lt.StringKey);
            }

            // Keys in tables but not referenced by any LocalizedText (heuristic)
            if (referencedKeys.Count > 0)
            {
                foreach (var key in allTableKeys)
                {
                    if (!referencedKeys.Contains(key))
                        _orphanedKeys.Add(key);
                }
            }

            _missingKeys.Sort();
            _orphanedKeys.Sort();
        }
    }
}
