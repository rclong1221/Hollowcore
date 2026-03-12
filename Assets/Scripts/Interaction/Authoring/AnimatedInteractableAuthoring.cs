using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 13.17.6-13.17.9: Enhanced authoring for animated interactables.
    /// Provides audio feedback, trigger/bool parameters, toggle behavior,
    /// single-interact mode, and multi-switch group support.
    /// </summary>
    [RequireComponent(typeof(InteractableAuthoring))]
    public class AnimatedInteractableAuthoring : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Duration of the interaction animation")]
        public float AnimationDuration = 0.5f;

        [Tooltip("Lock player movement during animation")]
        public bool LockPlayerDuringAnimation = false;

        [Header("EPIC 13.17.7: Single Interact")]
        [Tooltip("Can only be interacted with once")]
        public bool SingleInteract = false;

        [Header("EPIC 13.17.8: Bool Parameter")]
        [Tooltip("Animator bool parameter name (leave empty to skip)")]
        public string BoolParameterName;

        [Tooltip("Initial bool value when interacted")]
        public bool InitialBoolValue = true;

        [Tooltip("Toggle bool value after each interaction")]
        public bool ToggleBoolValue = true;

        [Header("EPIC 13.17.8: UI Messages")]
        [Tooltip("Message shown when bool is enabled/open")]
        public string EnabledMessage = "Close";

        [Tooltip("Message shown when bool is disabled/closed")]
        public string DisabledMessage = "Open";

        [Header("EPIC 13.17.8: Multi-Switch Group")]
        [Tooltip("Group ID for exclusive state (0 = not grouped)")]
        public int SwitchGroupID = 0;

        [Header("EPIC 13.17.9: Trigger Parameter")]
        [Tooltip("Animator trigger parameter name (leave empty to skip)")]
        public string TriggerParameterName;

        [Header("EPIC 13.17.6: Audio Feedback")]
        [Tooltip("Audio clips to play on interaction (cycles sequentially)")]
        public AudioClip[] InteractAudioClips;

        [Tooltip("Audio volume")]
        [Range(0f, 1f)]
        public float AudioVolume = 1f;

        [Tooltip("Pitch variation (+/- from 1.0)")]
        [Range(0f, 0.5f)]
        public float PitchVariation = 0f;

        [Tooltip("Cycle clips sequentially (vs random)")]
        public bool SequentialCycle = true;

        private void OnValidate()
        {
            // Ensure animation duration is positive
            if (AnimationDuration < 0.01f)
                AnimationDuration = 0.01f;

            // Ensure volume is valid
            AudioVolume = Mathf.Clamp01(AudioVolume);
        }

        public class Baker : Baker<AnimatedInteractableAuthoring>
        {
            public override void Bake(AnimatedInteractableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Compute parameter hashes at bake time
                int boolHash = string.IsNullOrEmpty(authoring.BoolParameterName)
                    ? 0
                    : Animator.StringToHash(authoring.BoolParameterName);

                int triggerHash = string.IsNullOrEmpty(authoring.TriggerParameterName)
                    ? 0
                    : Animator.StringToHash(authoring.TriggerParameterName);

                // Add AnimatedInteractable with all EPIC 13.17.6-13.17.9 fields
                AddComponent(entity, new AnimatedInteractable
                {
                    IsOpen = false,
                    AnimationDuration = authoring.AnimationDuration,
                    CurrentTime = 0f,
                    IsAnimating = false,
                    LockPlayerDuringAnimation = authoring.LockPlayerDuringAnimation,

                    // EPIC 13.17.6: Audio
                    AudioClipIndex = -1,

                    // EPIC 13.17.7: Single interact
                    SingleInteract = authoring.SingleInteract,
                    HasInteracted = false,

                    // EPIC 13.17.8: Toggle and multi-switch
                    ToggleBoolValue = authoring.ToggleBoolValue,
                    IsActiveBoolInteractable = false,
                    SwitchGroupID = authoring.SwitchGroupID,

                    // EPIC 13.17.9: Animator parameters
                    TriggerParameterHash = triggerHash,
                    BoolParameterHash = boolHash
                });

                // Add audio config if clips provided
                if (authoring.InteractAudioClips != null && authoring.InteractAudioClips.Length > 0)
                {
                    AddComponent(entity, new InteractableAudioConfig
                    {
                        ClipCount = authoring.InteractAudioClips.Length,
                        Volume = authoring.AudioVolume,
                        PitchVariation = authoring.PitchVariation,
                        SequentialCycle = authoring.SequentialCycle
                    });

                    // Add buffer for clip indices
                    var buffer = AddBuffer<InteractableAudioClipElement>(entity);
                    for (int i = 0; i < authoring.InteractAudioClips.Length; i++)
                    {
                        buffer.Add(new InteractableAudioClipElement { ClipIndex = i });
                    }
                }

                // Add message config if messages provided
                if (!string.IsNullOrEmpty(authoring.EnabledMessage) ||
                    !string.IsNullOrEmpty(authoring.DisabledMessage))
                {
                    AddComponent(entity, new InteractableMessageConfig
                    {
                        EnabledMessage = authoring.EnabledMessage ?? "",
                        DisabledMessage = authoring.DisabledMessage ?? ""
                    });
                }
            }
        }
    }
}
