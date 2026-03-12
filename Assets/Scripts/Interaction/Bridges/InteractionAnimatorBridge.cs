using UnityEngine;
using UnityEngine.Events;
using Unity.Mathematics;

namespace DIG.Interaction.Bridges
{
    /// <summary>
    /// EPIC 13.17.1: Animation bridge for interaction system.
    ///
    /// Handles bidirectional communication between the interaction system and Animator:
    /// - Receives interaction state from ECS via InteractionAnimatorBridgeSystem
    /// - Sends animation events back to DOTS via static events
    ///
    /// Features:
    /// - OnAnimatorInteract: Called when interaction animation reaches the "interact" point
    /// - OnAnimatorInteractComplete: Called when interaction animation finishes
    /// - Animator parameter control for interaction states
    /// - Timeout fallback via ECS system
    ///
    /// Designer Workflow:
    /// 1. Attach to player presentation prefab alongside Animator
    /// 2. Configure parameter names to match your Animator Controller
    /// 3. Add animation events to your interaction animation clips:
    ///    - "OnAnimatorInteract" at the point where the effect should trigger
    ///    - "OnAnimatorInteractComplete" at the end of the animation
    /// 4. Hook up UnityEvents for VFX/audio feedback
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionAnimatorBridge : MonoBehaviour
    {
        #region References
        [Header("References")]
        [Tooltip("Animator component. Auto-found if not set.")]
        public Animator Animator;
        #endregion

        #region Interaction Parameters
        [Header("Interaction Animator Parameters")]
        [Tooltip("Bool parameter - true when interacting")]
        public string ParamIsInteracting = "IsInteracting";

        [Tooltip("Int parameter - interaction type/ID for blend tree selection")]
        public string ParamInteractionId = "InteractionId";

        [Tooltip("Trigger parameter - fires when interaction starts")]
        public string ParamInteractTrigger = "InteractTrigger";

        [Tooltip("Float parameter - interaction progress (0-1) for timed interactions")]
        public string ParamInteractionProgress = "InteractionProgress";

        [Tooltip("Int parameter - interaction phase (0=none, 1=starting, 2=active, 3=ending)")]
        public string ParamInteractionPhase = "InteractionPhase";
        #endregion

        #region Animation Event Settings
        [Header("Animation Event Settings")]
        [Tooltip("Name of animation event that signals interaction point reached")]
        public string InteractEventName = "OnAnimatorInteract";

        [Tooltip("Name of animation event that signals interaction complete")]
        public string CompleteEventName = "OnAnimatorInteractComplete";
        #endregion

        #region Events
        [Header("Events (for Audio/VFX)")]
        [Tooltip("Invoked when interaction starts")]
        public UnityEvent OnInteractionStart;

        [Tooltip("Invoked when interaction point reached (OnAnimatorInteract event)")]
        public UnityEvent OnInteract;

        [Tooltip("Invoked when interaction completes (OnAnimatorInteractComplete event)")]
        public UnityEvent OnInteractionComplete;

        [Tooltip("Invoked when interaction is cancelled")]
        public UnityEvent OnInteractionCancelled;
        #endregion

        #region Debug
        [Header("Debug")]
        public bool DebugLogging = false;
        #endregion

        // Cached parameter hashes for performance
        private int _isInteractingHash;
        private int _interactionIdHash;
        private int _interactTriggerHash;
        private int _interactionProgressHash;
        private int _interactionPhaseHash;

        // State tracking
        private bool _wasInteracting;
        private int _lastInteractionId;
        private float _lastProgress;

        /// <summary>
        /// Static event system for DOTS communication.
        /// InteractionAnimationEventSystem subscribes to these.
        /// </summary>
        public static event System.Action<GameObject> OnAnimatorInteractEvent;
        public static event System.Action<GameObject> OnAnimatorInteractCompleteEvent;

        #region Unity Lifecycle
        private void Reset()
        {
            Animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (Animator == null)
                Animator = GetComponentInChildren<Animator>();

            if (Animator == null)
            {
                Debug.LogWarning($"[InteractionAnimatorBridge] No Animator found on {gameObject.name}. Interaction animations will not play.");
                enabled = false;
                return;
            }

            CacheParameterHashes();

            if (DebugLogging)
                Debug.Log($"[InteractionAnimatorBridge] Initialized on {gameObject.name}");
        }
        #endregion

        #region Initialization
        private void CacheParameterHashes()
        {
            if (!string.IsNullOrEmpty(ParamIsInteracting))
                _isInteractingHash = Animator.StringToHash(ParamIsInteracting);
            if (!string.IsNullOrEmpty(ParamInteractionId))
                _interactionIdHash = Animator.StringToHash(ParamInteractionId);
            if (!string.IsNullOrEmpty(ParamInteractTrigger))
                _interactTriggerHash = Animator.StringToHash(ParamInteractTrigger);
            if (!string.IsNullOrEmpty(ParamInteractionProgress))
                _interactionProgressHash = Animator.StringToHash(ParamInteractionProgress);
            if (!string.IsNullOrEmpty(ParamInteractionPhase))
                _interactionPhaseHash = Animator.StringToHash(ParamInteractionPhase);
        }
        #endregion

        #region Public API - Called by ECS Bridge System
        /// <summary>
        /// Update interaction state from ECS.
        /// Called by InteractionAnimatorBridgeSystem each frame.
        /// </summary>
        /// <param name="isInteracting">True if currently in interaction</param>
        /// <param name="interactionId">ID/type of current interaction</param>
        /// <param name="progress">Progress 0-1 for timed interactions</param>
        /// <param name="phase">Current interaction phase</param>
        public void UpdateInteractionState(bool isInteracting, int interactionId, float progress, int phase)
        {
            if (Animator == null) return;

            // Update animator parameters
            if (_isInteractingHash != 0)
                Animator.SetBool(_isInteractingHash, isInteracting);

            if (_interactionIdHash != 0)
                Animator.SetInteger(_interactionIdHash, interactionId);

            if (_interactionProgressHash != 0)
                Animator.SetFloat(_interactionProgressHash, progress);

            if (_interactionPhaseHash != 0)
                Animator.SetInteger(_interactionPhaseHash, phase);

            // Detect state transitions
            DetectStateTransitions(isInteracting, interactionId, progress);
        }

        /// <summary>
        /// Trigger the interaction animation start.
        /// </summary>
        public void TriggerInteraction()
        {
            if (_interactTriggerHash != 0 && Animator != null)
            {
                Animator.SetTrigger(_interactTriggerHash);

                if (DebugLogging)
                    Debug.Log($"[InteractionAnimatorBridge] TriggerInteraction on {gameObject.name}");
            }
        }

        /// <summary>
        /// Force signal interaction event (for external callers or timeout fallback).
        /// </summary>
        public void SignalInteract()
        {
            OnAnimatorInteract();
        }

        /// <summary>
        /// Force signal interaction complete (for external callers or timeout fallback).
        /// </summary>
        public void SignalComplete()
        {
            OnAnimatorInteractComplete();
        }
        #endregion

        #region State Transition Detection
        private void DetectStateTransitions(bool isInteracting, int interactionId, float progress)
        {
            // Detect interaction start
            if (isInteracting && !_wasInteracting)
            {
                OnInteractionStarted(interactionId);
            }

            // Detect interaction end (cancelled before complete)
            if (!isInteracting && _wasInteracting && math.abs(progress) < 0.001f)
            {
                OnInteractionCancelledInternal();
            }

            _wasInteracting = isInteracting;
            _lastInteractionId = interactionId;
            _lastProgress = progress;
        }

        private void OnInteractionStarted(int interactionId)
        {
            if (DebugLogging)
                Debug.Log($"[InteractionAnimatorBridge] Interaction started with ID {interactionId}");

            OnInteractionStart?.Invoke();
        }

        private void OnInteractionCancelledInternal()
        {
            if (DebugLogging)
                Debug.Log($"[InteractionAnimatorBridge] Interaction cancelled");

            OnInteractionCancelled?.Invoke();
        }
        #endregion

        #region Animation Event Receivers
        /// <summary>
        /// Called by animation event when the interaction effect should trigger.
        /// Add this event to your interaction animation clip at the appropriate moment.
        /// This signals the DOTS system to trigger the actual interaction effect.
        /// </summary>
        public void OnAnimatorInteract()
        {
            if (DebugLogging)
                Debug.Log($"[InteractionAnimatorBridge] Animation event: OnAnimatorInteract on {gameObject.name}");

            // Fire static event for DOTS system
            OnAnimatorInteractEvent?.Invoke(gameObject);

            // Fire UnityEvent for local handlers (audio, VFX)
            OnInteract?.Invoke();
        }

        /// <summary>
        /// Called by animation event when the interaction animation completes.
        /// Add this event to your interaction animation clip at the end.
        /// This signals the DOTS system that it can transition out of the interaction state.
        /// </summary>
        public void OnAnimatorInteractComplete()
        {
            if (DebugLogging)
                Debug.Log($"[InteractionAnimatorBridge] Animation event: OnAnimatorInteractComplete on {gameObject.name}");

            // Fire static event for DOTS system
            OnAnimatorInteractCompleteEvent?.Invoke(gameObject);

            // Fire UnityEvent for local handlers
            OnInteractionComplete?.Invoke();
        }

        /// <summary>
        /// Generic animation event handler.
        /// Animator can call this with string parameter for flexibility.
        /// </summary>
        /// <param name="eventName">Name of the event to trigger</param>
        public void OnAnimationEvent(string eventName)
        {
            if (DebugLogging)
                Debug.Log($"[InteractionAnimatorBridge] Animation event: {eventName}");

            if (eventName == InteractEventName)
            {
                OnAnimatorInteract();
            }
            else if (eventName == CompleteEventName)
            {
                OnAnimatorInteractComplete();
            }
        }
        #endregion

        #region Public Properties
        /// <summary>Check if currently in interaction state based on cached values.</summary>
        public bool IsInteracting => _wasInteracting;

        /// <summary>Get current interaction ID.</summary>
        public int CurrentInteractionId => _lastInteractionId;

        /// <summary>Get current interaction progress.</summary>
        public float CurrentProgress => _lastProgress;
        #endregion
    }
}
