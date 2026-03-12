using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: A branching dialogue tree authored as a ScriptableObject.
    /// Contains all nodes inline. Baked to BlobAsset at runtime for Burst-compatible traversal.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueTree", menuName = "DIG/Dialogue/Dialogue Tree", order = 0)]
    public class DialogueTreeSO : ScriptableObject
    {
        [Tooltip("Unique identifier referenced by DialogueSpeakerData.")]
        public int TreeId;

        [Tooltip("Editor label only.")]
        public string DisplayName;

        [Tooltip("Entry point node ID.")]
        public int StartNodeId;

        [Tooltip("All nodes in this tree.")]
        public DialogueNode[] Nodes = new DialogueNode[0];

        [Header("Priority (EPIC 18.5)")]
        [Tooltip("Priority level for interrupt/queue behavior.")]
        public DialoguePriority Priority = DialoguePriority.Exploration;

        [Tooltip("What happens when this dialogue is interrupted by higher priority.")]
        public InterruptBehavior InterruptBehavior = InterruptBehavior.Discard;

        [HideInInspector]
        [Tooltip("Parallel array for editor-only node layout positions.")]
        public Vector2[] NodeEditorPositions = new Vector2[0];

        /// <summary>
        /// Builds a BlobAssetReference for Burst-compatible runtime traversal.
        /// Follows ProceduralMotionProfile.BakeToBlob() pattern.
        /// </summary>
        public BlobAssetReference<DialogueTreeBlob> BakeToBlob()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var blob = ref builder.ConstructRoot<DialogueTreeBlob>();

            blob.TreeId = TreeId;
            blob.StartNodeId = StartNodeId;

            // Count totals for flat arrays
            int totalChoices = 0;
            int totalActions = 0;
            int totalRandomEntries = 0;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (Nodes[i].Choices != null) totalChoices += Nodes[i].Choices.Length;
                if (Nodes[i].Actions != null) totalActions += Nodes[i].Actions.Length;
                if (Nodes[i].RandomEntries != null) totalRandomEntries += Nodes[i].RandomEntries.Length;
            }

            // Allocate flat arrays
            var nodes = builder.Allocate(ref blob.Nodes, Nodes.Length);
            var choices = builder.Allocate(ref blob.Choices, totalChoices);
            var actions = builder.Allocate(ref blob.Actions, totalActions);
            var randoms = builder.Allocate(ref blob.RandomEntries, totalRandomEntries);

            int choiceOffset = 0;
            int actionOffset = 0;
            int randomOffset = 0;

            for (int i = 0; i < Nodes.Length; i++)
            {
                ref var src = ref Nodes[i];
                nodes[i] = new DialogueNodeBlob
                {
                    NodeId = src.NodeId,
                    NodeType = (byte)src.NodeType,
                    NextNodeId = src.NextNodeId,
                    SpeakerNameHash = src.SpeakerName != null ? src.SpeakerName.GetHashCode() : 0,
                    TextHash = src.Text != null ? src.Text.GetHashCode() : 0,
                    Duration = src.Duration,
                    CameraMode = (byte)src.CameraMode,
                    ConditionType = (byte)src.ConditionType,
                    ConditionValue = src.ConditionValue,
                    TrueNodeId = src.TrueNodeId,
                    FalseNodeId = src.FalseNodeId,
                    ChoiceStart = choiceOffset,
                    ChoiceCount = (byte)(src.Choices != null ? src.Choices.Length : 0),
                    ActionStart = actionOffset,
                    ActionCount = (byte)(src.Actions != null ? src.Actions.Length : 0),
                    RandomStart = randomOffset,
                    RandomCount = (byte)(src.RandomEntries != null ? src.RandomEntries.Length : 0)
                };

                if (src.Choices != null)
                {
                    for (int c = 0; c < src.Choices.Length; c++)
                    {
                        choices[choiceOffset + c] = new DialogueChoiceBlob
                        {
                            ChoiceIndex = src.Choices[c].ChoiceIndex,
                            NextNodeId = src.Choices[c].NextNodeId,
                            ConditionType = (byte)src.Choices[c].ConditionType,
                            ConditionValue = src.Choices[c].ConditionValue
                        };
                    }
                    choiceOffset += src.Choices.Length;
                }

                if (src.Actions != null)
                {
                    for (int a = 0; a < src.Actions.Length; a++)
                    {
                        actions[actionOffset + a] = new DialogueActionBlob
                        {
                            ActionType = (byte)src.Actions[a].ActionType,
                            IntValue = src.Actions[a].IntValue,
                            IntValue2 = src.Actions[a].IntValue2
                        };
                    }
                    actionOffset += src.Actions.Length;
                }

                if (src.RandomEntries != null)
                {
                    for (int r = 0; r < src.RandomEntries.Length; r++)
                    {
                        randoms[randomOffset + r] = new DialogueRandomEntryBlob
                        {
                            NodeId = src.RandomEntries[r].NodeId,
                            Weight = src.RandomEntries[r].Weight
                        };
                    }
                    randomOffset += src.RandomEntries.Length;
                }
            }

            var blobRef = builder.CreateBlobAssetReference<DialogueTreeBlob>(Allocator.Persistent);
            builder.Dispose();
            return blobRef;
        }

        /// <summary>Finds the array index of a node by its NodeId. Returns -1 if not found.</summary>
        public int FindNodeIndex(int nodeId)
        {
            for (int i = 0; i < Nodes.Length; i++)
                if (Nodes[i].NodeId == nodeId) return i;
            return -1;
        }
    }
}
