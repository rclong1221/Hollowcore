using System.Collections.Generic;
using DIG.SkillTree;
using UnityEditor;
using UnityEngine;

namespace DIG.Editor.SkillTreeWorkstation.Modules
{
    /// <summary>
    /// EPIC 17.1: Validation checks for skill tree data.
    /// 8 checks: orphan nodes, circular prereqs, unreachable tiers, duplicate IDs,
    /// missing prereq targets, cost validation, keystone limits, empty trees.
    /// </summary>
    public class ValidatorModule : ISkillTreeWorkstationModule
    {
        private SkillTreeDatabaseSO _database;
        private readonly List<ValidationResult> _results = new();
        private Vector2 _scroll;
        private bool _hasRun;

        public void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Skill Tree Validator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var newDb = (SkillTreeDatabaseSO)EditorGUILayout.ObjectField("Database", _database,
                typeof(SkillTreeDatabaseSO), false);
            if (newDb != _database)
            {
                _database = newDb;
                _hasRun = false;
                _results.Clear();
            }

            if (_database == null)
                _database = Resources.Load<SkillTreeDatabaseSO>("SkillTreeDatabase");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run All Checks", GUILayout.Height(28)))
                RunAllChecks();

            if (_hasRun)
            {
                int errors = 0, warnings = 0;
                foreach (var r in _results)
                {
                    if (r.Severity == Severity.Error) errors++;
                    else if (r.Severity == Severity.Warning) warnings++;
                }

                var summary = errors > 0
                    ? $"{errors} errors, {warnings} warnings"
                    : warnings > 0
                        ? $"0 errors, {warnings} warnings"
                        : "All checks passed!";

                var color = errors > 0 ? Color.red : warnings > 0 ? Color.yellow : Color.green;
                var prevColor = GUI.contentColor;
                GUI.contentColor = color;
                EditorGUILayout.LabelField(summary, EditorStyles.boldLabel);
                GUI.contentColor = prevColor;
            }
            EditorGUILayout.EndHorizontal();

