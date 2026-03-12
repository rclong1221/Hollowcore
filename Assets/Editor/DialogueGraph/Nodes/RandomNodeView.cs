#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Graph node view for Random (weighted) nodes.
    /// One output port per random entry with weight labels.
    /// </summary>
    public class RandomNodeView : DialogueNodeViewBase
    {
        public List<Port> RandomPorts { get; } = new();
        private readonly List<FloatField> _weightFields = new();
        private RandomNodeEntry[] _entries;

        public RandomNodeView(int nodeId) : base(DialogueNodeType.Random, nodeId)
        {
            var addButton = new Button(AddEntry) { text = "+ Entry" };
            addButton.style.width = 80;
            titleContainer.Add(addButton);

            RefreshExpandedState();
            RefreshPorts();
        }

        private void AddEntry()
        {
            int idx = RandomPorts.Count;
            var port = CreateOutputPort($"Rnd {idx}");
            RandomPorts.Add(port);

            var weightField = new FloatField($"Weight {idx}") { value = 1f };
            extensionContainer.Add(weightField);
            _weightFields.Add(weightField);

            RefreshExpandedState();
            RefreshPorts();
        }

        public override void LoadFromDialogueNode(ref DialogueNode node)
        {
            _entries = node.RandomEntries;
            if (node.RandomEntries == null) return;

            for (int i = 0; i < node.RandomEntries.Length; i++)
            {
                AddEntry();
                _weightFields[i].value = node.RandomEntries[i].Weight;
            }
        }

        public override DialogueNode SaveToDialogueNode(Dictionary<Port, int> edgeMap)
        {
            var entries = new RandomNodeEntry[RandomPorts.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = new RandomNodeEntry
                {
                    NodeId = GetEdgeTarget(edgeMap, RandomPorts[i]),
                    Weight = i < _weightFields.Count ? _weightFields[i].value : 1f
                };
            }

            return new DialogueNode
            {
                NodeId = NodeId,
                NodeType = DialogueNodeType.Random,
                RandomEntries = entries
            };
        }
    }
}
#endif
