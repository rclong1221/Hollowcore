using System;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: A single node in a dialogue tree.
    /// Serialized inline in DialogueTreeSO.Nodes[].
    /// </summary>
    [Serializable]
    public struct DialogueNode
    {
        public int NodeId;
        public DialogueNodeType NodeType;

        [Header("Speech")]
        public string SpeakerName;
        public string Text;
        public string AudioClipPath;
        [Tooltip("Auto-advance seconds. 0 = wait for input.")]
        public float Duration;

        [Header("Presentation (EPIC 18.5)")]
        [Tooltip("Portrait expression key for this line (e.g., neutral, happy, angry).")]
        public string Expression;
        [Tooltip("Voice-over audio clip for this line.")]
        public AudioClip VoiceClip;
        [Tooltip("Override typewriter speed (chars/sec). 0 = use global default from DialogueConfig.")]
        [Min(0f)] public float TypewriterSpeed;

        [Header("Navigation")]
        public int NextNodeId;

        [Header("Choices (PlayerChoice nodes)")]
        public DialogueChoice[] Choices;

        [Header("Actions (Action nodes)")]
        public DialogueAction[] Actions;

        [Header("Condition (Condition nodes)")]
        public DialogueConditionType ConditionType;
        public int ConditionValue;
        public int TrueNodeId;
        public int FalseNodeId;

        [Header("Random (Random nodes)")]
        public RandomNodeEntry[] RandomEntries;

        [Header("Camera")]
        public DialogueCameraMode CameraMode;
    }

    /// <summary>
    /// EPIC 16.16: A selectable choice within a PlayerChoice node.
    /// </summary>
    [Serializable]
    public struct DialogueChoice
    {
        public int ChoiceIndex;
        public string Text;
        public int NextNodeId;
        public DialogueConditionType ConditionType;
        public int ConditionValue;
    }

    /// <summary>
    /// EPIC 16.16: A game action to execute from an Action node.
    /// </summary>
    [Serializable]
    public struct DialogueAction
    {
        public DialogueActionType ActionType;
        public int IntValue;
        public int IntValue2;
    }

    /// <summary>
    /// EPIC 16.16: Weighted entry for Random node branching.
    /// </summary>
    [Serializable]
    public struct RandomNodeEntry
    {
        public int NodeId;
        [Range(0.01f, 10f)] public float Weight;
    }

    /// <summary>
    /// EPIC 16.16: Context rule for selecting which dialogue tree to use.
    /// Evaluated in order; first match wins, DefaultTreeId as fallback.
    /// </summary>
    [Serializable]
    public struct DialogueContextRule
    {
        public DialogueConditionType ConditionType;
        public int ConditionValue;
        public DialogueTreeSO Tree;
    }

    /// <summary>
    /// EPIC 16.16: A single bark line in a BarkCollectionSO pool.
    /// </summary>
    [Serializable]
    public struct BarkLine
    {
        public string Text;
        public string AudioClipPath;
        [Range(0.01f, 10f)] public float Weight;
        public DialogueConditionType ConditionType;
        public int ConditionValue;
    }
}
