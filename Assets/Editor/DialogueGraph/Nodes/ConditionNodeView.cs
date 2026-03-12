#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Graph node view for Condition (if/else branch) nodes.
    /// Two output ports: True and False.
    /// </summary>
    public class ConditionNodeView : DialogueNodeViewBase
    {
        public Port TruePort { get; private set; }
        public Port FalsePort { get; private set; }

        private EnumField _conditionTypeField;
        private IntegerField _conditionValueField;

        public ConditionNodeView(int nodeId) : base(DialogueNodeType.Condition, nodeId)
        {
            TruePort = CreateOutputPort("True");
            TruePort.portColor = new Color(0.2f, 0.8f, 0.2f);

            FalsePort = CreateOutputPort("False");
            FalsePort.portColor = new Color(0.8f, 0.2f, 0.2f);

            _conditionTypeField = new EnumField("Condition", DialogueConditionType.None);
            extensionContainer.Add(_conditionTypeField);

            _conditionValueField = new IntegerField("Value") { value = 0 };
            extensionContainer.Add(_conditionValueField);

            RefreshExpandedState();
            RefreshPorts();
        }

        public override void LoadFromDialogueNode(ref DialogueNode node)
        {
            _conditionTypeField.value = node.ConditionType;
            _conditionValueField.value = node.ConditionValue;
        }

        public override DialogueNode SaveToDialogueNode(Dictionary<Port, int> edgeMap)
        {
            return new DialogueNode
            {
                NodeId = NodeId,
                NodeType = DialogueNodeType.Condition,
                ConditionType = (DialogueConditionType)_conditionTypeField.value,
                ConditionValue = _conditionValueField.value,
                TrueNodeId = GetEdgeTarget(edgeMap, TruePort),
                FalseNodeId = GetEdgeTarget(edgeMap, FalsePort)
            };
        }
    }
}
#endif
