using UnityEngine;
using UnityEngine.Events;
using Player.Systems;

namespace Player.Bridges
{
    /// <summary>
    /// Animation bridge for landing-specific visuals (Epic 1.8).
    /// Drives landing animator triggers, recovery timing, and optional root-motion-to-MoveRequest translation.
    /// Receives animation events (via AnimatorEventBridge) to trigger VFX/audio and notify DOTS systems.
    /// 
    /// Designer Workflow:
    /// 1. Attach to player prefab alongside Animator
    /// 2. Configure parameter names to match your Animator Controller
    /// 3. Hook up UnityEvents for audio/VFX on landing
    /// 4. Optionally enable root motion translation for landing recovery animations
    /// </summary>
    [DisallowMultipleComponent]
    public class LandingAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        #region References
        [Header("References")]
        [Tooltip("Animator component on this player character. Auto-found if not set.")]
        public Animator Animator;
        
        [Tooltip("Optional CharacterController for root motion translation. " +
                 "Only needed if ApplyRootMotion=true AND this bridge is on the same GameObject as the CharacterController. " +
                 "For hybrid Ghost/UI setups, leave this empty - root motion will move transform directly (visual only).")]
        public CharacterController CharacterController;
        #endregion
        
        #region Landing Parameters
        [Header("Landing Animator Parameters")]
        [Tooltip("Trigger parameter to start landing animation")]
        public string ParamLandingTrigger = "LandTrigger";
        
        [Tooltip("Float parameter for landing intensity (0-1, based on fall height/damage)")]
        public string ParamLandingIntensity = "LandIntensity";
        
        [Tooltip("Bool parameter - true during landing recovery")]
        public string ParamIsRecovering = "IsRecovering";
        
        [Tooltip("Float parameter - recovery progress (0 to 1)")]
        public string ParamRecoveryProgress = "RecoveryProgress";
        #endregion
        
        #region Blend Parameters
        [Header("Blend Parameters (Standard)")]
        [Tooltip("Float parameter for movement speed blending")]
        public string ParamSpeed = "Speed";
        
        [Tooltip("Bool parameter for crouch state")]
        public string ParamIsCrouch = "IsCrouch";
        #endregion
        
        #region Root Motion Settings
        [Header("Root Motion Settings")]
        [Tooltip("Apply animator root motion during landing recovery animations. " +
                 "NOTE: Only enable if this bridge is on the same GameObject as the CharacterController. " +
                 "For hybrid setups (Ghost=CharacterController, UI=Animator), leave this FALSE and let DOTS handle movement.")]
        public bool ApplyRootMotion = false;
        
        [Tooltip("Scale factor for root motion translation (1.0 = full, 0.5 = half). Only used if ApplyRootMotion is true.")]
        [Range(0f, 2f)]
        public float RootMotionScale = 1f;
        
        [Tooltip("Only apply root motion while IsRecovering is true")]
        public bool RootMotionOnlyDuringRecovery = true;
        #endregion
        
        #region Recovery Timing
        [Header("Recovery Timing")]
        [Tooltip("Duration of soft landing recovery animation (low intensity)")]
        public float SoftLandingDuration = 0.3f;
        
        [Tooltip("Duration of hard landing recovery animation (high intensity)")]
        public float HardLandingDuration = 1.5f;
        
        [Tooltip("Intensity threshold for hard landing (0-1)")]
        [Range(0f, 1f)]
        public float HardLandingThreshold = 0.5f;
        #endregion
        
        #region Events
        [Header("Events (for Audio/VFX)")]
        [Tooltip("Invoked when landing occurs - use for footstep/impact audio")]
        public UnityEvent<float> OnLanding;
        
        [Tooltip("Invoked when recovery starts - use for grunt/strain audio")]
        public UnityEvent OnRecoveryStart;
        
        [Tooltip("Invoked when recovery completes - use for 'ready' audio cue")]
        public UnityEvent OnRecoveryComplete;
        
        [Tooltip("Invoked by animation event 'OnLandingImpact' - precise frame audio/VFX")]
        public UnityEvent OnLandingImpactEvent;
        
