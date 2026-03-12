using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// Authoring component for interactable objects.
    /// </summary>
    public class InteractableAuthoring : MonoBehaviour
    {
        [Header("Interaction Type")]
        [Tooltip("How this object is interacted with")]
        public InteractableType Type = InteractableType.Instant;

        [Header("Interaction Settings")]
        [Tooltip("Can this object be interacted with?")]
        public bool CanInteract = true;

        [Tooltip("Maximum interaction distance")]
        public float InteractionRadius = 2f;

        [Tooltip("Priority for overlapping interactables (higher = preferred)")]
        public int Priority = 0;

        [Header("ID Filtering (EPIC 13.17.4)")]
        [Tooltip("Unique identifier for filtering (0 = universal, interacts with any ability)")]
        public int InteractableID = 0;

        [Header("Timed Interaction")]
        [Tooltip("Requires holding the interact button")]
        public bool RequiresHold = false;

        [Tooltip("Duration to hold for timed interactions")]
        public float HoldDuration = 1f;

        [Header("UI")]
        [Tooltip("Prompt message shown to player")]
        public string Message = "Press E to Interact";

        [Header("EPIC 16.1: Interaction Context (Optional)")]
        [Tooltip("Semantic verb for this interaction. 'Interact' is the default and won't add the context component.")]
        public InteractionVerb Verb = InteractionVerb.Interact;

        [Tooltip("Localization key for the action name (e.g., 'interact_loot'). Leave empty to use verb name.")]
        public string ActionNameKey = "";

        [Tooltip("Require line of sight for detection")]
        public bool RequireLineOfSight = true;

        public class Baker : Baker<InteractableAuthoring>
        {
            public override void Bake(InteractableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Interactable
                {
                    CanInteract = authoring.CanInteract,
                    RequiresHold = authoring.RequiresHold,
                    HoldDuration = authoring.HoldDuration,
                    InteractionRadius = authoring.InteractionRadius,
                    Message = authoring.Message,
                    Type = authoring.Type,
                    Priority = authoring.Priority,
                    InteractableID = authoring.InteractableID
                });

                AddComponent(entity, new InteractableState
                {
                    InteractingEntity = Entity.Null,
                    Progress = 0f,
                    IsBeingInteracted = false
                });

                // EPIC 16.1: Add context component if verb is non-default or has a localization key
                if (authoring.Verb != InteractionVerb.Interact || !string.IsNullOrEmpty(authoring.ActionNameKey))
                {
                    AddComponent(entity, new InteractableContext
                    {
                        Verb = authoring.Verb,
                        ActionNameKey = authoring.ActionNameKey,
                        RequireLineOfSight = authoring.RequireLineOfSight
                    });
                }
            }
        }
    }
}
