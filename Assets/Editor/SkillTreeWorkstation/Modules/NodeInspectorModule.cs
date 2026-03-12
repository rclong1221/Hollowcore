using DIG.SkillTree;
using UnityEditor;
using UnityEngine;

namespace DIG.Editor.SkillTreeWorkstation.Modules
{
    /// <summary>
    /// EPIC 17.1: Property inspector for the currently selected skill node.
    /// Displays and edits node fields via SerializedObject when a SkillTreeSO is selected.
    /// </summary>
    public class NodeInspectorModule : ISkillTreeWorkstationModule
    {
        private SkillTreeDatabaseSO _database;
        private int _selectedTreeIndex;
        private int _selectedNodeIndex;
        private Vector2 _scroll;
        private SerializedObject _serializedTree;

        public void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Node Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Database
            var newDb = (SkillTreeDatabaseSO)EditorGUILayout.ObjectField("Database", _database,
                typeof(SkillTreeDatabaseSO), false);
            if (newDb != _database)
            {
                _database = newDb;
                _selectedTreeIndex = 0;
                _selectedNodeIndex = 0;
                _serializedTree = null;
            }

            if (_database == null)
                _database = Resources.Load<SkillTreeDatabaseSO>("SkillTreeDatabase");

            if (_database == null || _database.Trees == null || _database.Trees.Count == 0)
            {
                EditorGUILayout.HelpBox("No SkillTreeDatabase loaded.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // Tree selector
            var treeNames = new string[_database.Trees.Count];
            for (int i = 0; i < _database.Trees.Count; i++)
                treeNames[i] = _database.Trees[i] != null ? _database.Trees[i].TreeName : $"(null {i})";

            int newTreeIdx = EditorGUILayout.Popup("Tree", _selectedTreeIndex, treeNames);
            if (newTreeIdx != _selectedTreeIndex)
            {
                _selectedTreeIndex = newTreeIdx;
                _selectedNodeIndex = 0;
                _serializedTree = null;
            }

            var tree = _database.Trees[_selectedTreeIndex];
            if (tree == null || tree.Nodes == null || tree.Nodes.Length == 0)
            {
                EditorGUILayout.HelpBox("Selected tree has no nodes.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // Node selector
            var nodeNames = new string[tree.Nodes.Length];
            for (int i = 0; i < tree.Nodes.Length; i++)
                nodeNames[i] = $"[{tree.Nodes[i].NodeId}] T{tree.Nodes[i].Tier} {tree.Nodes[i].NodeType}";

            _selectedNodeIndex = Mathf.Clamp(_selectedNodeIndex, 0, tree.Nodes.Length - 1);
            _selectedNodeIndex = EditorGUILayout.Popup("Node", _selectedNodeIndex, nodeNames);

            EditorGUILayout.Space(8);

            // Draw node properties via SerializedObject
            if (_serializedTree == null || _serializedTree.targetObject != tree)
                _serializedTree = new SerializedObject(tree);

            _serializedTree.Update();

            var nodesProp = _serializedTree.FindProperty("Nodes");
            if (nodesProp != null && _selectedNodeIndex < nodesProp.arraySize)
            {
                var nodeProp = nodesProp.GetArrayElementAtIndex(_selectedNodeIndex);

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("NodeId"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("Tier"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("NodeType"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Cost & Ranks", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("PointCost"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("MaxRanks"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("TierPointsRequired"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Passive Bonus", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("BonusType"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("BonusValue"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Ability (ActiveAbility nodes only)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("AbilityTypeId"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Prerequisites (NodeId array)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("Prerequisites"), true);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Editor Layout", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("EditorPosition"));

                EditorGUILayout.EndScrollView();
            }

            if (_serializedTree.ApplyModifiedProperties())
                EditorUtility.SetDirty(tree);

            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