        [Tooltip("Invoked by animation event 'OnRecoveryStep' - for foot plant audio")]
        public UnityEvent OnRecoveryStepEvent;
        #endregion
        
        #region Debug
        [Header("Debug")]
        [Tooltip("Log landing events to console")]
        public bool DebugLogging = false;
        #endregion
        
        // Cached hashes
        private int _landingTriggerHash;
        private int _landingIntensityHash;
        private int _isRecoveringHash;
        private int _recoveryProgressHash;
        private int _speedHash;
        private int _isCrouchHash;
        
        // State tracking
        private bool _isRecovering;
        private float _recoveryTimer;
        private float _recoveryDuration;
        private float _currentIntensity;
        
        #region Unity Lifecycle
        private void Reset()
        {
            Animator = GetComponent<Animator>();
            CharacterController = GetComponent<CharacterController>();
        }
        
        private void Awake()
        {
            if (Animator == null)
                Animator = GetComponentInChildren<Animator>();
            
            // Note: CharacterController is NOT auto-found intentionally.
            // For hybrid Ghost/UI setups, the CharacterController is on the Ghost prefab,
            // not on the UI prefab where this bridge lives. Leave it null and root motion
            // will move the visual transform directly (or disable ApplyRootMotion entirely).
            
            if (Animator == null)
            {
                Debug.LogWarning($"[LandingAnimatorBridge] No Animator found on {gameObject.name}. Landing animations will not play.");
                enabled = false;
                return;
            }
            
            CacheParameterHashes();
            
            if (DebugLogging)
                Debug.Log($"[LandingAnimatorBridge] Initialized on {gameObject.name}");
        }
        
        private void Update()
        {
            if (!_isRecovering) return;
            
            _recoveryTimer -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(_recoveryTimer / _recoveryDuration);
            
            SetRecoveryProgress(progress);
            
            if (_recoveryTimer <= 0f)
            {
                EndRecovery();
            }
        }
        
        private void OnAnimatorMove()
        {
            if (!ApplyRootMotion) return;
            if (Animator == null) return;
            if (RootMotionOnlyDuringRecovery && !_isRecovering) return;
            
            Vector3 deltaPosition = Animator.deltaPosition * RootMotionScale;
            
            if (CharacterController != null && CharacterController.enabled)
            {
                CharacterController.Move(deltaPosition);
            }
            else
            {
                transform.position += deltaPosition;
            }
            
            // EPIC 15.20: Don't apply animator rotation - ECS PlayerMovementSystem controls rotation
            // In screen-relative/isometric modes, character must face movement direction, not animator deltas
            // transform.rotation *= Animator.deltaRotation;
        }
        #endregion
        
        #region Private Methods
        private void CacheParameterHashes()
        {
            _landingTriggerHash = Animator.StringToHash(ParamLandingTrigger);
            _landingIntensityHash = Animator.StringToHash(ParamLandingIntensity);
            _isRecoveringHash = Animator.StringToHash(ParamIsRecovering);
            _recoveryProgressHash = Animator.StringToHash(ParamRecoveryProgress);
            _speedHash = Animator.StringToHash(ParamSpeed);
            _isCrouchHash = Animator.StringToHash(ParamIsCrouch);
        }
        
        private void SetRecoveryProgress(float progress)
        {
            if (Animator == null) return;
            if (!string.IsNullOrEmpty(ParamRecoveryProgress))
                Animator.SetFloat(_recoveryProgressHash, progress);
        }
        
        private void EndRecovery()
        {
            _isRecovering = false;
            _recoveryTimer = 0f;
            
            if (Animator != null && !string.IsNullOrEmpty(ParamIsRecovering))
                Animator.SetBool(_isRecoveringHash, false);
            
            OnRecoveryComplete?.Invoke();
            
            if (DebugLogging)
                Debug.Log($"[LandingAnimatorBridge] Recovery complete");
        }
        #endregion
        
