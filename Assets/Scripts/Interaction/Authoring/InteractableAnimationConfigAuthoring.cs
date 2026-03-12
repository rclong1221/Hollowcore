using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 13.17.1: Authoring component for animation-driven interactions.
    /// Add this to interactables that should wait for animation events.
    /// </summary>
    public class InteractableAnimationConfigAuthoring : MonoBehaviour
    {
        [Header("Animation Event Settings")]
        [Tooltip("Wait for OnAnimatorInteract event before triggering the interaction effect")]
        public bool WaitForAnimStart = true;

        [Tooltip("Wait for OnAnimatorInteractComplete event before ending the interaction")]
        public bool WaitForAnimComplete = true;

        [Header("Timeout")]
        [Tooltip("Maximum time to wait for animation events (fallback in case of missed events)")]
        [Range(0.5f, 10f)]
        public float AnimEventTimeout = 2f;

        [Header("Animator")]
        [Tooltip("Integer parameter value to pass to the animator for this interaction type")]
        public int AnimatorIntData = 0;

        public class Baker : Baker<InteractableAnimationConfigAuthoring>
        {
            public override void Bake(InteractableAnimationConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new InteractableAnimationConfig
                {
                    WaitForAnimStart = authoring.WaitForAnimStart,
                    WaitForAnimComplete = authoring.WaitForAnimComplete,
                    AnimEventTimeout = authoring.AnimEventTimeout,
                    AnimatorIntData = authoring.AnimatorIntData
                });
            }
        }
    }
}
