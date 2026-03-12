using UnityEngine;
using Player.Systems;

namespace Player.Bridges
{
    /// <summary>
    /// MonoBehaviour bridge that drives Animator parameters for mantle and vault animations.
    /// Triggered by MantleAnimationTriggerSystem when DOTS detects mantle/vault state changes.
    /// Handles animation triggers, progress tracking, and optional IK for hand placement.
    /// </summary>
    [DisallowMultipleComponent]
    public class MantleAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        [Header("Animator")]
        [SerializeField] private Animator animator;

        [Header("Mantle Parameter Names")]
        [SerializeField] private string paramMantleTrigger = "MantleTrigger";
        [SerializeField] private string paramIsMantling = "IsMantling";
        [SerializeField] private string paramMantleProgress = "MantleProgress";
        
        [Header("Vault Parameter Names")]
        [SerializeField] private string paramVaultTrigger = "VaultTrigger";
        [SerializeField] private string paramIsVaulting = "IsVaulting";
        [SerializeField] private string paramVaultProgress = "VaultProgress";
        
        [Header("IK Settings")]
        [Tooltip("Enable IK hand placement on ledges during mantle")]
        [SerializeField] private bool enableHandIK = false;
        
        [Tooltip("Left hand IK weight curve during mantle")]
        [SerializeField] private AnimationCurve leftHandIKCurve = AnimationCurve.EaseInOut(0f, 0f, 0.3f, 1f);
        
        [Tooltip("Right hand IK weight curve during mantle")]
        [SerializeField] private AnimationCurve rightHandIKCurve = AnimationCurve.EaseInOut(0f, 0f, 0.3f, 1f);
        
