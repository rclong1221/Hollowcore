#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DIG.Achievement.Editor.Modules
{
    /// <summary>
    /// EPIC 17.7: Create/edit AchievementDefinitionSO assets. Category dropdown,
    /// condition type selector, tier editor, hidden toggle, icon picker.
    /// Batch creation: "Generate tier set" fills Bronze/Silver/Gold/Platinum from template.
    /// </summary>
    public class DefinitionEditorModule : IAchievementWorkstationModule
    {
        public string ModuleName => "Definition Editor";

        private AchievementDatabaseSO _database;
        private int _selectedIndex = -1;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        // Batch creation fields
        private string _batchName = "New Achievement";
        private AchievementCategory _batchCategory = AchievementCategory.Combat;
        private AchievementConditionType _batchCondition = AchievementConditionType.EnemyKill;
        private int _batchBronze = 10;
        private int _batchSilver = 50;
        private int _batchGold = 100;
        private int _batchPlatinum = 500;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Achievement Definition Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Database reference
            _database = (AchievementDatabaseSO)EditorGUILayout.ObjectField(
                "Database", _database, typeof(AchievementDatabaseSO), false);

            if (_database == null)
            {
                _database = Resources.Load<AchievementDatabaseSO>("AchievementDatabase");
                if (_database == null)
                {
                    EditorGUILayout.HelpBox("No AchievementDatabaseSO found. Create one via Assets > Create > DIG > Achievement > Achievement Database.", MessageType.Warning);
                    return;
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            // Left: Achievement list
            EditorGUILayout.BeginVertical("box", GUILayout.Width(220));
            EditorGUILayout.LabelField($"Achievements ({_database.Achievements.Count})", EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(300));

            for (int i = 0; i < _database.Achievements.Count; i++)
            {
                var def = _database.Achievements[i];
                string label = def != null ? $"[{def.AchievementId}] {def.AchievementName}" : $"[{i}] (null)";

                var prevBg = GUI.backgroundColor;
                if (i == _selectedIndex) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button(label, EditorStyles.miniButton))
                    _selectedIndex = i;
                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add New Definition"))
            {
                var newDef = ScriptableObject.CreateInstance<AchievementDefinitionSO>();
                newDef.AchievementId = (ushort)(_database.Achievements.Count > 0
                    ? _database.Achievements.Max(d => d != null ? d.AchievementId : 0) + 1
                    : 1);
                newDef.AchievementName = "New Achievement";
                newDef.name = $"Achievement_{newDef.AchievementId}";

                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Achievement Definition", newDef.name, "asset", "Save achievement definition");
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(newDef, path);
                    _database.Achievements.Add(newDef);
                    EditorUtility.SetDirty(_database);
                    AssetDatabase.SaveAssets();
                    _selectedIndex = _database.Achievements.Count - 1;
                }
            }

            EditorGUILayout.EndVertical();

            // Right: Detail view
            EditorGUILayout.BeginVertical();
            if (_selectedIndex >= 0 && _selectedIndex < _database.Achievements.Count)
            {
                var def = _database.Achievements[_selectedIndex];
                if (def != null)
                {
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                    DrawDefinitionDetail(def);
                    EditorGUILayout.EndScrollView();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select an achievement from the list.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Batch creation section
            EditorGUILayout.Space(16);
            DrawBatchCreation();
        }

        private void DrawDefinitionDetail(AchievementDefinitionSO def)
        {
            EditorGUI.BeginChangeCheck();

            var editor = UnityEditor.Editor.CreateEditor(def);
            editor.OnInspectorGUI();
            Object.DestroyImmediate(editor);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(def);
            }
        }

        private void DrawBatchCreation()
        {
            EditorGUILayout.LabelField("Batch Tier Generation", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            _batchName = EditorGUILayout.TextField("Name", _batchName);
            _batchCategory = (AchievementCategory)EditorGUILayout.EnumPopup("Category", _batchCategory);
            _batchCondition = (AchievementConditionType)EditorGUILayout.EnumPopup("Condition", _batchCondition);
            _batchBronze = EditorGUILayout.IntField("Bronze Threshold", _batchBronze);
            _batchSilver = EditorGUILayout.IntField("Silver Threshold", _batchSilver);
            _batchGold = EditorGUILayout.IntField("Gold Threshold", _batchGold);
            _batchPlatinum = EditorGUILayout.IntField("Platinum Threshold", _batchPlatinum);

            if (GUILayout.Button("Generate 4-Tier Achievement"))
            {
                if (_database == null) return;

                var newDef = ScriptableObject.CreateInstance<AchievementDefinitionSO>();
                newDef.AchievementId = (ushort)(_database.Achievements.Count > 0
                    ? _database.Achievements.Max(d => d != null ? d.AchievementId : 0) + 1
                    : 1);
                newDef.AchievementName = _batchName;
                newDef.Category = _batchCategory;
                newDef.ConditionType = _batchCondition;
                newDef.Tiers = new[]
                {
                    new AchievementTierDefinition { Tier = AchievementTier.Bronze, Threshold = _batchBronze, RewardType = AchievementRewardType.Gold, RewardIntValue = 100, RewardDescription = "100 Gold" },
                    new AchievementTierDefinition { Tier = AchievementTier.Silver, Threshold = _batchSilver, RewardType = AchievementRewardType.Gold, RewardIntValue = 500, RewardDescription = "500 Gold" },
                    new AchievementTierDefinition { Tier = AchievementTier.Gold, Threshold = _batchGold, RewardType = AchievementRewardType.XP, RewardIntValue = 1000, RewardDescription = "1000 XP" },
                    new AchievementTierDefinition { Tier = AchievementTier.Platinum, Threshold = _batchPlatinum, RewardType = AchievementRewardType.TalentPoints, RewardIntValue = 2, RewardDescription = "2 Talent Points" }
                };
                newDef.name = $"Achievement_{newDef.AchievementId}_{_batchName.Replace(" ", "")}";

                string path = $"Assets/Resources/Achievements/{newDef.name}.asset";
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                AssetDatabase.CreateAsset(newDef, path);
                _database.Achievements.Add(newDef);
                EditorUtility.SetDirty(_database);
                AssetDatabase.SaveAssets();
                _selectedIndex = _database.Achievements.Count - 1;

                Debug.Log($"[AchievementWorkstation] Generated 4-tier achievement: {_batchName} (ID: {newDef.AchievementId})");
            }

            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
