#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Graph node view for PlayerChoice nodes.
    /// Displays choice text + one output port per choice.
    /// </summary>
    public class ChoiceNodeView : DialogueNodeViewBase
    {
        public List<Port> ChoicePorts { get; } = new();
        private readonly List<TextField> _choiceTextFields = new();
        private DialogueChoice[] _choices;

        public ChoiceNodeView(int nodeId) : base(DialogueNodeType.PlayerChoice, nodeId)
        {
            var addButton = new Button(AddChoice) { text = "+ Choice" };
            addButton.style.width = 80;
            titleContainer.Add(addButton);

            RefreshExpandedState();
            RefreshPorts();
        }

        private void AddChoice()
        {
            int idx = ChoicePorts.Count;
            var port = CreateOutputPort($"Choice {idx}");
            ChoicePorts.Add(port);

            var textField = new TextField($"Text {idx}") { value = "" };
            textField.style.maxWidth = 250;
            extensionContainer.Add(textField);
            _choiceTextFields.Add(textField);

            RefreshExpandedState();
            RefreshPorts();
        }

        public override void LoadFromDialogueNode(ref DialogueNode node)
        {
            _choices = node.Choices;
            if (node.Choices == null) return;

            for (int i = 0; i < node.Choices.Length; i++)
            {
                AddChoice();
                _choiceTextFields[i].value = node.Choices[i].Text ?? "";
            }
        }

        public override DialogueNode SaveToDialogueNode(Dictionary<Port, int> edgeMap)
        {
            var choices = new DialogueChoice[ChoicePorts.Count];
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i] = new DialogueChoice
                {
                    ChoiceIndex = i,
                    Text = i < _choiceTextFields.Count ? _choiceTextFields[i].value : "",
                    NextNodeId = GetEdgeTarget(edgeMap, ChoicePorts[i]),
                    ConditionType = _choices != null && i < _choices.Length ? _choices[i].ConditionType : DialogueConditionType.None,
                    ConditionValue = _choices != null && i < _choices.Length ? _choices[i].ConditionValue : 0
                };
            }

            return new DialogueNode
            {
                NodeId = NodeId,
                NodeType = DialogueNodeType.PlayerChoice,
                Choices = choices
            };
        }
    }
}
#endif
