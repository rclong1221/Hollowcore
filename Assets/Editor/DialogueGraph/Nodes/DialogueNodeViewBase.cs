#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Base class for all dialogue graph node views.
    /// Provides standard input/output ports and node ID tracking.
    /// </summary>
    public abstract class DialogueNodeViewBase : Node
    {
        public int NodeId { get; protected set; }
        public DialogueNodeType NodeType { get; protected set; }
        public Port InputPort { get; protected set; }
        public Port OutputPort { get; protected set; }

        protected DialogueNodeViewBase(DialogueNodeType nodeType, int nodeId)
        {
            NodeId = nodeId;
            NodeType = nodeType;
            title = $"{nodeType} ({nodeId})";

            // Input port (all nodes except optionally start)
            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            ApplyNodeColor(nodeType);
        }

        /// <summary>
        /// Mark this node visually as the start node.
        /// </summary>
        public void MarkAsStart()
        {
            var badge = new Label("START") { name = "start-badge" };
            badge.style.backgroundColor = new Color(0.1f, 0.7f, 0.1f, 0.8f);
            badge.style.color = Color.white;
            badge.style.fontSize = 10;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.paddingLeft = 4;
            badge.style.paddingRight = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            titleContainer.Add(badge);
        }

        /// <summary>Load data from a DialogueNode struct into this view's fields.</summary>
        public abstract void LoadFromDialogueNode(ref DialogueNode node);

        /// <summary>Save this view's data back to a DialogueNode struct.</summary>
        public abstract DialogueNode SaveToDialogueNode(Dictionary<Port, int> edgeMap);

        protected int GetEdgeTarget(Dictionary<Port, int> edgeMap, Port port)
        {
            if (port != null && edgeMap.TryGetValue(port, out int targetId))
                return targetId;
            return 0;
        }

        protected Port CreateOutputPort(string name, Port.Capacity capacity = Port.Capacity.Single)
        {
            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, capacity, typeof(float));
            port.portName = name;
            outputContainer.Add(port);
            return port;
        }

        private void ApplyNodeColor(DialogueNodeType nodeType)
        {
            Color color = nodeType switch
            {
                DialogueNodeType.Speech => new Color(0.2f, 0.4f, 0.8f),        // Blue
                DialogueNodeType.PlayerChoice => new Color(0.2f, 0.7f, 0.3f),   // Green
                DialogueNodeType.Condition => new Color(0.9f, 0.6f, 0.1f),      // Orange
                DialogueNodeType.Action => new Color(0.6f, 0.3f, 0.8f),         // Purple
                DialogueNodeType.Random => new Color(0.9f, 0.8f, 0.1f),         // Yellow
                DialogueNodeType.End => new Color(0.7f, 0.2f, 0.2f),            // Red
                DialogueNodeType.Hub => new Color(0.5f, 0.5f, 0.5f),            // Gray
                _ => new Color(0.4f, 0.4f, 0.4f)
            };

            titleContainer.style.backgroundColor = new StyleColor(color);
        }
    }
}
#endif
