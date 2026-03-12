using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using DIG.Player.Abilities;

namespace Player.Bridges
{
    /// <summary>
    /// DEPRECATED: Fall animation is now handled by Opsive's AbilityIndex system.
    /// 
    /// Setting AbilityIndex = 2 (ABILITY_FALL) triggers "Base Layer > Fall" state automatically
    /// via the Opsive animator controller's transitions. This is handled by:
    /// - PlayerAnimationStateSystem: Sets AbilityIndex = 2 when falling
    /// - ClimbAnimatorBridge: Writes AbilityIndex to animator
    /// 
    /// This bridge is kept for backwards compatibility but does NOT set animator parameters.
    /// The UnityEvents (OnFallStart, OnLanding, OnFallComplete) still work for VFX/audio.
    /// 
    /// OLD Features (no longer used):
    /// - 13.14.3: Animation event wait for land complete (OnAnimatorFallComplete)
    /// - 13.14.4: Blend tree float data (FallVelocity)
    /// - 13.14.6: State index for animation (0 = falling, 1 = landed)
    /// </summary>
    [DisallowMultipleComponent]
    public class FallAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        #region Settings
        [Header("Settings")]
        [Tooltip("Enable legacy fall parameters (IsFalling, FallVelocity, FallStateIndex). " +
                 "Disable this to use Opsive's native AbilityIndex=2 fall handling.")]
        public bool UseLegacyFallParameters = false;
        #endregion

        #region References
        [Header("References")]
        [Tooltip("Animator component. Auto-found if not set.")]
        public Animator Animator;
        #endregion

        #region Fall Parameters
        [Header("Fall Animator Parameters")]
        [Tooltip("Bool parameter - true when actively falling")]
        public string ParamIsFalling = "IsFalling";

        [Tooltip("Float parameter - vertical velocity for blend tree (negative = falling)")]
        public string ParamFallVelocity = "FallVelocity";

        [Tooltip("Int parameter - fall state index (0 = falling, 1 = landed)")]
        public string ParamFallStateIndex = "FallStateIndex";

        [Tooltip("Trigger parameter - fires when landing occurs")]
        public string ParamLandTrigger = "LandTrigger";
        #endregion

        #region Animation Event Settings
        [Header("Animation Event Settings")]
        [Tooltip("Name of animation event that signals fall complete (called from landing animation)")]
        public string FallCompleteEventName = "OnAnimatorFallComplete";
        #endregion

        #region Events
        [Header("Events (for Audio/VFX)")]
        [Tooltip("Invoked when fall starts")]
        public UnityEvent OnFallStart;

        [Tooltip("Invoked when landing occurs (before animation completes)")]
        public UnityEvent<float> OnLanding;

        [Tooltip("Invoked when landing animation completes (OnAnimatorFallComplete event)")]
        public UnityEvent OnFallComplete;
        #endregion

        #region Debug
        [Header("Debug")]
        public bool DebugLogging = false;
        #endregion

        // Cached parameter hashes
        private int _isFallingHash;
        private int _fallVelocityHash;
        private int _fallStateIndexHash;
        private int _landTriggerHash;

        // State tracking
        private bool _wasFalling;
        private int _lastStateIndex;