            if (!_hasRun)
            {
                EditorGUILayout.HelpBox("Click 'Run All Checks' to validate your skill tree data.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var r in _results)
            {
                var msgType = r.Severity switch
                {
                    Severity.Error => MessageType.Error,
                    Severity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox($"[{r.CheckName}] {r.Message}", msgType);
            }

            if (_results.Count == 0)
                EditorGUILayout.HelpBox("No issues found. All checks passed.", MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void RunAllChecks()
        {
            _results.Clear();
            _hasRun = true;

            if (_database == null)
            {
                _results.Add(new ValidationResult("Database", "No SkillTreeDatabase assigned.", Severity.Error));
                return;
            }

            if (_database.Trees == null || _database.Trees.Count == 0)
            {
                _results.Add(new ValidationResult("Empty Database", "Database has no skill trees.", Severity.Warning));
                return;
            }

            CheckEmptyTrees();
            CheckDuplicateTreeIds();

            for (int i = 0; i < _database.Trees.Count; i++)
            {
                var tree = _database.Trees[i];
                if (tree == null) continue;
                string prefix = $"Tree '{tree.TreeName}' (ID {tree.TreeId})";

                CheckDuplicateNodeIds(tree, prefix);
                CheckMissingPrereqTargets(tree, prefix);
                CheckCircularPrereqs(tree, prefix);
                CheckOrphanNodes(tree, prefix);
                CheckUnreachableTiers(tree, prefix);
                CheckCostValidation(tree, prefix);
                CheckKeystoneLimits(tree, prefix);
            }
        }

        // Check 1: Empty trees
        private void CheckEmptyTrees()
        {
            for (int i = 0; i < _database.Trees.Count; i++)
            {
                var tree = _database.Trees[i];
                if (tree == null)
                {
                    _results.Add(new ValidationResult("Null Tree", $"Tree slot {i} is null.", Severity.Error));
                    continue;
                }
                if (tree.Nodes == null || tree.Nodes.Length == 0)
                    _results.Add(new ValidationResult("Empty Tree", $"Tree '{tree.TreeName}' has no nodes.", Severity.Warning));
            }
        }

        // Check 2: Duplicate tree IDs
        private void CheckDuplicateTreeIds()
        {
            var seen = new HashSet<int>();
            foreach (var tree in _database.Trees)
            {
                if (tree == null) continue;
                if (!seen.Add(tree.TreeId))
                    _results.Add(new ValidationResult("Duplicate Tree ID", $"Tree ID {tree.TreeId} is used by multiple trees.", Severity.Error));
            }
        }

        // Check 3: Duplicate node IDs within a tree
        private void CheckDuplicateNodeIds(SkillTreeSO tree, string prefix)
        {
            if (tree.Nodes == null) return;
            var seen = new HashSet<int>();
            foreach (var node in tree.Nodes)
            {
                if (!seen.Add(node.NodeId))
                    _results.Add(new ValidationResult("Duplicate Node ID", $"{prefix}: Node ID {node.NodeId} is duplicated.", Severity.Error));
            }
        }

        // Check 4: Prerequisites reference non-existent nodes
        private void CheckMissingPrereqTargets(SkillTreeSO tree, string prefix)
        {
            if (tree.Nodes == null) return;
            var nodeIds = new HashSet<int>();
            foreach (var n in tree.Nodes) nodeIds.Add(n.NodeId);

            foreach (var node in tree.Nodes)
            {
                if (node.Prerequisites == null) continue;
                foreach (int prereqId in node.Prerequisites)
                    CheckPrereq(node.NodeId, prereqId, nodeIds, prefix);
            }
        }

        private void CheckPrereq(int nodeId, int prereqId, HashSet<int> validIds, string prefix)
        {
            if (prereqId >= 0 && !validIds.Contains(prereqId))
                _results.Add(new ValidationResult("Missing Prereq", $"{prefix}: Node {nodeId} references prereq {prereqId} which doesn't exist.", Severity.Error));
        }

        // Check 5: Circular prerequisites (DFS cycle detection)
        private void CheckCircularPrereqs(SkillTreeSO tree, string prefix)
        {
            if (tree.Nodes == null) return;

            var adjacency = new Dictionary<int, List<int>>();
            foreach (var node in tree.Nodes)
            {
                var deps = new List<int>();
                if (node.Prerequisites != null)
                {
                    foreach (int prereqId in node.Prerequisites)
                    {
                        if (prereqId >= 0) deps.Add(prereqId);
                    }
                }
                adjacency[node.NodeId] = deps;
            }

            var visited = new HashSet<int>();
            var inStack = new HashSet<int>();

            foreach (var node in tree.Nodes)
            {
                if (!visited.Contains(node.NodeId))
                {
                    if (HasCycle(node.NodeId, adjacency, visited, inStack))
                    {
                        _results.Add(new ValidationResult("Circular Prereqs", $"{prefix}: Circular prerequisite chain detected involving node {node.NodeId}.", Severity.Error));
                        return;
                    }
                }
            }
        }

        private static bool HasCycle(int nodeId, Dictionary<int, List<int>> adj, HashSet<int> visited, HashSet<int> inStack)
        {
            visited.Add(nodeId);
            inStack.Add(nodeId);

            if (adj.TryGetValue(nodeId, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (!visited.Contains(dep))
                    {
                        if (adj.ContainsKey(dep) && HasCycle(dep, adj, visited, inStack))
                            return true;
                    }
                    else if (inStack.Contains(dep))
                    {
                        return true;
                    }
                }
            }

            inStack.Remove(nodeId);
            return false;
        }

        // Check 6: Orphan nodes (no prereqs, not tier 0)
        private void CheckOrphanNodes(SkillTreeSO tree, string prefix)
        {
            if (tree.Nodes == null) return;

            // Nodes that are prerequisites to something
            var referencedNodes = new HashSet<int>();
            foreach (var n in tree.Nodes)
            {
                if (n.Prerequisites == null) continue;
                foreach (int prereqId in n.Prerequisites)
                {
                    if (prereqId >= 0) referencedNodes.Add(prereqId);
                }
            }

            foreach (var node in tree.Nodes)
            {
                bool hasPrereqs = node.Prerequisites != null && node.Prerequisites.Length > 0;
                bool isReferenced = referencedNodes.Contains(node.NodeId);
                bool isTierZero = node.Tier == 0;

                if (!hasPrereqs && !isTierZero && !isReferenced)
                    _results.Add(new ValidationResult("Orphan Node", $"{prefix}: Node {node.NodeId} (tier {node.Tier}) has no prerequisites and is not tier 0. It may be unreachable.", Severity.Warning));
            }
        }

        // Check 7: Unreachable tiers (tier N requires points but no tier N-1 nodes exist)
        private void CheckUnreachableTiers(SkillTreeSO tree, string prefix)
        {
            if (tree.Nodes == null) return;

            var tiers = new HashSet<int>();
            foreach (var n in tree.Nodes) tiers.Add(n.Tier);

            var sortedTiers = new List<int>(tiers);
            sortedTiers.Sort();

            for (int i = 1; i < sortedTiers.Count; i++)
            {
                int prevTier = sortedTiers[i - 1];
                int curTier = sortedTiers[i];

                if (curTier - prevTier > 1)
                    _results.Add(new ValidationResult("Tier Gap", $"{prefix}: Gap between tier {prevTier} and tier {curTier}. Tier {curTier} may be unreachable.", Severity.Warning));
            }
        }

        // Check 8: Cost validation
        private void CheckCostValidation(SkillTreeSO tree, string prefix)
        {
            if (tree.Nodes == null) return;
            foreach (var node in tree.Nodes)
            {
                if (node.PointCost <= 0)
                    _results.Add(new ValidationResult("Invalid Cost", $"{prefix}: Node {node.NodeId} has PointCost={node.PointCost} (must be > 0).", Severity.Error));
                if (node.MaxRanks <= 0)
                    _results.Add(new ValidationResult("Invalid Ranks", $"{prefix}: Node {node.NodeId} has MaxRanks={node.MaxRanks} (must be > 0).", Severity.Error));
                if (node.TierPointsRequired < 0)
                    _results.Add(new ValidationResult("Invalid Tier Req", $"{prefix}: Node {node.NodeId} has negative TierPointsRequired.", Severity.Error));
            }
        }

        // Check 9: Keystone limits (at most 1 keystone per tier)
        private void CheckKeystoneLimits(SkillTreeSO tree, string prefix)
        {
            if (tree.Nodes == null) return;
            var keystonesPerTier = new Dictionary<int, int>();

            foreach (var node in tree.Nodes)
            {
                if (node.NodeType == SkillNodeType.Keystone)
                {
                    keystonesPerTier.TryGetValue(node.Tier, out int count);
                    keystonesPerTier[node.Tier] = count + 1;
                }
            }

            foreach (var kvp in keystonesPerTier)
            {
                if (kvp.Value > 1)
                    _results.Add(new ValidationResult("Multiple Keystones", $"{prefix}: Tier {kvp.Key} has {kvp.Value} keystone nodes. Only 1 keystone per tier is recommended.", Severity.Warning));
            }
        }

        private struct ValidationResult
        {
            public string CheckName;
            public string Message;
            public Severity Severity;

            public ValidationResult(string check, string msg, Severity sev)
            {
                CheckName = check;
                Message = msg;
                Severity = sev;
            }
        }

        private enum Severity { Info, Warning, Error }
    }
}
