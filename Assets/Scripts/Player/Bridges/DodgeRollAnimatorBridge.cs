using UnityEngine;
using System.Collections.Generic;
using Player.Systems;

namespace Player.Bridges
{
    /// <summary>
    /// Optional animator bridge for custom roll animation parameters (RollTrigger, IsRolling, RollProgress).
    /// If the Animator Controller lacks these parameters, this bridge silently no-ops.
    /// The primary animation path for Opsive controllers is AbilityIndex/AbilityChange via ClimbAnimatorBridge.
    /// </summary>
    [DisallowMultipleComponent]
    public class DodgeRollAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        [Header("Animator")]
        [SerializeField] Animator animator;

        [Header("Parameter Names")]
        [SerializeField] string paramRoll = "RollTrigger";
        [SerializeField] string paramIsRolling = "IsRolling";
        [SerializeField] string paramRollProgress = "RollProgress";
        [SerializeField] string paramSpeed = "Speed";

        [Header("Settings")]
        [Tooltip("Enable debug logging")]
        [SerializeField] bool enableDebugLog = false;

        int h_Roll;
        int h_IsRolling;
        int h_RollProgress;
        int h_Speed;

        private bool _isRolling;
        private float _rollStartTime;

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
            if (animator == null) animator = GetComponentInChildren<Animator>();
            CacheHashes();
        }

        void CacheHashes()
        {
            h_Roll = !string.IsNullOrEmpty(paramRoll) ? Animator.StringToHash(paramRoll) : 0;
            h_IsRolling = !string.IsNullOrEmpty(paramIsRolling) ? Animator.StringToHash(paramIsRolling) : 0;
            h_RollProgress = !string.IsNullOrEmpty(paramRollProgress) ? Animator.StringToHash(paramRollProgress) : 0;
            h_Speed = !string.IsNullOrEmpty(paramSpeed) ? Animator.StringToHash(paramSpeed) : 0;

            // Validate against actual Animator parameters to prevent "Parameter does not exist" spam
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var validHashes = new HashSet<int>();
                foreach (var param in animator.parameters)
                    validHashes.Add(param.nameHash);

                if (!validHashes.Contains(h_Roll)) h_Roll = 0;
                if (!validHashes.Contains(h_IsRolling)) h_IsRolling = 0;
                if (!validHashes.Contains(h_RollProgress)) h_RollProgress = 0;
                // Keep h_Speed — Speed parameter usually exists in Opsive controllers
                if (!validHashes.Contains(h_Speed)) h_Speed = 0;
            }
        }

        void Update()
        {
            if (animator == null) return;
            
            // Update roll progress parameter if rolling
            if (_isRolling)
            {
                float elapsed = Time.time - _rollStartTime;
                float estimatedDuration = 0.6f; // Match DodgeRollComponent.Default.Duration
                float progress = UnityEngine.Mathf.Clamp01(elapsed / estimatedDuration);
                
                if (h_RollProgress != 0)
                    animator.SetFloat(h_RollProgress, progress);
                
                // Fallback: Auto-complete roll after duration if EndRoll wasn't called
                if (elapsed >= estimatedDuration)
                {
                    EndRoll();
                }
            }
        }

        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            if (animator == null) return;
            if (h_Speed != 0) animator.SetFloat(h_Speed, state.MoveSpeed);
        }

        public void TriggerLanding()
        {
            // no-op for roll bridge
        }

        /// <summary>
        /// Trigger a roll animation. Called by DodgeRollAnimationTriggerSystem.
        /// </summary>
        public void TriggerRoll()
        {
            if (animator == null) return;
            
            // Set trigger to start roll animation
            if (h_Roll != 0)
                animator.SetTrigger(h_Roll);
            
            // Set rolling bool
            if (h_IsRolling != 0)
                animator.SetBool(h_IsRolling, true);
            
            // Reset progress
            if (h_RollProgress != 0)
                animator.SetFloat(h_RollProgress, 0f);
            
            _isRolling = true;
            _rollStartTime = Time.time;

            if (enableDebugLog)
            {
                Debug.Log($"[DodgeRollAnimatorBridge] TriggerRoll called on {gameObject.name}");
            }
        }

        /// <summary>
        /// End the roll animation. Called by DodgeRollAnimationTriggerSystem when roll completes.
        /// </summary>
        public void EndRoll()
        {
            if (animator == null) return;

            _isRolling = false;
            
            if (h_IsRolling != 0)
                animator.SetBool(h_IsRolling, false);
            
            if (h_RollProgress != 0)
                animator.SetFloat(h_RollProgress, 0f);

            if (enableDebugLog)
            {
                Debug.Log($"[DodgeRollAnimatorBridge] EndRoll called on {gameObject.name}");
            }
        }
    }
}
