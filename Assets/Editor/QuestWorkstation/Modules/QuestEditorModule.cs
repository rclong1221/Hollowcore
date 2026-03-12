using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace DIG.Quest.Editor.Modules
{
    /// <summary>
    /// EPIC 16.12: Quest Editor module — list/search/filter quests, edit inline.
    /// </summary>
    public class QuestEditorModule : IQuestModule
    {
        private QuestDatabaseSO _database;
        private string _searchFilter = "";
        private QuestCategory _categoryFilter = (QuestCategory)255; // All
        private Vector2 _listScroll;
        private int _selectedIndex = -1;
        private UnityEditor.Editor _questEditor;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Quest Editor", EditorStyles.boldLabel);

            // Database reference
            _database = (QuestDatabaseSO)EditorGUILayout.ObjectField("Database", _database, typeof(QuestDatabaseSO), false);
            if (_database == null)
            {
                _database = Resources.Load<QuestDatabaseSO>("QuestDatabase");
                if (_database == null)
                {
                    EditorGUILayout.HelpBox("No QuestDatabaseSO found. Create one at Resources/QuestDatabase.", MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(4);

            // Search and filter bar
            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            _categoryFilter = (QuestCategory)EditorGUILayout.EnumPopup("Category", _categoryFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Quest list
            EditorGUILayout.BeginHorizontal();

            // Left: quest list
            EditorGUILayout.BeginVertical("box", GUILayout.Width(250));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(400));

            var filtered = GetFilteredQuests();
            for (int i = 0; i < filtered.Count; i++)
            {
                var quest = filtered[i];
                bool isSelected = _selectedIndex == i;
                var style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"[{quest.QuestId}] {quest.DisplayName}", style))
                    _selectedIndex = i;

                var catColor = GetCategoryColor(quest.Category);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = catColor;
                GUILayout.Label(quest.Category.ToString(), EditorStyles.miniLabel, GUILayout.Width(50));
                GUI.backgroundColor = prevBg;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ New Quest"))
                CreateNewQuest();

            EditorGUILayout.EndVertical();

            // Right: selected quest inspector
            EditorGUILayout.BeginVertical("box");
            if (_selectedIndex >= 0 && _selectedIndex < filtered.Count)
            {
                var selected = filtered[_selectedIndex];
                if (_questEditor == null || _questEditor.target != selected)
                    _questEditor = UnityEditor.Editor.CreateEditor(selected);

                _questEditor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.HelpBox("Select a quest from the list.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(UnityEditor.SceneView sceneView) { }

        private List<QuestDefinitionSO> GetFilteredQuests()
        {
            var result = new List<QuestDefinitionSO>();
            if (_database == null) return result;

            foreach (var quest in _database.Quests)
            {
                if (quest == null) continue;

                // Category filter
                if ((byte)_categoryFilter != 255 && quest.Category != _categoryFilter)
                    continue;

                // Search filter
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    bool match = quest.DisplayName != null &&
                        quest.DisplayName.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase);
                    match |= quest.QuestId.ToString().Contains(_searchFilter);
                    if (!match) continue;
                }

                result.Add(quest);
            }
            return result;
        }

        private void CreateNewQuest()
        {
            if (_database == null) return;

            var path = EditorUtility.SaveFilePanelInProject(
                "New Quest Definition", "NewQuest", "asset", "Choose location for new quest");
            if (string.IsNullOrEmpty(path)) return;

            var quest = ScriptableObject.CreateInstance<QuestDefinitionSO>();
            quest.QuestId = GetNextQuestId();
            quest.DisplayName = "New Quest";
            AssetDatabase.CreateAsset(quest, path);
            _database.Quests.Add(quest);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
        }

        private int GetNextQuestId()
        {
            int max = 0;
            foreach (var q in _database.Quests)
                if (q != null && q.QuestId > max) max = q.QuestId;
            return max + 1;
        }

        private static Color GetCategoryColor(QuestCategory cat) => cat switch
        {
            QuestCategory.Main => new Color(1f, 0.8f, 0.2f),
            QuestCategory.Side => new Color(0.6f, 0.8f, 1f),
            QuestCategory.Daily => new Color(0.6f, 1f, 0.6f),
            QuestCategory.Event => new Color(1f, 0.5f, 0.5f),
            QuestCategory.Tutorial => new Color(0.8f, 0.8f, 0.8f),
            _ => Color.white
        };
    }
}
