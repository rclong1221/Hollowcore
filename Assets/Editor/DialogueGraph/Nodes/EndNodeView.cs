#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Graph node view for End (terminal) nodes.
    /// No output ports.
    /// </summary>
    public class EndNodeView : DialogueNodeViewBase
    {
        public EndNodeView(int nodeId) : base(DialogueNodeType.End, nodeId)
        {
            // No output port — this is a terminal node
            RefreshExpandedState();
            RefreshPorts();
        }

        public override void LoadFromDialogueNode(ref DialogueNode node) { }

        public override DialogueNode SaveToDialogueNode(Dictionary<Port, int> edgeMap)
        {
            return new DialogueNode
            {
                NodeId = NodeId,
                NodeType = DialogueNodeType.End
            };
        }
    }
}
#endif
