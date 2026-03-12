#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 16.16: Dialogue tree validator with 9 error categories.
    /// Checks for dead-end nodes, unreachable nodes, broken references, etc.
    /// </summary>
    public class ValidatorModule : IDialogueModule
    {
        private DialogueDatabaseSO _database;
        private List<ValidationResult> _results = new();
        private Vector2 _scrollPos;

        private struct ValidationResult
        {
            public string TreeName;
            public int TreeId;
            public string Message;
            public MessageType Severity;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Dialogue Validator", EditorStyles.boldLabel);

            _database = (DialogueDatabaseSO)EditorGUILayout.ObjectField(
                "Database", _database, typeof(DialogueDatabaseSO), false);

            if (_database == null)
            {
                EditorGUILayout.HelpBox("Select a DialogueDatabaseSO to validate.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Run Validation", GUILayout.Height(28)))
                RunValidation();

            EditorGUILayout.Space(4);

            // Summary
            int errors = 0, warnings = 0;
            foreach (var r in _results)
            {
                if (r.Severity == MessageType.Error) errors++;
                else if (r.Severity == MessageType.Warning) warnings++;
            }
            EditorGUILayout.LabelField($"Results: {errors} errors, {warnings} warnings, {_results.Count} total",
                EditorStyles.miniLabel);

            // Results
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var result in _results)
            {
                EditorGUILayout.BeginHorizontal();
                var icon = result.Severity == MessageType.Error ? "console.erroricon.sml" :
                    result.Severity == MessageType.Warning ? "console.warnicon.sml" : "console.infoicon.sml";
                GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(20), GUILayout.Height(18));
                EditorGUILayout.LabelField($"[{result.TreeName}] {result.Message}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void RunValidation()
        {
            _results.Clear();

            foreach (var tree in _database.Trees)
            {
                if (tree == null) continue;
                ValidateTree(tree);
            }

            // Check for duplicate TreeIds
            var seenIds = new HashSet<int>();
            foreach (var tree in _database.Trees)
            {
                if (tree == null) continue;
                if (!seenIds.Add(tree.TreeId))
                    AddResult(tree, $"Duplicate TreeId: {tree.TreeId}", MessageType.Error);
            }
        }

        private void ValidateTree(DialogueTreeSO tree)
        {
            if (tree.Nodes == null || tree.Nodes.Length == 0)
            {
                AddResult(tree, "Tree has no nodes.", MessageType.Error);
                return;
            }

            // Build NodeId set
            var nodeIds = new HashSet<int>();
            foreach (var node in tree.Nodes)
                nodeIds.Add(node.NodeId);

            // Check StartNodeId
            if (!nodeIds.Contains(tree.StartNodeId))
                AddResult(tree, $"StartNodeId {tree.StartNodeId} does not exist.", MessageType.Error);

            // Check reachability
            var reachable = new HashSet<int>();
            FloodReach(tree, tree.StartNodeId, reachable, new HashSet<int>());

            foreach (var node in tree.Nodes)
            {
                // 1. Unreachable node
                if (!reachable.Contains(node.NodeId))
                    AddResult(tree, $"Node {node.NodeId} ({node.NodeType}) is unreachable from Start.", MessageType.Warning);

                // 2. Dead-end non-End node
                if (node.NodeType != DialogueNodeType.End && node.NodeType != DialogueNodeType.PlayerChoice)
                {
                    if (node.NextNodeId == 0 && node.NodeType != DialogueNodeType.Condition && node.NodeType != DialogueNodeType.Random)
                        AddResult(tree, $"Node {node.NodeId} ({node.NodeType}) has no outgoing connection.", MessageType.Error);
                }

                // 3. Broken NextNodeId
                if (node.NextNodeId != 0 && !nodeIds.Contains(node.NextNodeId))
                    AddResult(tree, $"Node {node.NodeId}: NextNodeId {node.NextNodeId} does not exist.", MessageType.Error);

                // 4. Choice pointing to missing node
                if (node.Choices != null)
                {
                    for (int c = 0; c < node.Choices.Length; c++)
                    {
                        if (!nodeIds.Contains(node.Choices[c].NextNodeId))
                            AddResult(tree, $"Node {node.NodeId} choice {c}: NextNodeId {node.Choices[c].NextNodeId} does not exist.", MessageType.Error);
                    }
                }

                // 5. Condition branches pointing to missing nodes
                if (node.NodeType == DialogueNodeType.Condition)
                {
                    if (!nodeIds.Contains(node.TrueNodeId))
                        AddResult(tree, $"Node {node.NodeId}: TrueNodeId {node.TrueNodeId} does not exist.", MessageType.Error);
                    if (!nodeIds.Contains(node.FalseNodeId))
                        AddResult(tree, $"Node {node.NodeId}: FalseNodeId {node.FalseNodeId} does not exist.", MessageType.Error);
                }

                // 6. Random entries pointing to missing nodes
                if (node.RandomEntries != null)
                {
                    for (int r = 0; r < node.RandomEntries.Length; r++)
                    {
                        if (!nodeIds.Contains(node.RandomEntries[r].NodeId))
                            AddResult(tree, $"Node {node.NodeId} random entry {r}: NodeId {node.RandomEntries[r].NodeId} does not exist.", MessageType.Error);
                    }
                }

                // 7. Empty text on Speech node
                if (node.NodeType == DialogueNodeType.Speech && string.IsNullOrEmpty(node.Text))
                    AddResult(tree, $"Node {node.NodeId}: Speech node has empty Text.", MessageType.Warning);

                // 8. PlayerChoice with no choices
                if (node.NodeType == DialogueNodeType.PlayerChoice && (node.Choices == null || node.Choices.Length == 0))
                    AddResult(tree, $"Node {node.NodeId}: PlayerChoice node has no choices.", MessageType.Error);

                // 9. Random with no entries
                if (node.NodeType == DialogueNodeType.Random && (node.RandomEntries == null || node.RandomEntries.Length == 0))
                    AddResult(tree, $"Node {node.NodeId}: Random node has no entries.", MessageType.Error);
            }
        }

        private void FloodReach(DialogueTreeSO tree, int nodeId, HashSet<int> visited, HashSet<int> stack)
        {
            if (!visited.Add(nodeId)) return;
            if (stack.Contains(nodeId)) return; // cycle detection
            stack.Add(nodeId);

            int idx = tree.FindNodeIndex(nodeId);
            if (idx < 0) { stack.Remove(nodeId); return; }

            ref var node = ref tree.Nodes[idx];

            if (node.NextNodeId != 0) FloodReach(tree, node.NextNodeId, visited, stack);
            if (node.TrueNodeId != 0) FloodReach(tree, node.TrueNodeId, visited, stack);
            if (node.FalseNodeId != 0) FloodReach(tree, node.FalseNodeId, visited, stack);

            if (node.Choices != null)
                foreach (var c in node.Choices)
                    FloodReach(tree, c.NextNodeId, visited, stack);

            if (node.RandomEntries != null)
                foreach (var r in node.RandomEntries)
                    FloodReach(tree, r.NodeId, visited, stack);

            stack.Remove(nodeId);
        }

        private void AddResult(DialogueTreeSO tree, string message, MessageType severity)
        {
            _results.Add(new ValidationResult
            {
                TreeName = tree.DisplayName ?? tree.name,
                TreeId = tree.TreeId,
                Message = message,
                Severity = severity
            });
        }
    }
}
#endif