        #region Public API
        /// <summary>
        /// Trigger landing animation with specified intensity.
        /// Called by LandingAnimationAdapter when LandingFlag is detected.
        /// </summary>
        /// <param name="intensity">Landing intensity (0-1) based on fall height/damage</param>
        public void TriggerLandingWithIntensity(float intensity)
        {
            if (Animator == null) return;
            
            _currentIntensity = Mathf.Clamp01(intensity);
            
            // Set intensity parameter
            if (!string.IsNullOrEmpty(ParamLandingIntensity))
                Animator.SetFloat(_landingIntensityHash, _currentIntensity);
            
            // Trigger landing animation
            if (!string.IsNullOrEmpty(ParamLandingTrigger))
                Animator.SetTrigger(_landingTriggerHash);
            
            // Start recovery
            _isRecovering = true;
            _recoveryDuration = _currentIntensity >= HardLandingThreshold ? HardLandingDuration : SoftLandingDuration;
            _recoveryTimer = _recoveryDuration;
            
            if (!string.IsNullOrEmpty(ParamIsRecovering))
                Animator.SetBool(_isRecoveringHash, true);
            
            SetRecoveryProgress(0f);
            
            // Fire events
            OnLanding?.Invoke(_currentIntensity);
            OnRecoveryStart?.Invoke();
            
            if (DebugLogging)
                Debug.Log($"[LandingAnimatorBridge] Landing - Intensity: {_currentIntensity:F2}, Recovery: {_recoveryDuration:F2}s");
        }
        
        /// <summary>
        /// Cancel recovery early (e.g., if player takes another action).
        /// </summary>
        public void CancelRecovery()
        {
            if (!_isRecovering) return;
            
            _isRecovering = false;
            _recoveryTimer = 0f;
            
            if (Animator != null && !string.IsNullOrEmpty(ParamIsRecovering))
                Animator.SetBool(_isRecoveringHash, false);
            
            if (DebugLogging)
                Debug.Log($"[LandingAnimatorBridge] Recovery cancelled");
        }
        
        /// <summary>
        /// Get current recovery progress (0-1). Returns 1 if not recovering.
        /// </summary>
        public float GetRecoveryProgress()
        {
            if (!_isRecovering) return 1f;
            return 1f - Mathf.Clamp01(_recoveryTimer / _recoveryDuration);
        }
        
        /// <summary>
        /// Check if currently in landing recovery.
        /// </summary>
        public bool IsRecovering => _isRecovering;
        #endregion
        
        #region Animation Event Receivers
        /// <summary>
        /// Called by animation event at precise impact frame.
        /// Add this event to your landing animation clip.
        /// </summary>
        public void OnLandingImpact()
        {
            OnLandingImpactEvent?.Invoke();
            
            if (DebugLogging)
                Debug.Log($"[LandingAnimatorBridge] Animation event: OnLandingImpact");
        }
        
        /// <summary>
        /// Called by animation event when foot plants during recovery.
        /// Add this event to your get-up/recovery animation clip.
        /// </summary>
        public void OnRecoveryStep()
        {
            OnRecoveryStepEvent?.Invoke();
            
            if (DebugLogging)
                Debug.Log($"[LandingAnimatorBridge] Animation event: OnRecoveryStep");
        }
        
        /// <summary>
        /// Generic animation event handler for custom events.
        /// Add string events in your animation clips to trigger this.
        /// </summary>
        public void OnAnimationEvent(string eventName)
        {
            if (DebugLogging)
                Debug.Log($"[LandingAnimatorBridge] Animation event: {eventName}");
            
            // Handle specific named events
            switch (eventName)
            {
                case "LandingImpact":
                    OnLandingImpact();
                    break;
                case "RecoveryStep":
                    OnRecoveryStep();
                    break;
            }
        }
        #endregion
        
        #region IPlayerAnimationBridge Implementation
        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            if (Animator == null) return;
            
            if (!string.IsNullOrEmpty(ParamSpeed))
                Animator.SetFloat(_speedHash, state.MoveSpeed);
            
            if (!string.IsNullOrEmpty(ParamIsCrouch))
                Animator.SetBool(_isCrouchHash, state.IsCrouching);
        }

        public void TriggerLanding()
        {
            // Default intensity for basic landing trigger (medium impact)
            TriggerLandingWithIntensity(0.3f);
        }
        #endregion
    }
}
