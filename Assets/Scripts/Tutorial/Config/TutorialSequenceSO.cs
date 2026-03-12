using UnityEngine;

namespace DIG.Tutorial.Config
{
    /// <summary>
    /// EPIC 18.4: Ordered sequence of tutorial steps with metadata.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Tutorial/Sequence", fileName = "NewTutorialSequence")]
    public class TutorialSequenceSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier referenced by TutorialTriggerAuthoring and TutorialService API.")]
        public string SequenceId;
        public string DisplayName;

        [Header("Steps")]
        public TutorialStepSO[] Steps;

        [Header("Behavior")]
        [Tooltip("Prerequisite condition that must be met for auto-start.")]
        public TutorialConditionSO Prerequisite;
        [Tooltip("Start automatically when prerequisite is satisfied.")]
        public bool AutoStart;
        [Tooltip("Allow the player to skip this entire sequence.")]
        public bool CanSkip = true;
        [Tooltip("Higher priority wins when multiple tutorials trigger simultaneously.")]
        public int Priority;

        [Header("Persistence")]
        [Tooltip("PlayerPrefs key for completion persistence. Auto-generated from SequenceId if empty.")]
        public string SaveKey;

        public string GetSaveKey() => string.IsNullOrEmpty(SaveKey) ? $"Tutorial_{SequenceId}" : SaveKey;
    }
}