        // Static event system for DOTS communication
        // When animation event fires, we set this and the FallAnimationEventReceiverSystem reads it
        public static event System.Action<GameObject> OnFallAnimationCompleteEvent;

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
                Debug.LogWarning($"[FallAnimatorBridge] No Animator found on {gameObject.name}. Fall animations will not play.");
                enabled = false;
                return;
            }

            CacheParameterHashes();

            if (DebugLogging)
                Debug.Log($"[FallAnimatorBridge] Initialized on {gameObject.name}");
        }
        #endregion

        #region Initialization
        private void CacheParameterHashes()
        {
            _isFallingHash = Animator.StringToHash(ParamIsFalling);
            _fallVelocityHash = Animator.StringToHash(ParamFallVelocity);
            _fallStateIndexHash = Animator.StringToHash(ParamFallStateIndex);
            _landTriggerHash = Animator.StringToHash(ParamLandTrigger);
        }
        #endregion

        #region IPlayerAnimationBridge Implementation
        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            if (Animator == null) return;

            // NEW: Skip setting legacy fall parameters - Opsive's AbilityIndex=2 handles this now
            // Only set parameters if explicitly using legacy mode (for backwards compatibility)
            if (UseLegacyFallParameters)
            {
                // 13.14.4: Send fall velocity for blend tree
                if (!string.IsNullOrEmpty(ParamFallVelocity))
                    Animator.SetFloat(_fallVelocityHash, state.FallVelocity);

                // 13.14.6: Send state index
                if (!string.IsNullOrEmpty(ParamFallStateIndex))
                    Animator.SetInteger(_fallStateIndexHash, state.FallStateIndex);

                // Set falling bool
                if (!string.IsNullOrEmpty(ParamIsFalling))
                    Animator.SetBool(_isFallingHash, state.IsFalling);
            }

            // Detect state transitions (still works for VFX/audio events)
            DetectStateTransitions(state);
        }

        public void TriggerLanding()
        {
            // Fallback for systems that trigger landing directly
            if (!string.IsNullOrEmpty(ParamLandTrigger) && Animator != null)
                Animator.SetTrigger(_landTriggerHash);
        }
        #endregion

        #region State Transition Detection
        private void DetectStateTransitions(PlayerAnimationState animState)
        {
            // Detect fall start
            if (animState.IsFalling && !_wasFalling)
            {
                OnFallStarted();
            }

            // Detect landing (state index changed from 0 to 1)
            if (_lastStateIndex == 0 && animState.FallStateIndex == 1)
            {
                OnLanded(animState.FallVelocity);
            }

            _wasFalling = animState.IsFalling;
            _lastStateIndex = animState.FallStateIndex;
        }

        private void OnFallStarted()
        {
            if (DebugLogging)
                Debug.Log($"[FallAnimatorBridge] Fall started");

            OnFallStart?.Invoke();
        }

        private void OnLanded(float impactVelocity)
        {
            if (DebugLogging)
                Debug.Log($"[FallAnimatorBridge] Landed with velocity {impactVelocity:F2}");

            // Trigger landing animation
            if (!string.IsNullOrEmpty(ParamLandTrigger) && Animator != null)
                Animator.SetTrigger(_landTriggerHash);

            // Calculate intensity from velocity
            float intensity = math.clamp(math.abs(impactVelocity) / 15f, 0f, 1f);
            OnLanding?.Invoke(intensity);
        }
        #endregion

        #region Animation Event Receivers
        /// <summary>
        /// Called by animation event when fall/landing animation completes.
        /// Add this event to your landing animation clip at the end.
        /// This signals the DOTS system that it can transition out of the fall state.
        /// </summary>
        public void OnAnimatorFallComplete()
        {
            if (DebugLogging)
                Debug.Log($"[FallAnimatorBridge] Animation event: OnAnimatorFallComplete on {gameObject.name}");

            // Fire static event for DOTS system to receive
            OnFallAnimationCompleteEvent?.Invoke(gameObject);

            // Fire UnityEvent for local handlers
            OnFallComplete?.Invoke();
        }

        /// <summary>
        /// Generic animation event handler.
        /// Animator can call this with string parameter.
        /// </summary>
        public void OnAnimationEvent(string eventName)
        {
            if (DebugLogging)
                Debug.Log($"[FallAnimatorBridge] Animation event: {eventName}");

            if (eventName == FallCompleteEventName)
            {
                OnAnimatorFallComplete();
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Force signal fall complete (for external callers).
        /// </summary>
        public void SignalFallComplete()
        {
            OnAnimatorFallComplete();
        }

        /// <summary>
        /// Check if currently in falling state based on cached values.
        /// </summary>
        public bool IsFalling => _wasFalling;

        /// <summary>
        /// Get current fall state index (0 = falling, 1 = landed).
        /// </summary>
        public int FallStateIndex => _lastStateIndex;
        #endregion
    }
}
