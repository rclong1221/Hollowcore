using UnityEngine;

namespace DIG.Tutorial.Config
{
    /// <summary>
    /// EPIC 18.4: Definition of a single tutorial step within a sequence.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Tutorial/Step", fileName = "NewTutorialStep")]
    public class TutorialStepSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID within the sequence.")]
        public string StepId;

        [Header("Presentation")]
        public TutorialStepType StepType = TutorialStepType.Popup;
        public string Title;
        [TextArea(2, 5)]
        public string Message;

        [Header("Targeting")]
        [Tooltip("UI Toolkit element name for Highlight/Tooltip steps.")]
        public string TargetElementName;
        [Tooltip("Tag to find world-space target for WorldMarker steps.")]
        public string WorldTargetTag;

        [Header("Completion")]
        public CompletionCondition CompletionCondition = CompletionCondition.ManualContinue;
        [Tooltip("Parameter for completion (action name, screen ID, event key, etc.).")]
        public string CompletionParam;
        [Tooltip("Auto-advance after this many seconds. 0 = no timeout.")]
        public float TimeoutSeconds;

        [Header("Highlight")]
        [Tooltip("Pixels of padding around the highlighted element.")]
        public float HighlightPadding = 20f;

        [Header("Branching")]
        [Tooltip("Override next step ID instead of sequential. Empty = next in array.")]
        public string NextStepId;
        [Tooltip("Condition for Branch-type steps.")]
        public TutorialConditionSO BranchCondition;
        public string TrueStepId;
        public string FalseStepId;

        [Header("Audio")]
        public AudioClip Sound;
    }
}
