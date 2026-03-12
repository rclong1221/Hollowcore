using Unity.Entities;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Burst-compatible BlobAsset for a single dialogue tree.
    /// Built by DialogueTreeSO.BakeToBlob().
    /// </summary>
    public struct DialogueTreeBlob
    {
        public int TreeId;
        public int StartNodeId;
        public BlobArray<DialogueNodeBlob> Nodes;
        public BlobArray<DialogueChoiceBlob> Choices;
        public BlobArray<DialogueActionBlob> Actions;
        public BlobArray<DialogueRandomEntryBlob> RandomEntries;

        /// <summary>
        /// Finds the array index of a node by NodeId. Linear scan — trees have &lt;50 nodes.
        /// Returns -1 if not found.
        /// </summary>
        public int FindNodeIndex(int nodeId)
        {
            for (int i = 0; i < Nodes.Length; i++)
                if (Nodes[i].NodeId == nodeId) return i;
            return -1;
        }
    }

    public struct DialogueNodeBlob
    {
        public int NodeId;
        public byte NodeType;
        public int NextNodeId;
        public int SpeakerNameHash;
        public int TextHash;
        public float Duration;
        public byte CameraMode;
        public byte ConditionType;
        public int ConditionValue;
        public int TrueNodeId;
        public int FalseNodeId;
        public int ChoiceStart;
        public byte ChoiceCount;
        public int ActionStart;
        public byte ActionCount;
        public int RandomStart;
        public byte RandomCount;
    }

    public struct DialogueChoiceBlob
    {
        public int ChoiceIndex;
        public int NextNodeId;
        public byte ConditionType;
        public int ConditionValue;
    }

    public struct DialogueActionBlob
    {
        public byte ActionType;
        public int IntValue;
        public int IntValue2;
    }

    public struct DialogueRandomEntryBlob
    {
        public int NodeId;
        public float Weight;
    }

    /// <summary>
    /// EPIC 16.16: Global registry blob containing all dialogue trees.
    /// Built by DialogueBootstrapSystem.
    /// </summary>
    public struct DialogueRegistryBlob
    {
        public BlobArray<DialogueRegistryEntry> Entries;
    }

    public struct DialogueRegistryEntry
    {
        public int TreeId;
        public int TreeBlobIndex;
    }
}
