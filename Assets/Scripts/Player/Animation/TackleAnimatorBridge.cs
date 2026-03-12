using UnityEngine;
using Player.Components;
using Player.Bridges;

namespace Player.Animation
{
    /// <summary>
    /// Client-side animation bridge for tackle state (Epic 7.4.2).
    /// This MonoBehaviour receives state changes from ECS animation systems
    /// and forwards them to the Animator for visual presentation.
    /// 
    /// DOTS remains authoritative for gameplay state; this component only drives animation.
    /// </summary>
    public class TackleAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        [Header("References")]
        [Tooltip("Animator component on this player character")]
        public Animator PlayerAnimator;
        
        [Header("Animator Parameters")]
        [Tooltip("Trigger parameter to start tackle lunge")]
        public string TackleTriggerParam = "TackleTrigger";
        
        [Tooltip("Trigger parameter for successful hit reaction")]
        public string TackleHitTriggerParam = "TackleHitTrigger";
        
        [Tooltip("Trigger parameter for whiff/stumble")]
        public string TackleMissTriggerParam = "TackleMissTrigger";
        
        [Tooltip("Bool parameter - true during tackle")]
        public string IsTacklingParam = "IsTackling";
        
        [Tooltip("Float parameter - tackle speed/intensity (0-1)")]
        public string TackleSpeedParam = "TackleSpeed";
        
        [Header("Debug")]
        [Tooltip("Log tackle events to console")]
        public bool DebugLogging = true;
        
        // Animator parameter hashes
        private int _tackleTriggerHash;
        private int _tackleHitTriggerHash;
        private int _tackleMissTriggerHash;
        private int _isTacklingHash;
        private int _tackleSpeedHash;
        
        private void Awake()
        {
            if (PlayerAnimator == null)
            {
                PlayerAnimator = GetComponentInChildren<Animator>();
            }
            
            if (PlayerAnimator == null)
            {
                Debug.LogWarning($"[TackleAnimatorBridge] No Animator found on {gameObject.name}. Tackle animations will not play.");
                enabled = false;
                return;
            }
            
            // Cache animator parameter hashes for performance
            _tackleTriggerHash = Animator.StringToHash(TackleTriggerParam);
            _tackleHitTriggerHash = Animator.StringToHash(TackleHitTriggerParam);
            _tackleMissTriggerHash = Animator.StringToHash(TackleMissTriggerParam);
            _isTacklingHash = Animator.StringToHash(IsTacklingParam);
            _tackleSpeedHash = Animator.StringToHash(TackleSpeedParam);
            
            Debug.Log($"[TackleAnimatorBridge] Initialized on {gameObject.name}");
        }
        
        /// <summary>
        /// Called when player initiates a tackle.
        /// </summary>
        /// <param name="tackleSpeed">Speed of tackle (for animation intensity)</param>
        public void TriggerTackle(float tackleSpeed)
        {
            if (PlayerAnimator == null) return;
            
            float intensity = Mathf.Clamp01(tackleSpeed / 10f);
            
            PlayerAnimator.SetTrigger(_tackleTriggerHash);
            PlayerAnimator.SetBool(_isTacklingHash, true);
            PlayerAnimator.SetFloat(_tackleSpeedHash, intensity);
            
            if (DebugLogging)
            {
                Debug.Log($"[TackleAnimatorBridge] TriggerTackle - Speed: {tackleSpeed:F2}, Intensity: {intensity:F2}");
            }
        }
        
        /// <summary>
        /// Called when tackle hits a target.
        /// </summary>
        public void TriggerTackleHit()
        {
            if (PlayerAnimator == null) return;
            
            PlayerAnimator.SetTrigger(_tackleHitTriggerHash);
            PlayerAnimator.SetBool(_isTacklingHash, false);
            
            if (DebugLogging)
            {
                Debug.Log($"[TackleAnimatorBridge] TriggerTackleHit");
            }
        }
        
        /// <summary>
        /// Called when tackle misses (whiff/stumble).
        /// </summary>
        public void TriggerTackleMiss()
        {
            if (PlayerAnimator == null) return;
            
            PlayerAnimator.SetTrigger(_tackleMissTriggerHash);
            PlayerAnimator.SetBool(_isTacklingHash, false);
            
            if (DebugLogging)
            {
                Debug.Log($"[TackleAnimatorBridge] TriggerTackleMiss");
            }
        }
        
        /// <summary>
        /// Called when tackle ends (normally or aborted).
        /// </summary>
        public void EndTackle()
        {
            if (PlayerAnimator == null) return;
            
            PlayerAnimator.SetBool(_isTacklingHash, false);
            PlayerAnimator.SetFloat(_tackleSpeedHash, 0f);
            
            if (DebugLogging)
            {
                Debug.Log($"[TackleAnimatorBridge] EndTackle");
            }
        }
        
        // IPlayerAnimationBridge interface implementation
        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            // Tackle uses its own dedicated system, not the standard animation state
        }
        
        public void TriggerLanding()
        {
            // No-op: tackle has its own landing/recovery logic
        }
        
        public void OnAnimationEvent(string eventName)
        {
            if (DebugLogging)
            {
                Debug.Log($"[TackleAnimatorBridge] Animation event: {eventName}");
            }
        }
    }
}
