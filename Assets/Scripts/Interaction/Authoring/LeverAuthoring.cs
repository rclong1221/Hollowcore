using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// Authoring component for lever interactables.
    /// EPIC 13.17.6-13.17.9: Enhanced with audio, animator, and toggle support.
    /// </summary>
    [RequireComponent(typeof(InteractableAuthoring))]
    public class LeverAuthoring : MonoBehaviour
    {
        [Header("Lever Settings")]
        [Tooltip("Entity this lever controls")]
        public GameObject TargetObject;

        [Tooltip("Event name to fire on toggle")]
        public string TargetEvent = "Toggle";

        [Tooltip("Initial activation state")]
        public bool StartActivated = false;

        [Header("Animation")]
        [Tooltip("Duration of toggle animation")]
        public float AnimationDuration = 0.3f;

        [Header("EPIC 13.17.6: Audio Feedback")]
        [Tooltip("Audio clips to play on lever interaction")]
        public AudioClip[] InteractAudioClips;

        [Tooltip("Audio volume")]
        [Range(0f, 1f)]
        public float AudioVolume = 1f;

        [Header("EPIC 13.17.7: Single Interact")]
        [Tooltip("Can only be toggled once")]
        public bool SingleInteract = false;

        [Header("EPIC 13.17.8: Multi-Switch Group")]
        [Tooltip("Group ID for exclusive state (0 = not grouped)")]
        public int SwitchGroupID = 0;

        [Header("EPIC 13.17.9: Animator Parameters")]
        [Tooltip("Animator bool parameter for lever state")]
        public string BoolParameterName = "IsActivated";

        [Tooltip("Animator trigger parameter on interaction")]
        public string TriggerParameterName;

        public class Baker : Baker<LeverAuthoring>
        {
            public override void Bake(LeverAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                Entity targetEntity = Entity.Null;
                if (authoring.TargetObject != null)
                {
                    targetEntity = GetEntity(authoring.TargetObject, TransformUsageFlags.Dynamic);
                }

                // Compute parameter hashes at bake time
                int boolHash = string.IsNullOrEmpty(authoring.BoolParameterName)
                    ? 0
                    : Animator.StringToHash(authoring.BoolParameterName);

                int triggerHash = string.IsNullOrEmpty(authoring.TriggerParameterName)
                    ? 0
                    : Animator.StringToHash(authoring.TriggerParameterName);

                AddComponent(entity, new LeverInteractable
                {
                    TargetEntity = targetEntity,
                    TargetEvent = authoring.TargetEvent,
                    IsActivated = authoring.StartActivated
                });

                AddComponent(entity, new AnimatedInteractable
                {
                    IsOpen = authoring.StartActivated,
                    AnimationDuration = authoring.AnimationDuration,
                    CurrentTime = 0f,
                    IsAnimating = false,
                    LockPlayerDuringAnimation = false,

                    // EPIC 13.17.6: Audio
                    AudioClipIndex = -1,

                    // EPIC 13.17.7: Single interact
                    SingleInteract = authoring.SingleInteract,
                    HasInteracted = false,

                    // EPIC 13.17.8: Toggle and multi-switch (levers always toggle)
                    ToggleBoolValue = true,
                    IsActiveBoolInteractable = authoring.StartActivated,
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
                        PitchVariation = 0f,
                        SequentialCycle = true
                    });

                    var buffer = AddBuffer<InteractableAudioClipElement>(entity);
                    for (int i = 0; i < authoring.InteractAudioClips.Length; i++)
                    {
                        buffer.Add(new InteractableAudioClipElement { ClipIndex = i });
                    }
                }
            }
        }
    }
}
