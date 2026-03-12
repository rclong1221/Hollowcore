#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.6: Meta Tree Editor module.
    /// Visual editor for MetaUnlockTreeSO — category color-coding, prerequisite validation,
    /// cost progression, orphan detection, and unlock path simulation.
    /// </summary>
    public class MetaTreeModule : IRunWorkstationModule
    {
        public string TabName => "Meta Tree";

        private MetaUnlockTreeSO _unlockTree;
        private UnityEditor.Editor _treeEditor;
        private Vector2 _scrollPos;
        private bool _showNodes = true;
        private bool _showValidation = true;
        private bool _showSimulation;
        private int _metaCurrencyPerRun = 50;

        // Cached to avoid per-repaint allocation
        private readonly Dictionary<string, int> _categoryCounts = new();
        private readonly HashSet<int> _ids = new();
        private readonly List<int> _duplicateIds = new();

        public void OnEnable() { }
        public void OnDisable()
        {
            if (_treeEditor != null)
                Object.DestroyImmediate(_treeEditor);
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Meta Unlock Tree Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _unlockTree = (MetaUnlockTreeSO)EditorGUILayout.ObjectField(
                "Unlock Tree", _unlockTree, typeof(MetaUnlockTreeSO), false);

            if (_unlockTree == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a MetaUnlockTreeSO to view and edit the unlock tree.\n" +
                    "Create one via Assets > Create > DIG > Roguelite > Meta Unlock Tree.",
                    MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Node list with category color-coding
            _showNodes = EditorGUILayout.Foldout(_showNodes, "Unlock Nodes", true);
            if (_showNodes)
            {
                DrawNodeList();
            }

            EditorGUILayout.Space(8);

            // Validation
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation", true);
            if (_showValidation)
            {
                DrawValidation();
            }

            EditorGUILayout.Space(8);

            // Unlock path simulation
            _showSimulation = EditorGUILayout.Foldout(_showSimulation, "Unlock Path Simulation", true);
            if (_showSimulation)
            {
                DrawUnlockSimulation();
            }

            EditorGUILayout.Space(8);

            // Full inspector
            if (_treeEditor == null || _treeEditor.target != _unlockTree)
            {
                if (_treeEditor != null)
                    Object.DestroyImmediate(_treeEditor);
                _treeEditor = UnityEditor.Editor.CreateEditor(_unlockTree);
            }
            _treeEditor.OnInspectorGUI();

            EditorGUILayout.EndScrollView();
        }

        private void DrawNodeList()
        {
            EditorGUI.indentLevel++;

            var so = new SerializedObject(_unlockTree);
            var nodes = so.FindProperty("Unlocks");
            if (nodes == null || !nodes.isArray || nodes.arraySize == 0)
            {
                EditorGUILayout.LabelField("No nodes defined.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            // Category summary
            _categoryCounts.Clear();
            int totalCost = 0;

            for (int i = 0; i < nodes.arraySize; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i);
                var category = node.FindPropertyRelative("Category");
                var cost = node.FindPropertyRelative("Cost");

                string catName = category != null
                    ? category.enumDisplayNames[category.enumValueIndex] : "Unknown";
                _categoryCounts.TryGetValue(catName, out int count);
                _categoryCounts[catName] = count + 1;

                if (cost != null) totalCost += cost.intValue;
            }

            EditorGUILayout.LabelField($"Total nodes: {nodes.arraySize}, Total cost: {totalCost}",
                EditorStyles.miniLabel);

            foreach (var kvp in _categoryCounts)
            {
                var rect = EditorGUILayout.GetControlRect(false, 18);
                float pct = (float)kvp.Value / nodes.arraySize;
                var barRect = new Rect(rect.x, rect.y, rect.width * pct, rect.height);
                EditorGUI.DrawRect(barRect, GetCategoryColor(kvp.Key));
                EditorGUI.LabelField(rect, $"  {kvp.Key}: {kvp.Value}");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawValidation()
        {
            EditorGUI.indentLevel++;

            var so = new SerializedObject(_unlockTree);
            var nodes = so.FindProperty("Unlocks");
            if (nodes == null || !nodes.isArray)
            {
                EditorGUI.indentLevel--;
                return;
            }

            bool hasIssues = false;

            // Collect all IDs
            _ids.Clear();
            _duplicateIds.Clear();

            for (int i = 0; i < nodes.arraySize; i++)
            {
                var unlockId = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("UnlockId");
                if (unlockId != null)
                {
                    if (!_ids.Add(unlockId.intValue))
                        _duplicateIds.Add(unlockId.intValue);
                }
            }

            // Check for duplicate IDs
            foreach (int dupId in _duplicateIds)
            {
                EditorGUILayout.HelpBox($"Duplicate UnlockId: {dupId}", MessageType.Error);
                hasIssues = true;
            }

            // Check for orphans (prerequisite ID not in tree)
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i);
                var prereq = node.FindPropertyRelative("PrerequisiteId");
                var unlockId = node.FindPropertyRelative("UnlockId");

                if (prereq != null && prereq.intValue > 0 && !_ids.Contains(prereq.intValue))
                {
                    int nodeId = unlockId != null ? unlockId.intValue : i;
                    EditorGUILayout.HelpBox(
                        $"Orphan: Node {nodeId} requires prerequisite {prereq.intValue} which doesn't exist.",
                        MessageType.Warning);
                    hasIssues = true;
                }
            }

            // Check for zero-cost nodes
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var cost = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("Cost");
                var unlockId = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("UnlockId");
                if (cost != null && cost.intValue <= 0)
                {
                    int nodeId = unlockId != null ? unlockId.intValue : i;
                    EditorGUILayout.HelpBox($"Node {nodeId} has zero or negative cost.", MessageType.Warning);
                    hasIssues = true;
                }
            }

            if (!hasIssues)
                EditorGUILayout.HelpBox("All validations passed.", MessageType.None);

            EditorGUI.indentLevel--;
        }

        private void DrawUnlockSimulation()
        {
            EditorGUI.indentLevel++;

            _metaCurrencyPerRun = EditorGUILayout.IntField("Meta-Currency Per Run", _metaCurrencyPerRun);

            if (_metaCurrencyPerRun <= 0) _metaCurrencyPerRun = 1;

            if (GUILayout.Button("Simulate Unlock Path"))
            {
                var so = new SerializedObject(_unlockTree);
                var nodes = so.FindProperty("Unlocks");
                if (nodes == null || !nodes.isArray || nodes.arraySize == 0)
                {
                    Debug.Log("[MetaTree Sim] No nodes to simulate.");
                }
                else
                {
                    int totalCost = 0;
                    for (int i = 0; i < nodes.arraySize; i++)
                    {
                        var cost = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("Cost");
                        if (cost != null) totalCost += cost.intValue;
                    }

                    int runsToUnlockAll = Mathf.CeilToInt((float)totalCost / _metaCurrencyPerRun);
                    Debug.Log($"[MetaTree Sim] Total cost: {totalCost}, " +
                              $"Currency/run: {_metaCurrencyPerRun}, " +
                              $"Runs to unlock all: {runsToUnlockAll}");
                }
            }

            EditorGUI.indentLevel--;
        }

        private static Color GetCategoryColor(string category)
        {
            return category switch
            {
                "StatBoost" => new Color(0.3f, 0.5f, 0.9f, 0.5f),
                "StarterItem" => new Color(0.3f, 0.8f, 0.3f, 0.5f),
                "NewAbility" => new Color(0.7f, 0.3f, 0.8f, 0.5f),
                "Cosmetic" => new Color(0.9f, 0.8f, 0.2f, 0.5f),
                "RunModifier" => new Color(0.8f, 0.4f, 0.2f, 0.5f),
                "ZoneAccess" => new Color(0.2f, 0.7f, 0.7f, 0.5f),
                "ShopUpgrade" => new Color(0.5f, 0.8f, 0.3f, 0.5f),
                "CurrencyBonus" => new Color(0.8f, 0.7f, 0.3f, 0.5f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };
        }
    }
}
#endif
