#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DIG.Items.Definitions;

namespace DIG.Items.Editor
{
    /// <summary>
    /// EPIC 16.6: Custom editor for ItemDatabaseSO with search, filter, and validation.
    /// </summary>
    [CustomEditor(typeof(ItemDatabaseSO))]
    public class ItemDatabaseSOEditor : UnityEditor.Editor
    {
        private string _searchFilter = "";
        private ItemCategory _categoryFilter = ItemCategory.None;
        private bool _filterByCategory;
        private bool _showValidation;
        private string _validationResult;

        public override void OnInspectorGUI()
        {
            var database = (ItemDatabaseSO)target;

            EditorGUILayout.LabelField($"Item Database ({database.Entries.Count} entries)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Search
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            EditorGUILayout.EndHorizontal();

            // Category filter
            EditorGUILayout.BeginHorizontal();
            _filterByCategory = EditorGUILayout.Toggle("Filter by Category:", _filterByCategory, GUILayout.Width(130));
            if (_filterByCategory)
                _categoryFilter = (ItemCategory)EditorGUILayout.EnumPopup(_categoryFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Filtered list
            int displayed = 0;
            foreach (var entry in database.Entries)
            {
                if (entry == null) continue;

                // Apply search filter
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    if (!entry.DisplayName.ToLower().Contains(_searchFilter.ToLower()) &&
                        !entry.ItemTypeId.ToString().Contains(_searchFilter))
                        continue;
                }

                // Apply category filter
                if (_filterByCategory && entry.Category != _categoryFilter)
                    continue;

                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"[{entry.ItemTypeId}]", GUILayout.Width(50));
                EditorGUILayout.LabelField(entry.DisplayName, GUILayout.Width(150));
                EditorGUILayout.LabelField(entry.Category.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField(entry.Rarity.ToString(), GUILayout.Width(80));

                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = entry;
                    EditorGUIUtility.PingObject(entry);
                }
                EditorGUILayout.EndHorizontal();

                displayed++;
            }

            EditorGUILayout.LabelField($"Showing {displayed} / {database.Entries.Count} items");

            EditorGUILayout.Space(10);

            // Validation
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation");
            if (_showValidation)
            {
                if (GUILayout.Button("Run Validation"))
                {
                    RunValidation(database);
                }

                if (!string.IsNullOrEmpty(_validationResult))
                {
                    EditorGUILayout.HelpBox(_validationResult, MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            // Default inspector for the entries list
            DrawDefaultInspector();
        }

        private void RunValidation(ItemDatabaseSO database)
        {
            var sb = new System.Text.StringBuilder();
            var seen = new System.Collections.Generic.HashSet<int>();
            int issues = 0;

            foreach (var entry in database.Entries)
            {
                if (entry == null)
                {
                    sb.AppendLine("NULL entry in database");
                    issues++;
                    continue;
                }

                if (!seen.Add(entry.ItemTypeId))
                {
                    sb.AppendLine($"DUPLICATE ID {entry.ItemTypeId}: {entry.DisplayName}");
                    issues++;
                }

                if (entry.WorldPrefab == null)
                {
                    sb.AppendLine($"MISSING WorldPrefab: {entry.DisplayName} (ID:{entry.ItemTypeId})");
                    issues++;
                }

                if (string.IsNullOrEmpty(entry.DisplayName))
                {
                    sb.AppendLine($"EMPTY DisplayName: ID {entry.ItemTypeId}");
                    issues++;
                }

                if (entry.MaxStack < 1)
                {
                    sb.AppendLine($"INVALID MaxStack ({entry.MaxStack}): {entry.DisplayName}");
                    issues++;
                }
            }

            if (issues == 0)
                _validationResult = "All items valid. No issues found.";
            else
                _validationResult = $"{issues} issue(s) found:\n{sb}";
        }
    }
}
#endif
