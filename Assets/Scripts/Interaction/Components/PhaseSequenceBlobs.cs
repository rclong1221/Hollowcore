using Unity.Collections;
using Unity.Entities;

namespace DIG.Interaction
{
    /// <summary>
    /// EPIC 16.1 Phase 3: Type of a single phase within a multi-phase interaction.
    /// </summary>
    public enum PhaseType : byte
    {
        /// <summary>Completes instantly, auto-advances to next phase.</summary>
        Instant = 0,
        /// <summary>Requires waiting for Duration seconds.</summary>
        Timed = 1,
        /// <summary>Requires completing an input sequence (combo).</summary>
        InputSequence = 2
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: Expected input for a single step in an input sequence.
    /// </summary>
    public enum SequenceInput : byte
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
        Interact = 5,
        Fire = 6,
        Reload = 7,
        Jump = 8
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: Definition of a single phase within a multi-phase interaction.
    /// Stored in a BlobArray inside PhaseSequenceBlob.
    /// </summary>
    public struct PhaseDefinition
    {
        /// <summary>How this phase is completed.</summary>
        public PhaseType Type;

        /// <summary>Duration in seconds for Timed phases. Ignored for Instant/InputSequence.</summary>
        public float Duration;

        /// <summary>Localization key for the UI prompt during this phase.</summary>
        public FixedString32Bytes PromptKey;

        /// <summary>
        /// Index into InputSequenceBlob.Sequences for InputSequence phases.
        /// -1 = not an input sequence phase.
        /// </summary>
        public int InputSequenceIndex;
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: BlobAsset defining the full multi-phase interaction sequence.
    /// Immutable, Burst-friendly, shared across all instances of the same interactable type.
    /// </summary>
    public struct PhaseSequenceBlob
    {
        /// <summary>Ordered list of phases to complete.</summary>
        public BlobArray<PhaseDefinition> Phases;

        /// <summary>If true, failing any phase resets to phase 0 instead of cancelling.</summary>
        public bool ResetOnFail;

        /// <summary>Maximum total time for the entire sequence. 0 = no limit.</summary>
        public float TotalTimeLimit;
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: A single step in an input sequence combo.
    /// </summary>
    public struct InputStep
    {
        /// <summary>The input the player must provide.</summary>
        public SequenceInput ExpectedInput;

        /// <summary>
        /// Max time allowed for this step. 0 = use sequence default timeout.
        /// </summary>
        public float TimeLimit;
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: Definition of a single input sequence (e.g., UpDownLeftRightUp).
    /// </summary>
    public struct SequenceDefinitionBlob
    {
        /// <summary>Ordered list of input steps to complete.</summary>
        public BlobArray<InputStep> Steps;

        /// <summary>Default timeout per step if InputStep.TimeLimit is 0.</summary>
        public float DefaultStepTimeout;
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: BlobAsset containing all input sequences for a multi-phase interactable.
    /// A single interactable may have multiple sequences (one per InputSequence phase).
    /// </summary>
    public struct InputSequenceBlob
    {
        /// <summary>Array of sequence definitions, indexed by PhaseDefinition.InputSequenceIndex.</summary>
        public BlobArray<SequenceDefinitionBlob> Sequences;
    }
}
