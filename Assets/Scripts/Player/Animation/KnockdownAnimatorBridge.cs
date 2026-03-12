using UnityEngine;
using Player.Components;
using Player.Bridges;

namespace Player.Animation
{
    /// <summary>
    /// Client-side animation bridge for stagger and knockdown states (Epic 7.4.1).
    /// This MonoBehaviour receives state changes from ECS animation systems
    /// and forwards them to the Animator for visual presentation.
    /// 
    /// DOTS remains authoritative for gameplay state; this component only drives animation.
    /// </summary>
    public class KnockdownAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        [Header("References")]
        [Tooltip("Animator component on this player character")]
        public Animator PlayerAnimator;
        
        [Header("Stagger Parameters")]
        [Tooltip("Trigger parameter to start stagger animation")]
        public string StaggerTriggerParam = "StaggerTrigger";
        
        [Tooltip("Bool parameter - true during stagger")]
        public string IsStaggeredParam = "IsStaggered";
        
        [Tooltip("Float parameter - stagger intensity (0-1)")]
        public string StaggerIntensityParam = "StaggerIntensity";
        
        [Header("Knockdown Parameters")]
        [Tooltip("Trigger parameter to start knockdown animation")]
        public string KnockdownTriggerParam = "KnockdownTrigger";
        
        [Tooltip("Trigger parameter to start recovery/get-up animation")]
        public string RecoveryTriggerParam = "RecoveryTrigger";
        
        [Tooltip("Bool parameter - true during entire knockdown")]
        public string IsKnockedDownParam = "IsKnockedDown";
        
        [Tooltip("Bool parameter - true during recovery phase only")]
        public string IsRecoveringParam = "IsRecovering";
        
        [Tooltip("Float parameter - animation intensity based on impact (0-1)")]
        public string KnockdownIntensityParam = "KnockdownIntensity";
        
        [Header("Debug")]
        [Tooltip("Log stagger/knockdown events to console")]
        public bool DebugLogging = true;
        
        // Stagger hashes
        private int _staggerTriggerHash;
        private int _isStaggeredHash;
        private int _staggerIntensityHash;
        
        // Knockdown hashes
        private int _knockdownTriggerHash;
        private int _recoveryTriggerHash;
        private int _isKnockedDownHash;
        private int _isRecoveringHash;
        private int _knockdownIntensityHash;
        
        private void Awake()
        {
            if (PlayerAnimator == null)
            {
                PlayerAnimator = GetComponentInChildren<Animator>();
            }
            
            if (PlayerAnimator == null)
            {
                Debug.LogWarning($"[KnockdownAnimatorBridge] No Animator found on {gameObject.name}. Stagger/knockdown animations will not play.");
                enabled = false;
                return;
            }
            
            // Cache animator parameter hashes for performance
            _staggerTriggerHash = Animator.StringToHash(StaggerTriggerParam);
            _isStaggeredHash = Animator.StringToHash(IsStaggeredParam);
            _staggerIntensityHash = Animator.StringToHash(StaggerIntensityParam);
            
            _knockdownTriggerHash = Animator.StringToHash(KnockdownTriggerParam);
            _recoveryTriggerHash = Animator.StringToHash(RecoveryTriggerParam);
            _isKnockedDownHash = Animator.StringToHash(IsKnockedDownParam);
            _isRecoveringHash = Animator.StringToHash(IsRecoveringParam);
            _knockdownIntensityHash = Animator.StringToHash(KnockdownIntensityParam);
            
            Debug.Log($"[KnockdownAnimatorBridge] Initialized on {gameObject.name}");
        }
        
        /// <summary>
        /// Called when player enters stagger state (brief stumble).
        /// </summary>
        /// <param name="intensity">Stagger intensity (0-1) based on collision power</param>
        public void TriggerStagger(float intensity)
        {
            if (PlayerAnimator == null) return;
            
            float clampedIntensity = Mathf.Clamp01(intensity);
            
            PlayerAnimator.SetTrigger(_staggerTriggerHash);
            PlayerAnimator.SetBool(_isStaggeredHash, true);
            PlayerAnimator.SetFloat(_staggerIntensityHash, clampedIntensity);
            
            if (DebugLogging)
            {
                Debug.Log($"[KnockdownAnimatorBridge] TriggerStagger - Intensity: {clampedIntensity:F2}");
            }
        }
        
        /// <summary>
        /// Called when stagger ends.
        /// </summary>
        public void EndStagger()
        {
            if (PlayerAnimator == null) return;
            
            PlayerAnimator.SetBool(_isStaggeredHash, false);
            PlayerAnimator.SetFloat(_staggerIntensityHash, 0f);
            
            if (DebugLogging)
            {
                Debug.Log($"[KnockdownAnimatorBridge] EndStagger");
            }
        }
        
        /// <summary>
        /// Called when player enters knockdown state (on ground).
        /// </summary>
        /// <param name="impactSpeed">Collision impact speed for animation intensity (0-10 m/s)</param>
        public void TriggerKnockdown(float impactSpeed)
        {
            if (PlayerAnimator == null) return;
            
            // Normalize impact speed to 0-1 range for animator parameter
            float intensity = Mathf.Clamp01(impactSpeed / 10f);
            
            PlayerAnimator.SetTrigger(_knockdownTriggerHash);
            PlayerAnimator.SetBool(_isKnockedDownHash, true);
            PlayerAnimator.SetBool(_isRecoveringHash, false);
            PlayerAnimator.SetFloat(_knockdownIntensityHash, intensity);
            
            if (DebugLogging)
            {
                Debug.Log($"[KnockdownAnimatorBridge] TriggerKnockdown - Impact: {impactSpeed:F2} m/s, Intensity: {intensity:F2}");
            }
        }
        
        /// <summary>
        /// Called when player starts recovery phase (getting up).
        /// </summary>
        public void StartRecovery()
        {
            if (PlayerAnimator == null) return;
            
            PlayerAnimator.SetTrigger(_recoveryTriggerHash);
            PlayerAnimator.SetBool(_isRecoveringHash, true);
            
            if (DebugLogging)
            {
                Debug.Log($"[KnockdownAnimatorBridge] StartRecovery");
            }
        }
        
        /// <summary>
        /// Called when knockdown fully ends (player back to normal).
        /// </summary>
        public void EndKnockdown()
        {
            if (PlayerAnimator == null) return;
            
            PlayerAnimator.SetBool(_isKnockedDownHash, false);
            PlayerAnimator.SetBool(_isRecoveringHash, false);
            PlayerAnimator.SetFloat(_knockdownIntensityHash, 0f);
            
            if (DebugLogging)
            {
                Debug.Log($"[KnockdownAnimatorBridge] EndKnockdown");
            }
        }
        
        // IPlayerAnimationBridge interface implementation
        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            // Knockdown doesn't use the standard animation state system
            // State is managed by the dedicated knockdown systems
        }
        
        public void TriggerLanding()
        {
            // No-op: knockdown has its own landing/recovery logic
        }
        
        public void OnAnimationEvent(string eventName)
        {
            // Hook for animation events if needed (e.g., sound effects at specific frames)
            if (DebugLogging)
            {
                Debug.Log($"[KnockdownAnimatorBridge] Animation event: {eventName}");
            }
        }
    }
}
