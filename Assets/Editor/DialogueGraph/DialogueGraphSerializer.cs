#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Reads/writes between DialogueTreeSO arrays and GraphView nodes.
    /// Handles node ID generation, position migration, and edge wiring.
    /// </summary>
    public class DialogueGraphSerializer
    {
        /// <summary>
        /// Load a DialogueTreeSO into the GraphView as visual nodes + edges.
        /// </summary>
        public void Deserialize(DialogueTreeSO tree, DialogueGraphView graphView)
        {
            if (tree.Nodes == null || tree.Nodes.Length == 0)
                return;

            // Ensure NodeEditorPositions is the right length
            bool needsLayout = tree.NodeEditorPositions == null ||
                               tree.NodeEditorPositions.Length != tree.Nodes.Length;

            var nodeViews = new Dictionary<int, DialogueNodeViewBase>(tree.Nodes.Length);

            // Create node views
            for (int i = 0; i < tree.Nodes.Length; i++)
            {
                ref var node = ref tree.Nodes[i];
                var nodeView = DialogueNodeFactory.CreateNodeView(node.NodeType, node.NodeId);
                nodeView.LoadFromDialogueNode(ref node);

                // Set position
                Vector2 pos = needsLayout
                    ? new Vector2(i % 4 * 250, i / 4 * 200) // default grid layout
                    : tree.NodeEditorPositions[i];
                nodeView.SetPosition(new Rect(pos, new Vector2(200, 150)));

                // Mark start node
                if (node.NodeId == tree.StartNodeId)
                    nodeView.MarkAsStart();

                graphView.AddElement(nodeView);
                nodeViews[node.NodeId] = nodeView;
            }

            // Create edges
            for (int i = 0; i < tree.Nodes.Length; i++)
            {
                ref var node = ref tree.Nodes[i];
                var sourceView = nodeViews[node.NodeId];

                // NextNodeId
                if (node.NextNodeId != 0 && nodeViews.TryGetValue(node.NextNodeId, out var nextView))
                    CreateEdge(graphView, sourceView.OutputPort, nextView.InputPort);

                // Condition branches
                if (node.NodeType == DialogueNodeType.Condition)
                {
                    if (node.TrueNodeId != 0 && sourceView is ConditionNodeView condView)
                    {
                        if (nodeViews.TryGetValue(node.TrueNodeId, out var trueView))
                            CreateEdge(graphView, condView.TruePort, trueView.InputPort);
                        if (node.FalseNodeId != 0 && nodeViews.TryGetValue(node.FalseNodeId, out var falseView))
                            CreateEdge(graphView, condView.FalsePort, falseView.InputPort);
                    }
                }

                // Choices
                if (node.Choices != null && sourceView is ChoiceNodeView choiceView)
                {
                    for (int c = 0; c < node.Choices.Length && c < choiceView.ChoicePorts.Count; c++)
                    {
                        if (node.Choices[c].NextNodeId != 0 && nodeViews.TryGetValue(node.Choices[c].NextNodeId, out var choiceTarget))
                            CreateEdge(graphView, choiceView.ChoicePorts[c], choiceTarget.InputPort);
                    }
                }

                // Random entries
                if (node.RandomEntries != null && sourceView is RandomNodeView randomView)
                {
                    for (int r = 0; r < node.RandomEntries.Length && r < randomView.RandomPorts.Count; r++)
                    {
                        if (nodeViews.TryGetValue(node.RandomEntries[r].NodeId, out var randomTarget))
                            CreateEdge(graphView, randomView.RandomPorts[r], randomTarget.InputPort);
                    }
                }
            }

            if (needsLayout)
                DialogueAutoLayout.Layout(graphView, tree.StartNodeId);
        }

        /// <summary>
        /// Write the current GraphView state back to the DialogueTreeSO.
        /// </summary>
        public void Serialize(DialogueGraphView graphView, DialogueTreeSO tree)
        {
            var nodeViews = new List<DialogueNodeViewBase>();
            graphView.graphElements.ForEach(e =>
            {
                if (e is DialogueNodeViewBase nv) nodeViews.Add(nv);
            });

            // Build edge map: output port → target node ID
            var edgeMap = new Dictionary<Port, int>();
            graphView.graphElements.ForEach(e =>
            {
                if (e is Edge edge && edge.output != null && edge.input != null)
                {
                    if (edge.input.node is DialogueNodeViewBase targetNode)
                        edgeMap[edge.output] = targetNode.NodeId;
                }
            });

            // Write nodes and positions
            var nodes = new DialogueNode[nodeViews.Count];
            var positions = new Vector2[nodeViews.Count];

            for (int i = 0; i < nodeViews.Count; i++)
            {
                var view = nodeViews[i];
                nodes[i] = view.SaveToDialogueNode(edgeMap);
                positions[i] = view.GetPosition().position;
            }

            tree.Nodes = nodes;
            tree.NodeEditorPositions = positions;
        }

        private void CreateEdge(DialogueGraphView graphView, Port output, Port input)
        {
            if (output == null || input == null) return;
            var edge = new Edge { output = output, input = input };
            edge.output.Connect(edge);
            edge.input.Connect(edge);
            graphView.AddElement(edge);
        }
    }
}
#endif
