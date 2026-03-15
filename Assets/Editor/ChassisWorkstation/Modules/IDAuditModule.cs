using System.Collections.Generic;
using System.Linq;
using Hollowcore.Chassis.Definitions;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Editor.ChassisWorkstation.Modules
{
    /// <summary>
    /// Lists all LimbId values, flags duplicates and gaps in the ID sequence.
    /// </summary>
    public class IDAuditModule : IChassisModule
    {
        private List<(int id, string name, string path)> _entries = new();
        private List<string> _issues = new();
        private float _lastRefresh;

        public void OnGUI()
        {
            RefreshIfNeeded();

            EditorGUILayout.LabelField("ID Audit", EditorStyles.boldLabel);

            // Issues
            if (_issues.Count > 0)
            {
                foreach (var issue in _issues)
                    EditorGUILayout.HelpBox(issue, MessageType.Warning);
                EditorGUILayout.Space(4);
            }
            else
            {
                EditorGUILayout.HelpBox("No issues found.", MessageType.Info);
            }

            // ID table
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("ID", EditorStyles.toolbarButton, GUILayout.Width(50));
            EditorGUILayout.LabelField("Name", EditorStyles.toolbarButton, GUILayout.Width(200));
            EditorGUILayout.LabelField("Path", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            foreach (var (id, entryName, path) in _entries)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(id.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(entryName, GUILayout.Width(200));
                if (GUILayout.Button(path, EditorStyles.linkLabel))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<LimbDefinitionSO>(path);
                    if (obj != null) Selection.activeObject = obj;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                _lastRefresh = 0;
                RefreshIfNeeded();
            }
        }

        private void RefreshIfNeeded()
        {
            if (Time.realtimeSinceStartup - _lastRefresh > 5f || _entries.Count == 0)
            {
                _lastRefresh = Time.realtimeSinceStartup;
                _entries.Clear();
                _issues.Clear();

                var guids = AssetDatabase.FindAssets("t:LimbDefinitionSO");
                var idToEntries = new Dictionary<int, List<string>>();

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var def = AssetDatabase.LoadAssetAtPath<LimbDefinitionSO>(path);
                    if (def == null) continue;

                    _entries.Add((def.LimbId, def.DisplayName ?? def.name, path));

                    if (!idToEntries.ContainsKey(def.LimbId))
                        idToEntries[def.LimbId] = new List<string>();
                    idToEntries[def.LimbId].Add(def.name);
                }

                _entries = _entries.OrderBy(e => e.id).ToList();

                // Check duplicates
                foreach (var (id, names) in idToEntries)
                {
                    if (names.Count > 1)
                        _issues.Add($"Duplicate ID {id}: {string.Join(", ", names)}");
                }

                // Check gaps
                if (_entries.Count > 0)
                {
                    var ids = _entries.Select(e => e.id).Distinct().OrderBy(i => i).ToList();
                    for (int i = 1; i < ids.Count; i++)
                    {
                        if (ids[i] - ids[i - 1] > 1)
                            _issues.Add($"ID gap: {ids[i - 1]} to {ids[i]}");
                    }
                }
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
        public void OnEntityChanged(Entity entity, EntityManager entityManager) { }
    }
}
