using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction
{
    /// <summary>
    /// EPIC 16.1 Phase 3: Non-ghost config holding the phase sequence BlobAsset.
    /// BlobAssetReference cannot be ghost-replicated; baked identically on all clients.
    /// Placed on the INTERACTABLE entity.
    /// </summary>
    public struct InteractionPhaseConfig : IComponentData
    {
        public BlobAssetReference<PhaseSequenceBlob> PhaseBlob;
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: Non-ghost config holding the input sequence BlobAsset.
    /// Placed on the INTERACTABLE entity alongside InteractionPhaseConfig.
    /// </summary>
    public struct InputSequenceConfig : IComponentData
    {
        public BlobAssetReference<InputSequenceBlob> SequenceBlob;
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: Runtime state for multi-phase interaction progression.
    /// Ghost-replicated so all clients see the current phase.
    /// Placed on the INTERACTABLE entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct InteractionPhaseState : IComponentData
    {
        /// <summary>Current phase index. -1 = inactive.</summary>
        [GhostField]
        public int CurrentPhase;

        /// <summary>Time elapsed in the current phase.</summary>
        [GhostField(Quantization = 100)]
        public float PhaseTimeElapsed;

        /// <summary>Total time elapsed across all phases.</summary>
        [GhostField(Quantization = 100)]
        public float TotalTimeElapsed;

        /// <summary>Whether the multi-phase sequence is currently active.</summary>
        [GhostField]
        public bool IsActive;

        /// <summary>Whether the current phase has failed (wrong input, timeout).</summary>
        [GhostField]
        public bool PhaseFailed;

        /// <summary>Whether the entire sequence has been completed successfully.</summary>
        [GhostField]
        public bool SequenceComplete;

        /// <summary>The entity performing this multi-phase interaction (the player).</summary>
        [GhostField]
        public Entity PerformingEntity;
    }

    /// <summary>
    /// EPIC 16.1 Phase 3: Runtime state for input sequence (combo) validation.
    /// Ghost-replicated so all clients see progress.
    /// Placed on the INTERACTABLE entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct InputSequenceState : IComponentData
    {
        /// <summary>Which sequence definition is currently active (index into InputSequenceBlob.Sequences).</summary>
        [GhostField]
        public int ActiveSequenceIndex;

        /// <summary>Current step index within the active sequence.</summary>
        [GhostField]
        public int CurrentInputIndex;

        /// <summary>Time since last correct input (for timeout detection).</summary>
        [GhostField(Quantization = 100)]
        public float TimeSinceLastInput;

        /// <summary>Whether all inputs in the sequence have been matched.</summary>
        [GhostField]
        public bool SequenceComplete;

        /// <summary>Whether the sequence has failed (wrong input or timeout).</summary>
        [GhostField]
        public bool SequenceFailed;

        /// <summary>
        /// Previous frame's detected input for edge detection.
        /// NOT ghost-replicated — local-only for input processing.
        /// </summary>
        public SequenceInput PreviousInput;
    }
}
