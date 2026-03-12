#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Dagre-style automatic layout for dialogue graph nodes.
    /// Top-to-bottom layout with horizontal spacing per depth level.
    /// </summary>
    public static class DialogueAutoLayout
    {
        private const float NodeWidth = 220f;
        private const float NodeHeight = 160f;
        private const float HorizontalSpacing = 60f;
        private const float VerticalSpacing = 80f;

        public static void Layout(DialogueGraphView graphView, int startNodeId)
        {
            // Collect all node views
            var nodeViews = new Dictionary<int, DialogueNodeViewBase>();
            graphView.graphElements.ForEach(e =>
            {
                if (e is DialogueNodeViewBase nv)
                    nodeViews[nv.NodeId] = nv;
            });

            if (nodeViews.Count == 0) return;

            // Build adjacency from edges
            var children = new Dictionary<int, List<int>>();
            foreach (var kv in nodeViews) children[kv.Key] = new List<int>();

            graphView.graphElements.ForEach(e =>
            {
                if (e is Edge edge && edge.output != null && edge.input != null)
                {
                    if (edge.output.node is DialogueNodeViewBase src &&
                        edge.input.node is DialogueNodeViewBase dst)
                    {
                        if (children.ContainsKey(src.NodeId) && !children[src.NodeId].Contains(dst.NodeId))
                            children[src.NodeId].Add(dst.NodeId);
                    }
                }
            });

            // BFS from start node to assign layers
            var depth = new Dictionary<int, int>();
            var queue = new Queue<int>();

            if (nodeViews.ContainsKey(startNodeId))
            {
                queue.Enqueue(startNodeId);
                depth[startNodeId] = 0;
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int d = depth[current];

                if (!children.ContainsKey(current)) continue;
                foreach (int child in children[current])
                {
                    if (!depth.ContainsKey(child))
                    {
                        depth[child] = d + 1;
                        queue.Enqueue(child);
                    }
                }
            }

            // Assign unvisited nodes to max depth + 1
            int maxDepth = 0;
            foreach (var d in depth.Values)
                if (d > maxDepth) maxDepth = d;

            foreach (var kv in nodeViews)
            {
                if (!depth.ContainsKey(kv.Key))
                    depth[kv.Key] = maxDepth + 1;
            }

            // Group by depth
            var layers = new Dictionary<int, List<int>>();
            foreach (var kv in depth)
            {
                if (!layers.ContainsKey(kv.Value))
                    layers[kv.Value] = new List<int>();
                layers[kv.Value].Add(kv.Key);
            }

            // Position nodes
            foreach (var kv in layers)
            {
                int layer = kv.Key;
                var nodesInLayer = kv.Value;
                float totalWidth = nodesInLayer.Count * (NodeWidth + HorizontalSpacing) - HorizontalSpacing;
                float startX = -totalWidth / 2f + 400f; // center offset

                for (int i = 0; i < nodesInLayer.Count; i++)
                {
                    int nodeId = nodesInLayer[i];
                    if (!nodeViews.TryGetValue(nodeId, out var view)) continue;

                    float x = startX + i * (NodeWidth + HorizontalSpacing);
                    float y = 50f + layer * (NodeHeight + VerticalSpacing);
                    view.SetPosition(new Rect(x, y, NodeWidth, NodeHeight));
                }
            }
        }
    }
}
#endif
