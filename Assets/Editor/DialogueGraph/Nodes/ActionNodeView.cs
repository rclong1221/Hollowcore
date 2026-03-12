#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Graph node view for Action nodes.
    /// Displays action type and parameters.
    /// </summary>
    public class ActionNodeView : DialogueNodeViewBase
    {
        private DialogueAction[] _actions;

        public ActionNodeView(int nodeId) : base(DialogueNodeType.Action, nodeId)
        {
            OutputPort = CreateOutputPort("Next");

            RefreshExpandedState();
            RefreshPorts();
        }

        public override void LoadFromDialogueNode(ref DialogueNode node)
        {
            _actions = node.Actions;

            // Display action summary
            if (node.Actions != null)
            {
                for (int i = 0; i < node.Actions.Length; i++)
                {
                    var label = new Label($"  {node.Actions[i].ActionType} ({node.Actions[i].IntValue}, {node.Actions[i].IntValue2})");
                    label.style.fontSize = 10;
                    extensionContainer.Add(label);
                }
            }

            RefreshExpandedState();
        }

        public override DialogueNode SaveToDialogueNode(Dictionary<Port, int> edgeMap)
        {
            return new DialogueNode
            {
                NodeId = NodeId,
                NodeType = DialogueNodeType.Action,
                Actions = _actions,
                NextNodeId = GetEdgeTarget(edgeMap, OutputPort)
            };
        }
    }
}
#endif