        [Header("Settings")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLog = false;

        // Cached parameter hashes
        private int h_MantleTrigger;
        private int h_IsMantling;
        private int h_MantleProgress;
        private int h_VaultTrigger;
        private int h_IsVaulting;
        private int h_VaultProgress;
        
        // Runtime state
        private bool _isMantling;
        private bool _isVaulting;
        private float _startTime;
        private float _duration;
        private Vector3 _ledgePosition;

        void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            CacheHashes();
        }

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            CacheHashes();
        }

        void OnValidate()
        {
            CacheHashes();
        }

        void CacheHashes()
        {
            h_MantleTrigger = !string.IsNullOrEmpty(paramMantleTrigger) ? Animator.StringToHash(paramMantleTrigger) : 0;
            h_IsMantling = !string.IsNullOrEmpty(paramIsMantling) ? Animator.StringToHash(paramIsMantling) : 0;
            h_MantleProgress = !string.IsNullOrEmpty(paramMantleProgress) ? Animator.StringToHash(paramMantleProgress) : 0;
            h_VaultTrigger = !string.IsNullOrEmpty(paramVaultTrigger) ? Animator.StringToHash(paramVaultTrigger) : 0;
            h_IsVaulting = !string.IsNullOrEmpty(paramIsVaulting) ? Animator.StringToHash(paramIsVaulting) : 0;
            h_VaultProgress = !string.IsNullOrEmpty(paramVaultProgress) ? Animator.StringToHash(paramVaultProgress) : 0;
        }

        void Update()
        {
            if (animator == null) return;
            
            // Update mantle progress
            if (_isMantling)
            {
                float elapsed = Time.time - _startTime;
                float progress = Mathf.Clamp01(elapsed / _duration);
                
                if (h_MantleProgress != 0)
                    animator.SetFloat(h_MantleProgress, progress);
                
                // Auto-complete if duration exceeded
                if (elapsed >= _duration + 0.1f)
                {
                    EndMantle();
                }
            }
            
            // Update vault progress
            if (_isVaulting)
            {
                float elapsed = Time.time - _startTime;
                float progress = Mathf.Clamp01(elapsed / _duration);
                
                if (h_VaultProgress != 0)
                    animator.SetFloat(h_VaultProgress, progress);
                
                // Auto-complete if duration exceeded
                if (elapsed >= _duration + 0.1f)
                {
                    EndVault();
                }
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (!enableHandIK || animator == null || !_isMantling)
                return;
            
            float elapsed = Time.time - _startTime;
            float progress = Mathf.Clamp01(elapsed / _duration);
            
            // Apply hand IK weights based on progress
            float leftWeight = leftHandIKCurve.Evaluate(progress);
            float rightWeight = rightHandIKCurve.Evaluate(progress);
            
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightWeight);
            
            // Position hands on ledge (simplified - could be improved with raycast)
            if (leftWeight > 0.01f)
            {
                Vector3 leftHandPos = _ledgePosition + new Vector3(-0.3f, 0f, 0f);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandPos);
            }
            
            if (rightWeight > 0.01f)
            {
                Vector3 rightHandPos = _ledgePosition + new Vector3(0.3f, 0f, 0f);
                animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandPos);
            }
        }

        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            // Mantle/vault state is driven by specific triggers, not continuous state
        }

        public void TriggerLanding()
        {
            // no-op for mantle bridge
        }

        /// <summary>
        /// Trigger a mantle animation. Called by MantleAnimationTriggerSystem.
        /// </summary>
        public void TriggerMantle(Vector3 ledgePosition, float duration)
        {
            if (animator == null) return;
            
            // Set trigger to start mantle animation
            if (h_MantleTrigger != 0)
                animator.SetTrigger(h_MantleTrigger);
            
            // Set mantling bool
            if (h_IsMantling != 0)
                animator.SetBool(h_IsMantling, true);
            
            // Reset progress
            if (h_MantleProgress != 0)
                animator.SetFloat(h_MantleProgress, 0f);
            
            _isMantling = true;
            _startTime = Time.time;
            _duration = duration;
            _ledgePosition = ledgePosition;

            if (enableDebugLog)
            {
                Debug.Log($"[MantleAnimatorBridge] TriggerMantle called on {gameObject.name}, duration={duration:F2}s");
            }
        }

        /// <summary>
        /// End the mantle animation. Called by MantleAnimationTriggerSystem when mantle completes.
        /// </summary>
        public void EndMantle()
        {
            if (animator == null) return;

            _isMantling = false;
            
            if (h_IsMantling != 0)
                animator.SetBool(h_IsMantling, false);
            
            if (h_MantleProgress != 0)
                animator.SetFloat(h_MantleProgress, 0f);

            if (enableDebugLog)
            {
                Debug.Log($"[MantleAnimatorBridge] EndMantle called on {gameObject.name}");
            }
        }

        /// <summary>
        /// Trigger a vault animation. Called by MantleAnimationTriggerSystem.
        /// </summary>
        public void TriggerVault(float duration)
        {
            if (animator == null) return;
            
            // Set trigger to start vault animation
            if (h_VaultTrigger != 0)
                animator.SetTrigger(h_VaultTrigger);
            
            // Set vaulting bool
            if (h_IsVaulting != 0)
                animator.SetBool(h_IsVaulting, true);
            
            // Reset progress
            if (h_VaultProgress != 0)
                animator.SetFloat(h_VaultProgress, 0f);
            
            _isVaulting = true;
            _startTime = Time.time;
            _duration = duration;

            if (enableDebugLog)
            {
                Debug.Log($"[MantleAnimatorBridge] TriggerVault called on {gameObject.name}, duration={duration:F2}s");
            }
        }

        /// <summary>
        /// End the vault animation. Called by MantleAnimationTriggerSystem when vault completes.
        /// </summary>
        public void EndVault()
        {
            if (animator == null) return;

            _isVaulting = false;
            
            if (h_IsVaulting != 0)
                animator.SetBool(h_IsVaulting, false);
            
            if (h_VaultProgress != 0)
                animator.SetFloat(h_VaultProgress, 0f);

            if (enableDebugLog)
            {
                Debug.Log($"[MantleAnimatorBridge] EndVault called on {gameObject.name}");
            }
        }
    }
}
