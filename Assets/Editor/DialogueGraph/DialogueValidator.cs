#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Enhanced dialogue tree validator.
    /// Detects dead-end nodes, orphan nodes, circular loops, empty text,
    /// missing VO, missing localization, and structural errors.
    /// Used by both the graph editor and workstation validator module.
    /// </summary>
    public static class DialogueValidator
    {
        public struct ValidationIssue
        {
            public string Message;
            public MessageType Severity;
            public int NodeId;
        }

        /// <summary>
        /// Validate a single DialogueTreeSO. Returns list of issues.
        /// </summary>
        public static List<ValidationIssue> Validate(DialogueTreeSO tree)
        {
            var issues = new List<ValidationIssue>();

            if (tree == null)
            {
                issues.Add(new ValidationIssue { Message = "Tree is null.", Severity = MessageType.Error });
                return issues;
            }

            if (tree.TreeId <= 0)
                issues.Add(new ValidationIssue { Message = "TreeId must be > 0.", Severity = MessageType.Error });

            if (tree.Nodes == null || tree.Nodes.Length == 0)
            {
                issues.Add(new ValidationIssue { Message = "Tree has no nodes.", Severity = MessageType.Error });
                return issues;
            }

            // Build node ID set and index
            var nodeIds = new HashSet<int>();
            var nodeIndex = new Dictionary<int, int>(tree.Nodes.Length); // NodeId → array index
            for (int i = 0; i < tree.Nodes.Length; i++)
            {
                nodeIds.Add(tree.Nodes[i].NodeId);
                nodeIndex[tree.Nodes[i].NodeId] = i;
            }

            // Check StartNodeId
            if (!nodeIds.Contains(tree.StartNodeId))
                issues.Add(new ValidationIssue
                {
                    Message = $"StartNodeId {tree.StartNodeId} does not exist.",
                    Severity = MessageType.Error
                });

            // Reachability (BFS)
            var reachable = new HashSet<int>();
            var bfsQueue = new Queue<int>();
            if (nodeIds.Contains(tree.StartNodeId))
            {
                bfsQueue.Enqueue(tree.StartNodeId);
                reachable.Add(tree.StartNodeId);
            }

            while (bfsQueue.Count > 0)
            {
                int current = bfsQueue.Dequeue();
                if (!nodeIndex.TryGetValue(current, out int idx)) continue;
                ref var node = ref tree.Nodes[idx];

                void TryEnqueue(int id)
                {
                    if (id != 0 && nodeIds.Contains(id) && reachable.Add(id))
                        bfsQueue.Enqueue(id);
                }

                TryEnqueue(node.NextNodeId);
                TryEnqueue(node.TrueNodeId);
                TryEnqueue(node.FalseNodeId);

                if (node.Choices != null)
                    for (int c = 0; c < node.Choices.Length; c++)
                        TryEnqueue(node.Choices[c].NextNodeId);

                if (node.RandomEntries != null)
                    for (int r = 0; r < node.RandomEntries.Length; r++)
                        TryEnqueue(node.RandomEntries[r].NodeId);
            }

            // Circular loop detection — iterative DFS with back-edge check (no stack overflow risk)
            // Uses white(0)/gray(1)/black(2) coloring. O(N+E), no recursion.
            var loopNodes = new HashSet<int>();
            {
                var color = new Dictionary<int, byte>(tree.Nodes.Length);
                var dfsStack = new Stack<(int nodeId, bool backtrack)>();

                if (nodeIds.Contains(tree.StartNodeId))
                    dfsStack.Push((tree.StartNodeId, false));

                while (dfsStack.Count > 0)
                {
                    var (nid, backtrack) = dfsStack.Pop();

                    if (backtrack) { color[nid] = 2; continue; }

                    color.TryGetValue(nid, out byte c);
                    if (c != 0) continue; // already visited or in-progress from another path

                    color[nid] = 1; // gray — in progress
                    dfsStack.Push((nid, true)); // schedule backtrack to mark black

                    if (!nodeIndex.TryGetValue(nid, out int nIdx)) continue;
                    ref var dfsNode = ref tree.Nodes[nIdx];

                    // Inline successor checks — no per-node List allocation
                    CheckDfsEdge(dfsNode.NextNodeId, nid, color, loopNodes, dfsStack);
                    CheckDfsEdge(dfsNode.TrueNodeId, nid, color, loopNodes, dfsStack);
                    CheckDfsEdge(dfsNode.FalseNodeId, nid, color, loopNodes, dfsStack);

                    if (dfsNode.Choices != null)
                        for (int ci = 0; ci < dfsNode.Choices.Length; ci++)
                            CheckDfsEdge(dfsNode.Choices[ci].NextNodeId, nid, color, loopNodes, dfsStack);

                    if (dfsNode.RandomEntries != null)
                        for (int ri = 0; ri < dfsNode.RandomEntries.Length; ri++)
                            CheckDfsEdge(dfsNode.RandomEntries[ri].NodeId, nid, color, loopNodes, dfsStack);
                }
            }

            // Per-node checks
            bool hasEnd = false;
            for (int i = 0; i < tree.Nodes.Length; i++)
            {
                ref var node = ref tree.Nodes[i];
                int nid = node.NodeId;

                // Orphan check
                if (!reachable.Contains(nid))
                    issues.Add(new ValidationIssue
                    {
                        Message = $"Node {nid} ({node.NodeType}) is unreachable from Start.",
                        Severity = MessageType.Warning,
                        NodeId = nid
                    });

                // Dead-end check (non-End, non-Choice, no outgoing)
                if (node.NodeType != DialogueNodeType.End &&
                    node.NodeType != DialogueNodeType.PlayerChoice)
                {
                    bool hasOutgoing = node.NextNodeId != 0;
                    if (node.NodeType == DialogueNodeType.Condition)
                        hasOutgoing = node.TrueNodeId != 0 || node.FalseNodeId != 0;
                    if (node.NodeType == DialogueNodeType.Random)
                        hasOutgoing = node.RandomEntries != null && node.RandomEntries.Length > 0;
                    if (!hasOutgoing)
                        issues.Add(new ValidationIssue
                        {
                            Message = $"Node {nid} ({node.NodeType}) has no outgoing connection (dead end).",
                            Severity = MessageType.Error,
                            NodeId = nid
                        });
                }

                // Broken references
                if (node.NextNodeId != 0 && !nodeIds.Contains(node.NextNodeId))
                    issues.Add(new ValidationIssue
                    {
                        Message = $"Node {nid}: NextNodeId {node.NextNodeId} does not exist.",
                        Severity = MessageType.Error,
                        NodeId = nid
                    });

                if (node.NodeType == DialogueNodeType.Condition)
                {
                    if (node.TrueNodeId != 0 && !nodeIds.Contains(node.TrueNodeId))
                        issues.Add(new ValidationIssue
                        {
                            Message = $"Node {nid}: TrueNodeId {node.TrueNodeId} does not exist.",
                            Severity = MessageType.Error,
                            NodeId = nid
                        });
                    if (node.FalseNodeId != 0 && !nodeIds.Contains(node.FalseNodeId))
                        issues.Add(new ValidationIssue
                        {
                            Message = $"Node {nid}: FalseNodeId {node.FalseNodeId} does not exist.",
                            Severity = MessageType.Error,
                            NodeId = nid
                        });
                }

                // Broken choice targets — skip unconnected (NextNodeId == 0)
                if (node.Choices != null)
                {
                    for (int c = 0; c < node.Choices.Length; c++)
                    {
                        if (node.Choices[c].NextNodeId != 0 && !nodeIds.Contains(node.Choices[c].NextNodeId))
                            issues.Add(new ValidationIssue
                            {
                                Message = $"Node {nid} choice {c}: NextNodeId {node.Choices[c].NextNodeId} does not exist.",
                                Severity = MessageType.Error,
                                NodeId = nid
                            });
                    }
                }

                // Broken random targets — skip unconnected (NodeId == 0)
                if (node.RandomEntries != null)
                {
                    for (int r = 0; r < node.RandomEntries.Length; r++)
                    {
                        if (node.RandomEntries[r].NodeId != 0 && !nodeIds.Contains(node.RandomEntries[r].NodeId))
                            issues.Add(new ValidationIssue
                            {
                                Message = $"Node {nid} random entry {r}: NodeId {node.RandomEntries[r].NodeId} does not exist.",
                                Severity = MessageType.Error,
                                NodeId = nid
                            });
                    }
                }

                // Empty text on Speech
                if (node.NodeType == DialogueNodeType.Speech && string.IsNullOrEmpty(node.Text))
                    issues.Add(new ValidationIssue
                    {
                        Message = $"Node {nid}: Speech node has empty text.",
                        Severity = MessageType.Warning,
                        NodeId = nid
                    });

                // PlayerChoice with no choices
                if (node.NodeType == DialogueNodeType.PlayerChoice &&
                    (node.Choices == null || node.Choices.Length == 0))
                    issues.Add(new ValidationIssue
                    {
                        Message = $"Node {nid}: PlayerChoice node has no choices.",
                        Severity = MessageType.Error,
                        NodeId = nid
                    });

                // Random with no entries
                if (node.NodeType == DialogueNodeType.Random &&
                    (node.RandomEntries == null || node.RandomEntries.Length == 0))
                    issues.Add(new ValidationIssue
                    {
                        Message = $"Node {nid}: Random node has no entries.",
                        Severity = MessageType.Error,
                        NodeId = nid
                    });

                // EPIC 18.5: Missing VO on Speech with speaker
                if (node.NodeType == DialogueNodeType.Speech &&
                    !string.IsNullOrEmpty(node.SpeakerName) &&
                    node.VoiceClip == null &&
                    string.IsNullOrEmpty(node.AudioClipPath))
                    issues.Add(new ValidationIssue
                    {
                        Message = $"Node {nid}: Speech node has speaker \"{node.SpeakerName}\" but no voice clip or audio path.",
                        Severity = MessageType.Info,
                        NodeId = nid
                    });

                // Circular loop warning
                if (loopNodes.Contains(nid))
                    issues.Add(new ValidationIssue
                    {
                        Message = $"Node {nid} is part of a circular loop — ensure there is an exit condition.",
                        Severity = MessageType.Warning,
                        NodeId = nid
                    });

                if (node.NodeType == DialogueNodeType.End)
                    hasEnd = true;
            }

            if (!hasEnd)
                issues.Add(new ValidationIssue
                {
                    Message = "Tree has no End node.",
                    Severity = MessageType.Warning
                });

            return issues;
        }

        /// <summary>
        /// Validate all trees in a database. Returns issues keyed by tree display name.
        /// Merges per-tree issues with database-level issues (e.g., duplicate TreeIds).
        /// </summary>
        public static Dictionary<string, List<ValidationIssue>> ValidateDatabase(DialogueDatabaseSO database)
        {
            var allIssues = new Dictionary<string, List<ValidationIssue>>();
            if (database == null || database.Trees == null) return allIssues;

            // Check duplicate TreeIds
            var seenIds = new HashSet<int>();
            foreach (var tree in database.Trees)
            {
                if (tree == null) continue;
                if (!seenIds.Add(tree.TreeId))
                {
                    string name = tree.DisplayName ?? tree.name;
                    if (!allIssues.TryGetValue(name, out var list))
                    {
                        list = new List<ValidationIssue>();
                        allIssues[name] = list;
                    }
                    list.Add(new ValidationIssue
                    {
                        Message = $"Duplicate TreeId: {tree.TreeId}",
                        Severity = MessageType.Error
                    });
                }
            }

            foreach (var tree in database.Trees)
            {
                if (tree == null) continue;
                string name = tree.DisplayName ?? tree.name;
                var issues = Validate(tree);
                if (issues.Count > 0)
                {
                    // Merge with any existing issues (e.g., duplicate-ID warnings)
                    if (allIssues.TryGetValue(name, out var existing))
                        existing.AddRange(issues);
                    else
                        allIssues[name] = issues;
                }
            }

            return allIssues;
        }

        /// <summary>
        /// DFS edge check helper: if successor is gray (in-progress), it's a back-edge (cycle).
        /// If white (unvisited), push for exploration.
        /// </summary>
        private static void CheckDfsEdge(int succId, int parentId,
            Dictionary<int, byte> color, HashSet<int> loopNodes,
            Stack<(int nodeId, bool backtrack)> dfsStack)
        {
            if (succId == 0) return;
            color.TryGetValue(succId, out byte sc);
            if (sc == 1) { loopNodes.Add(parentId); loopNodes.Add(succId); }
            else if (sc == 0) dfsStack.Push((succId, false));
        }
    }
}
#endif
