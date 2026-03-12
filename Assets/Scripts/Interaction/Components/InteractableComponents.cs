using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Interaction
{
    /// <summary>
    /// Types of interactable objects.
    /// </summary>
    public enum InteractableType : byte
    {
        Instant = 0,      // Immediate effect
        Timed = 1,        // Hold for duration
        Toggle = 2,       // On/off
        Animated = 3,     // Animation-driven
        Continuous = 4,   // Hold to use
        MultiPhase = 5    // Multi-step sequence (EPIC 16.1 Phase 3)
    }

    #region Core Interactable Components

    /// <summary>
    /// Base component for all interactable objects.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct Interactable : IComponentData
    {
        /// <summary>
        /// Whether this object can currently be interacted with.
        /// </summary>
        [GhostField]
        public bool CanInteract;

        /// <summary>
        /// True = hold to interact, False = tap to interact.
        /// </summary>
        public bool RequiresHold;

        /// <summary>
        /// Duration to hold for timed interactions.
        /// </summary>
        public float HoldDuration;

        /// <summary>
        /// Maximum distance for interaction.
        /// </summary>
        public float InteractionRadius;

        /// <summary>
        /// UI prompt message (e.g., "Press E to Open").
        /// </summary>
        public FixedString64Bytes Message;

        /// <summary>
        /// Type of interaction behavior.
        /// </summary>
        [GhostField]
        public InteractableType Type;

        /// <summary>
        /// Priority for overlapping interactables.
        /// </summary>
        public int Priority;

        /// <summary>
        /// Unique identifier for filtering (EPIC 13.17.4).
        /// 0 = universal (interacts with any ability).
        /// Non-zero = only interacts with matching RequiredInteractableID on ability.
        /// </summary>
        [GhostField]
        public int InteractableID;
    }

    /// <summary>
    /// Current interaction state on an interactable.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct InteractableState : IComponentData
    {
        /// <summary>
        /// Entity currently interacting with this object.
        /// </summary>
        [GhostField]
        public Entity InteractingEntity;

        /// <summary>
        /// Progress for timed interactions (0-1).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Progress;

        /// <summary>
        /// True if currently being interacted with.
        /// </summary>
        [GhostField]
        public bool IsBeingInteracted;
    }

    #endregion

    #region Player Interaction Components

    /// <summary>
    /// Player's interaction ability state.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InteractAbility : IComponentData
    {
        /// <summary>
        /// Current target entity for interaction.
        /// </summary>
        [GhostField]
        public Entity TargetEntity;

        /// <summary>
        /// Progress through current interaction.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float InteractionProgress;

        /// <summary>
        /// True if currently interacting.
        /// </summary>
        [GhostField]
        public bool IsInteracting;

        /// <summary>
        /// Detection range for finding interactables.
        /// </summary>
        public float DetectionRange;

        /// <summary>
        /// Detection cone angle in degrees.
        /// </summary>
        public float DetectionAngle;

        // --- EPIC 13.17.4: ID Filtering ---

        /// <summary>
        /// ID filter for targeted interactions (EPIC 13.17.4).
        /// 0 = can interact with anything.
        /// Non-zero = only interacts with matching Interactable.InteractableID.
        /// </summary>
        public int RequiredInteractableID;

        /// <summary>
        /// Bitmask filter for interactable types (EPIC 13.17.4).
        /// If non-zero, only interacts with types where (1 << type) & mask != 0.
        /// </summary>
        public int InteractableTypeMask;

        // --- EPIC 13.17.5: Ability Blocking ---

        /// <summary>
        /// Allow height changes (crouch/prone) during interaction (EPIC 13.17.5).
        /// </summary>
        public bool AllowHeightChange;

        /// <summary>
        /// Allow aiming/looking during interaction (EPIC 13.17.5).
        /// </summary>
        public bool AllowAim;

        /// <summary>
        /// Interaction can run concurrently with locomotion (EPIC 13.17.5).
        /// If false, blocks movement abilities during interaction.
        /// </summary>
        public bool IsConcurrent;

        /// <summary>
        /// Bitmask of abilities blocked during this interaction (EPIC 13.17.5).
        /// Maps to AbilityDefinition.AbilityTypeId.
        /// </summary>
        public int BlockedAbilitiesMask;
    }

    /// <summary>
    /// Request to interact with an object.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InteractRequest : IComponentData
    {
        /// <summary>
        /// Target to interact with.
        /// </summary>
        [GhostField]
        public Entity TargetEntity;

        /// <summary>
        /// True to start interaction.
        /// </summary>
        [GhostField]
        public bool StartInteract;

        /// <summary>
        /// True to cancel interaction.
        /// </summary>
        [GhostField]
        public bool CancelInteract;
    }

    #endregion

    #region Animated Interactable Components

    /// <summary>
    /// State for animated interactables (doors, levers).
    /// EPIC 13.17.6-13.17.9: Enhanced with audio, triggers, reset, and multi-switch support.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct AnimatedInteractable : IComponentData
    {
        /// <summary>
        /// Current state (true = open/on, false = closed/off).
        /// </summary>
        [GhostField]
        public bool IsOpen;

        /// <summary>
        /// Duration of open/close animation.
        /// </summary>
        public float AnimationDuration;

        /// <summary>
        /// Current time through animation.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CurrentTime;

        /// <summary>
        /// True if currently animating.
        /// </summary>
        [GhostField]
        public bool IsAnimating;

        /// <summary>
        /// Whether to lock player during animation.
        /// </summary>
        public bool LockPlayerDuringAnimation;

        // --- EPIC 13.17.6: Audio Feedback ---

        /// <summary>
        /// Current audio clip index for cycling (EPIC 13.17.6).
        /// -1 = no clip played yet.
        /// </summary>
        public int AudioClipIndex;

        // --- EPIC 13.17.7: State Reset ---

        /// <summary>
        /// Whether this interactable can only be used once (EPIC 13.17.7).
        /// </summary>
        public bool SingleInteract;

        /// <summary>
        /// True if has been interacted with (EPIC 13.17.7).
        /// Used with SingleInteract to prevent re-interaction.
        /// </summary>
        [GhostField]
        public bool HasInteracted;

        // --- EPIC 13.17.8: Multi-Switch / Toggle State ---

        /// <summary>
        /// Whether the bool state toggles after each interaction (EPIC 13.17.8).
        /// </summary>
        public bool ToggleBoolValue;

        /// <summary>
        /// True when this interactable is the active one in a multi-switch group (EPIC 13.17.8).
        /// Used for exclusive state logic.
        /// </summary>
        [GhostField]
        public bool IsActiveBoolInteractable;

        /// <summary>
        /// Group ID for multi-switch behavior (EPIC 13.17.8).
        /// Interactables with same GroupID share exclusive state.
        /// 0 = not part of a group.
        /// </summary>
        public int SwitchGroupID;

        // --- EPIC 13.17.9: Trigger Parameter ---

        /// <summary>
        /// Animator trigger parameter hash (EPIC 13.17.9).
        /// If non-zero, SetTrigger is called on interaction.
        /// </summary>
        public int TriggerParameterHash;

        /// <summary>
        /// Animator bool parameter hash (EPIC 13.17.9).
        /// If non-zero, SetBool is called on interaction.
        /// </summary>
        public int BoolParameterHash;
    }

    /// <summary>
    /// Audio configuration for interactable feedback (EPIC 13.17.6).
    /// </summary>
    public struct InteractableAudioConfig : IComponentData
    {
        /// <summary>
        /// Number of audio clips available for cycling.
        /// </summary>
        public int ClipCount;

        /// <summary>
        /// Audio volume for playback.
        /// </summary>
        public float Volume;

        /// <summary>
        /// Pitch randomization range (+/- from 1.0).
        /// </summary>
        public float PitchVariation;

        /// <summary>
        /// Whether to cycle clips sequentially vs randomly.
        /// </summary>
        public bool SequentialCycle;
    }

    /// <summary>
    /// Buffer element for audio clip references (EPIC 13.17.6).
    /// Used with managed AudioClip references via hybrid approach.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct InteractableAudioClipElement : IBufferElementData
    {
        /// <summary>
        /// Index into managed audio clip array.
        /// </summary>
        public int ClipIndex;
    }

    /// <summary>
    /// UI message configuration for interactables (EPIC 13.17.8).
    /// Allows different messages for different states.
    /// </summary>
    public struct InteractableMessageConfig : IComponentData
    {
        /// <summary>
        /// Message shown when bool state is enabled/open.
        /// </summary>
        public FixedString64Bytes EnabledMessage;

        /// <summary>
        /// Message shown when bool state is disabled/closed.
        /// </summary>
        public FixedString64Bytes DisabledMessage;
    }

    /// <summary>
    /// Door-specific interactable configuration.
    /// </summary>
    public struct DoorInteractable : IComponentData
    {
        /// <summary>
        /// Rotation when fully open.
        /// </summary>
        public float OpenAngle;

        /// <summary>
        /// Rotation when closed.
        /// </summary>
        public float ClosedAngle;

        /// <summary>
        /// Speed of swing in degrees/second.
        /// </summary>
        public float SwingSpeed;

        /// <summary>
        /// Whether door auto-closes.
        /// </summary>
        public bool AutoClose;

        /// <summary>
        /// Delay before auto-close.
        /// </summary>
        public float AutoCloseDelay;

        /// <summary>
        /// Time since door was opened (for auto-close).
        /// </summary>
        public float TimeSinceOpened;
    }

    /// <summary>
    /// Lever-specific interactable configuration.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct LeverInteractable : IComponentData
    {
        /// <summary>
        /// Entity this lever controls.
        /// </summary>
        [GhostField]
        public Entity TargetEntity;

        /// <summary>
        /// Event name to fire on toggle.
        /// </summary>
        public FixedString32Bytes TargetEvent;

        /// <summary>
        /// Current activation state.
        /// </summary>
        [GhostField]
        public bool IsActivated;
    }

    #endregion

    #region UI Components

    /// <summary>
    /// UI prompt state for interaction display.
    /// </summary>
    public struct InteractionPrompt : IComponentData
    {
        /// <summary>
        /// Entity being prompted for.
        /// </summary>
        public Entity InteractableEntity;

        /// <summary>
        /// Message to display.
        /// </summary>
        public FixedString64Bytes Message;

        /// <summary>
        /// Hold progress for timed interactions.
        /// </summary>
        public float HoldProgress;

        /// <summary>
        /// Whether prompt should be visible.
        /// </summary>
        public bool IsVisible;

        /// <summary>
        /// Screen position for prompt (set by UI system).
        /// </summary>
        public float2 ScreenPosition;
    }

    #endregion

    #region Position Snapping Components

    /// <summary>
    /// Request to move player to interaction position.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct MoveTowardsLocation : IComponentData
    {
        /// <summary>
        /// Target world position.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 TargetPosition;

        /// <summary>
        /// Target rotation.
        /// </summary>
        [GhostField(Quantization = 1000)]
        public quaternion TargetRotation;

        /// <summary>
        /// Movement speed.
        /// </summary>
        public float MoveSpeed;

        /// <summary>
        /// Rotation speed.
        /// </summary>
        public float RotateSpeed;

        /// <summary>
        /// True when reached target.
        /// </summary>
        [GhostField]
        public bool HasArrived;

        /// <summary>
        /// True if currently moving to target.
        /// </summary>
        [GhostField]
        public bool IsMoving;

        // --- EPIC 13.17.3: Enhanced Positioning ---

        /// <summary>
        /// Acceptable tolerance radius for arrival check (EPIC 13.17.3).
        /// Default: 0.05f
        /// </summary>
        public float Size;

        /// <summary>
        /// Acceptable angle tolerance in degrees for arrival check (EPIC 13.17.3).
        /// Default: 5.0f degrees
        /// </summary>
        public float Angle;

        /// <summary>
        /// Require player to be grounded before starting movement (EPIC 13.17.3).
        /// </summary>
        public bool RequireGrounded;

        /// <summary>
        /// Use precision interpolation at start for smoother blend (EPIC 13.17.3).
        /// </summary>
        public bool PrecisionStart;

        /// <summary>
        /// Movement speed multiplier (EPIC 13.17.3).
        /// 1.0 = normal speed, 0.5 = half speed, etc.
        /// </summary>
        public float MovementMultiplier;
    }

    /// <summary>
    /// Defines enter/exit positions for interactions.
    /// </summary>
    public struct InteractionLocation : IComponentData
    {
        /// <summary>
        /// Position to move to when starting interaction.
        /// </summary>
        public float3 EnterPosition;

        /// <summary>
        /// Position to move to when ending interaction.
        /// </summary>
        public float3 ExitPosition;

        /// <summary>
        /// Rotation during interaction.
        /// </summary>
        public quaternion InteractionRotation;

        /// <summary>
        /// Whether to snap instantly vs lerp.
        /// </summary>
        public bool SnapToPosition;

        // --- EPIC 13.17.3: Enhanced Positioning ---

        /// <summary>
        /// Acceptable position tolerance (EPIC 13.17.3).
        /// </summary>
        public float Size;

        /// <summary>
        /// Acceptable rotation tolerance in degrees (EPIC 13.17.3).
        /// </summary>
        public float AngleTolerance;

        /// <summary>
        /// Require grounded before interaction (EPIC 13.17.3).
        /// </summary>
        public bool RequireGrounded;

        /// <summary>
        /// Use smooth start blend (EPIC 13.17.3).
        /// </summary>
        public bool PrecisionStart;

        /// <summary>
        /// Speed multiplier during approach (EPIC 13.17.3).
        /// </summary>
        public float MovementMultiplier;
    }

    #endregion

    #region Animation Event Components (EPIC 13.17.1)

    /// <summary>
    /// Phase of the interaction process for animation-driven interactions.
    /// </summary>
    public enum InteractionPhase : byte
    {
        /// <summary>No interaction active.</summary>
        None = 0,
        /// <summary>Moving to interaction location.</summary>
        WaitingForPosition = 1,
        /// <summary>Waiting for OnAnimatorInteract event.</summary>
        WaitingForAnimStart = 2,
        /// <summary>Interaction effect in progress.</summary>
        InProgress = 3,
        /// <summary>Waiting for OnAnimatorInteractComplete event.</summary>
        WaitingForAnimEnd = 4,
        /// <summary>Cleanup and exit phase.</summary>
        Completing = 5
    }

    /// <summary>
    /// Extended state for animation-driven interactions (EPIC 13.17.1).
    /// Tracks animation event synchronization and interaction phases.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InteractAbilityState : IComponentData
    {
        /// <summary>
        /// True when waiting for OnAnimatorInteract event before triggering effect.
        /// </summary>
        [GhostField]
        public bool WaitingForAnimatorInteract;

        /// <summary>
        /// True when waiting for OnAnimatorInteractComplete event before ending.
        /// </summary>
        [GhostField]
        public bool WaitingForAnimatorComplete;

        /// <summary>
        /// Set by animation event system when OnAnimatorInteract fires.
        /// Client-side only, not replicated.
        /// </summary>
        public bool AnimatorInteractReceived;

        /// <summary>
        /// Set by animation event system when OnAnimatorInteractComplete fires.
        /// Client-side only, not replicated.
        /// </summary>
        public bool AnimatorCompleteReceived;

        /// <summary>
        /// Timeout timer for animation events. Increments each frame while waiting.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float AnimEventTimeout;

        /// <summary>
        /// Maximum wait time for animation events before timeout fallback.
        /// Default: 2.0 seconds.
        /// </summary>
        public float MaxAnimEventTimeout;

        /// <summary>
        /// Current phase of the interaction process.
        /// </summary>
        [GhostField]
        public InteractionPhase Phase;

        /// <summary>
        /// Animator parameter value to pass for this interaction type.
        /// Used for blend trees or state selection.
        /// </summary>
        public int AnimatorIntData;
    }

    /// <summary>
    /// Configuration for animation-driven interactions on interactables.
    /// Add to interactables that require animation synchronization.
    /// </summary>
    public struct InteractableAnimationConfig : IComponentData
    {
        /// <summary>
        /// Wait for OnAnimatorInteract before triggering effect.
        /// </summary>
        public bool WaitForAnimStart;

        /// <summary>
        /// Wait for OnAnimatorInteractComplete before ending interaction.
        /// </summary>
        public bool WaitForAnimComplete;

        /// <summary>
        /// Timeout in seconds for animation events.
        /// </summary>
        public float AnimEventTimeout;

        /// <summary>
        /// Animator parameter value for this interactable type.
        /// </summary>
        public int AnimatorIntData;
    }

    #endregion

    #region IK Targets (EPIC 13.17.2)

    /// <summary>
    /// Which hand goal(s) to target for IK.
    /// </summary>
    public enum HandIKGoal : byte
    {
        None = 0,
        LeftHand = 1,
        RightHand = 2,
        BothHands = 3
    }

    /// <summary>
    /// IK target data on an interactable entity (EPIC 13.17.2).
    /// Defines where hands should be placed during interaction.
    /// </summary>
    public struct InteractableIKTarget : IComponentData
    {
        /// <summary>
        /// Which hand(s) this target applies to.
        /// </summary>
        public HandIKGoal Goal;

        /// <summary>
        /// Local position offset from interactable center.
        /// </summary>
        public float3 LeftHandPositionOffset;

        /// <summary>
        /// Local rotation for left hand grip.
        /// </summary>
        public quaternion LeftHandRotation;

        /// <summary>
        /// Local position offset for right hand.
        /// </summary>
        public float3 RightHandPositionOffset;

        /// <summary>
        /// Local rotation for right hand grip.
        /// </summary>
        public quaternion RightHandRotation;

        /// <summary>
        /// Delay before IK engages after interaction starts.
        /// </summary>
        public float Delay;

        /// <summary>
        /// Duration of active IK (0 = until interaction ends).
        /// </summary>
        public float Duration;

        /// <summary>
        /// Override interpolation speed (0 = use settings default).
        /// </summary>
        public float InterpolationSpeed;
    }

    #endregion

    #region Tags and Events

    /// <summary>
    /// Tag for entities that can interact with objects.
    /// </summary>
    public struct CanInteract : IComponentData { }

    /// <summary>
    /// Event fired when interaction completes.
    /// </summary>
    public struct InteractionCompleteEvent : IComponentData
    {
        public Entity InteractableEntity;
        public Entity InteractorEntity;
    }

    /// <summary>
    /// Event fired when lever is toggled.
    /// </summary>
    public struct LeverToggleEvent : IComponentData
    {
        public Entity LeverEntity;
        public Entity TargetEntity;
        public bool NewState;
    }

    /// <summary>
    /// Tag to request interactable state reset (EPIC 13.17.7).
    /// Add this component to reset HasInteracted, AudioClipIndex, and animator.
    /// System will remove after processing.
    /// </summary>
    public struct ResetInteractableRequest : IComponentData { }

    /// <summary>
    /// Request to play audio on interactable (EPIC 13.17.6).
    /// Processed by InteractableAudioBridge.
    /// </summary>
    public struct PlayInteractAudioRequest : IComponentData
    {
        /// <summary>
        /// Index of clip to play (-1 = auto-cycle).
        /// </summary>
        public int ClipIndex;
    }

    /// <summary>
    /// Request to set animator parameter (EPIC 13.17.9).
    /// Processed by InteractableAnimatorBridge.
    /// </summary>
    public struct SetAnimatorParameterRequest : IComponentData
    {
        /// <summary>
        /// True to set trigger, false to set bool.
        /// </summary>
        public bool IsTrigger;

        /// <summary>
        /// Parameter hash (from Animator.StringToHash).
        /// </summary>
        public int ParameterHash;

        /// <summary>
        /// Bool value (ignored for triggers).
        /// </summary>
        public bool BoolValue;
    }

    #endregion

    #region Resource Interaction Components

    /// <summary>
    /// Resource-specific interactable data.
    /// Add alongside base Interactable component.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ResourceInteractable : IComponentData
    {
        /// <summary>
        /// Type of resource this node yields.
        /// </summary>
        [GhostField]
        public byte ResourceTypeId;

        /// <summary>
        /// Remaining quantity available.
        /// </summary>
        [GhostField]
        public int CurrentAmount;

        /// <summary>
        /// Maximum amount for respawn.
        /// </summary>
        public int MaxAmount;

        /// <summary>
        /// Seconds to collect one unit.
        /// </summary>
        public float CollectionTime;

        /// <summary>
        /// Amount per collection action.
        /// </summary>
        public int AmountPerCollection;

        /// <summary>
        /// If true, requires specific tool to collect.
        /// </summary>
        public bool RequiresTool;

        /// <summary>
        /// Seconds to respawn after depleted (0 = no respawn).
        /// </summary>
        public float RespawnTime;
    }

    /// <summary>
    /// Tracks current collection progress for timed resource collection.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CollectionProgress : IComponentData
    {
        /// <summary>
        /// Node being collected from.
        /// </summary>
        [GhostField]
        public Entity TargetNode;

        /// <summary>
        /// Time spent collecting.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ElapsedTime;

        /// <summary>
        /// Total time required.
        /// </summary>
        public float RequiredTime;

        /// <summary>
        /// True if actively collecting.
        /// </summary>
        [GhostField]
        public bool IsCollecting;

        /// <summary>
        /// Entity doing the collecting.
        /// </summary>
        [GhostField]
        public Entity CollectorEntity;
    }

    /// <summary>
    /// Tag added when resource is depleted.
    /// </summary>
    public struct ResourceDepleted : IComponentData
    {
        /// <summary>
        /// Time since depletion for respawn tracking.
        /// </summary>
        public float TimeSinceDepletion;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    //  EPIC 16.1: Interaction Context (Rich Prompts)
    // ─────────────────────────────────────────────────────

    #region EPIC 16.1 - Interaction Context

    /// <summary>
    /// EPIC 16.1: Semantic verb describing what the interaction does.
    /// Used for localized UI prompts (e.g., "Loot" -> "Ricerca" in Italian).
    /// </summary>
    public enum InteractionVerb : byte
    {
        Interact = 0,
        Loot = 1,
        Open = 2,
        Close = 3,
        Revive = 4,
        Breach = 5,
        Talk = 6,
        Use = 7,
        Craft = 8,
        Mount = 9,
        Dismount = 10,
        Place = 11,
        Pickup = 12,
        Activate = 13,
        Deactivate = 14,
        Trade = 15          // EPIC 17.2: NeedGreed item trading
    }

    /// <summary>
    /// EPIC 16.1: Optional component providing rich interaction context.
    /// When present, the UI prompt system uses the verb and localization key
    /// instead of the raw Interactable.Message string.
    /// </summary>
    public struct InteractableContext : IComponentData
    {
        /// <summary>
        /// Semantic verb for this interaction (e.g., Loot, Open, Talk).
        /// </summary>
        public InteractionVerb Verb;

        /// <summary>
        /// Localization key for the action name (e.g., "interact_loot").
        /// If empty, falls back to the verb name.
        /// </summary>
        public FixedString32Bytes ActionNameKey;

        /// <summary>
        /// Override line-of-sight requirement per interactable.
        /// Default true (most interactions require LOS).
        /// </summary>
        public bool RequireLineOfSight;
    }

    #endregion
}
