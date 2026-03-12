using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// Authoring component for door interactables.
    /// EPIC 13.17.6-13.17.9: Enhanced with audio, trigger parameters, and state management.
    /// </summary>
    [RequireComponent(typeof(InteractableAuthoring))]
    public class DoorAuthoring : MonoBehaviour
    {
        [Header("Door Settings")]
        [Tooltip("Rotation angle when fully open")]
        public float OpenAngle = 90f;

        [Tooltip("Rotation angle when closed")]
        public float ClosedAngle = 0f;

        [Tooltip("Speed of door swing in degrees/second")]
        public float SwingSpeed = 180f;

        [Header("Animation")]
        [Tooltip("Duration of open/close animation")]
        public float AnimationDuration = 0.5f;

        [Tooltip("Lock player during animation")]
        public bool LockPlayerDuringAnimation = false;

        [Header("Auto-Close")]
        [Tooltip("Does the door close automatically?")]
        public bool AutoClose = false;

        [Tooltip("Delay before auto-close (seconds)")]
        public float AutoCloseDelay = 3f;

        [Header("EPIC 13.17.6: Audio Feedback")]
        [Tooltip("Audio clips to play on door interaction")]
        public AudioClip[] InteractAudioClips;

        [Tooltip("Audio volume")]
        [Range(0f, 1f)]
        public float AudioVolume = 1f;

        [Header("EPIC 13.17.7: Single Interact")]
        [Tooltip("Can only be opened once")]
        public bool SingleInteract = false;

        [Header("EPIC 13.17.9: Animator Parameters")]
        [Tooltip("Animator bool parameter for door state")]
        public string BoolParameterName = "IsOpen";

        [Tooltip("Animator trigger parameter on interaction")]
        public string TriggerParameterName;

        public class Baker : Baker<DoorAuthoring>
        {
            public override void Bake(DoorAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Compute parameter hashes at bake time
                int boolHash = string.IsNullOrEmpty(authoring.BoolParameterName)
                    ? 0
                    : Animator.StringToHash(authoring.BoolParameterName);

                int triggerHash = string.IsNullOrEmpty(authoring.TriggerParameterName)
                    ? 0
                    : Animator.StringToHash(authoring.TriggerParameterName);

                AddComponent(entity, new DoorInteractable
                {
                    OpenAngle = authoring.OpenAngle,
                    ClosedAngle = authoring.ClosedAngle,
                    SwingSpeed = authoring.SwingSpeed,
                    AutoClose = authoring.AutoClose,
                    AutoCloseDelay = authoring.AutoCloseDelay,
                    TimeSinceOpened = 0f
                });

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

                    // EPIC 13.17.8: Toggle behavior (doors always toggle)
                    ToggleBoolValue = true,
                    IsActiveBoolInteractable = false,
                    SwitchGroupID = 0,

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
                        PitchVariation = 0f,
                        SequentialCycle = true
                    });

                    var buffer = AddBuffer<InteractableAudioClipElement>(entity);
                    for (int i = 0; i < authoring.InteractAudioClips.Length; i++)
                    {
                        buffer.Add(new InteractableAudioClipElement { ClipIndex = i });
                    }
                }

                // Add state-based messages for doors
                AddComponent(entity, new InteractableMessageConfig
                {
                    EnabledMessage = "Close",
                    DisabledMessage = "Open"
                });
            }
        }
    }
}
