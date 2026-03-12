using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 16.1 Phase 3: Authoring component for multi-phase interactables.
    ///
    /// Designer workflow:
    /// 1. Add InteractableAuthoring (Type = MultiPhase)
    /// 2. Add MultiPhaseAuthoring (configure phases and input sequences)
    /// 3. Baker creates BlobAssets and adds Config + State components
    ///
    /// Example: Bomb defusal (Instant → Timed → InputSequence → Instant)
    /// Example: Door code (single InputSequence phase)
    /// </summary>
    public class MultiPhaseAuthoring : MonoBehaviour
    {
        [Header("Phase Sequence")]
        [Tooltip("Ordered list of phases the player must complete")]
        public PhaseEntry[] Phases = Array.Empty<PhaseEntry>();

        [Tooltip("If true, failing any phase resets to phase 0 instead of cancelling")]
        public bool ResetOnFail = false;

        [Tooltip("Maximum total time for the entire sequence. 0 = no limit")]
        public float TotalTimeLimit = 0f;

        [Header("Input Sequences (for InputSequence phases)")]
        [Tooltip("Input sequences referenced by PhaseEntry.InputSequenceIndex")]
        public InputSequenceEntry[] InputSequences = Array.Empty<InputSequenceEntry>();

        [Serializable]
        public class PhaseEntry
        {
            [Tooltip("How this phase is completed")]
            public PhaseType Type = PhaseType.Instant;

            [Tooltip("Duration in seconds for Timed phases")]
            public float Duration = 1f;

            [Tooltip("Localization key for the UI prompt during this phase")]
            public string PromptKey = "";

            [Tooltip("Index into InputSequences array for InputSequence phases. -1 = none")]
            public int InputSequenceIndex = -1;
        }

        [Serializable]
        public class InputSequenceEntry
        {
            [Tooltip("Name for editor identification")]
            public string Name = "Sequence";

            [Tooltip("Ordered list of expected inputs")]
            public SequenceInput[] Steps = Array.Empty<SequenceInput>();

            [Tooltip("Default timeout per step in seconds (0 = no timeout)")]
            public float DefaultStepTimeout = 2f;

            [Tooltip("Per-step time limits (optional, overrides DefaultStepTimeout). Leave empty for uniform timeout")]
            public float[] StepTimeLimits = Array.Empty<float>();
        }

        public class Baker : Baker<MultiPhaseAuthoring>
        {
            public override void Bake(MultiPhaseAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Build phase definitions
                var phaseDefs = new PhaseDefinition[authoring.Phases.Length];
                for (int i = 0; i < authoring.Phases.Length; i++)
                {
                    var entry = authoring.Phases[i];
                    phaseDefs[i] = new PhaseDefinition
                    {
                        Type = entry.Type,
                        Duration = entry.Duration,
                        PromptKey = new FixedString32Bytes(
                            entry.PromptKey.Length > 29 ? entry.PromptKey.Substring(0, 29) : entry.PromptKey),
                        InputSequenceIndex = entry.InputSequenceIndex
                    };
                }

                // Create phase blob
                var phaseBlob = InteractionBlobBuilder.CreatePhaseBlob(
                    phaseDefs, authoring.ResetOnFail, authoring.TotalTimeLimit);

                AddComponent(entity, new InteractionPhaseConfig { PhaseBlob = phaseBlob });
                AddComponent(entity, new InteractionPhaseState
                {
                    CurrentPhase = -1,
                    IsActive = false,
                    PhaseFailed = false,
                    SequenceComplete = false,
                    PerformingEntity = Entity.Null
                });

                // Build input sequences if any
                if (authoring.InputSequences.Length > 0)
                {
                    var seqData = new InteractionBlobBuilder.SequenceAuthoringData[authoring.InputSequences.Length];
                    for (int i = 0; i < authoring.InputSequences.Length; i++)
                    {
                        var entry = authoring.InputSequences[i];
                        var steps = new InputStep[entry.Steps.Length];
                        for (int j = 0; j < entry.Steps.Length; j++)
                        {
                            float timeLimit = (entry.StepTimeLimits != null && j < entry.StepTimeLimits.Length)
                                ? entry.StepTimeLimits[j] : 0f;

                            steps[j] = new InputStep
                            {
                                ExpectedInput = entry.Steps[j],
                                TimeLimit = timeLimit
                            };
                        }

                        seqData[i] = new InteractionBlobBuilder.SequenceAuthoringData
                        {
                            Steps = steps,
                            DefaultStepTimeout = entry.DefaultStepTimeout
                        };
                    }

                    var seqBlob = InteractionBlobBuilder.CreateInputSequenceBlob(seqData);
                    AddComponent(entity, new InputSequenceConfig { SequenceBlob = seqBlob });
                    AddComponent(entity, new InputSequenceState
                    {
                        ActiveSequenceIndex = -1,
                        CurrentInputIndex = 0,
                        SequenceComplete = false,
                        SequenceFailed = false,
                        PreviousInput = SequenceInput.None
                    });
                }
            }
        }
    }
}
