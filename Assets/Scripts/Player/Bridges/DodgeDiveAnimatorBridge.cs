using UnityEngine;
using System.Collections.Generic;

namespace Player.Bridges
{
    /// <summary>
    /// Client-side animator bridge for dodge dive animations.
    /// Triggered by DodgeDiveAnimationTriggerSystem when dive state is detected.
    /// Handles animation triggers and can optionally handle animation events.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class DodgeDiveAnimatorBridge : MonoBehaviour
    {
        [Header("Animation Parameters")]
        [Tooltip("Name of the trigger parameter for starting dive animation")]
        public string DiveTriggerName = "DodgeDive";
        
        [Tooltip("Name of the bool parameter indicating dive is active")]
        public string IsDivingParamName = "IsDiving";
        
        [Tooltip("Name of the float parameter for dive progress (0-1)")]
        public string DiveProgressParamName = "DiveProgress";

        [Header("Settings")]
        [Tooltip("Enable debug logging")]
        public bool EnableDebugLog = false;

        private Animator _animator;
        private float _diveStartTime;
        private bool _isDiving;

        // Cached hashes
        private int h_DiveTrigger;
        private int h_IsDiving;
        private int h_DiveProgress;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null && EnableDebugLog)
            {
                Debug.LogError($"[DodgeDiveAnimatorBridge] No Animator found on {gameObject.name}");
            }
            CacheHashes();
        }

        private void OnValidate()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            CacheHashes();
        }

        private void CacheHashes()
        {
            h_DiveTrigger = !string.IsNullOrEmpty(DiveTriggerName) ? Animator.StringToHash(DiveTriggerName) : 0;
            h_IsDiving = !string.IsNullOrEmpty(IsDivingParamName) ? Animator.StringToHash(IsDivingParamName) : 0;
            h_DiveProgress = !string.IsNullOrEmpty(DiveProgressParamName) ? Animator.StringToHash(DiveProgressParamName) : 0;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                // Verify existence to prevent "Parameter does not exist" spam
                var validHashes = new HashSet<int>();
                foreach (var param in _animator.parameters)
                {
                    validHashes.Add(param.nameHash);
                }

                if (!validHashes.Contains(h_DiveTrigger)) h_DiveTrigger = 0;
                if (!validHashes.Contains(h_IsDiving)) h_IsDiving = 0;
                if (!validHashes.Contains(h_DiveProgress)) h_DiveProgress = 0;
            }
        }

        /// <summary>
        /// Trigger a dive animation. Called by DodgeDiveAnimationTriggerSystem.
        /// </summary>
        public void TriggerDive()
        {
            if (_animator == null) return;

            // Set trigger to start dive animation
            if (h_DiveTrigger != 0)
                _animator.SetTrigger(h_DiveTrigger);
            
            // Set diving bool
            if (h_IsDiving != 0)
                _animator.SetBool(h_IsDiving, true);
            
            // Reset progress
            if (h_DiveProgress != 0)
                _animator.SetFloat(h_DiveProgress, 0f);
            
            _isDiving = true;
            _diveStartTime = Time.time;

            if (EnableDebugLog)
            {
                Debug.Log($"[DodgeDiveAnimatorBridge] TriggerDive called on {gameObject.name}");
            }
        }

        /// <summary>
        /// End the dive animation. Called by DodgeDiveAnimationTriggerSystem when dive completes.
        /// </summary>
        public void EndDive()
        {
            if (_animator == null) return;

            _isDiving = false;
            
            if (h_IsDiving != 0)
                _animator.SetBool(h_IsDiving, false);
            
            if (h_DiveProgress != 0)
                _animator.SetFloat(h_DiveProgress, 0f);

            if (EnableDebugLog)
            {
                Debug.Log($"[DodgeDiveAnimatorBridge] EndDive called on {gameObject.name}");
            }
        }

        /// <summary>
        /// Animation event callback for when dive completes and lands.
        /// Add this to the animation clip at the landing frame.
        /// </summary>
        public void OnDiveLand()
        {
            _isDiving = false;
            
            if (h_IsDiving != 0)
                _animator.SetBool(h_IsDiving, false);
            
            if (EnableDebugLog)
            {
                Debug.Log($"[DodgeDiveAnimatorBridge] OnDiveLand called on {gameObject.name}");
            }
        }

        /// <summary>
        /// Animation event callback for dive impact with ground.
        /// Can be used to trigger VFX/SFX.
        /// </summary>
        public void OnDiveImpact()
        {
            if (EnableDebugLog)
            {
                Debug.Log($"[DodgeDiveAnimatorBridge] OnDiveImpact called on {gameObject.name}");
            }
            
            // TODO: Trigger impact VFX/SFX here
            // Example: AudioManager.PlaySound("DiveImpact", transform.position);
        }

        /// <summary>
        /// Animation event callback for transition to prone.
        /// Add this to the animation clip where prone transition begins.
        /// </summary>
        public void OnTransitionToProne()
        {
            if (EnableDebugLog)
            {
                Debug.Log($"[DodgeDiveAnimatorBridge] OnTransitionToProne called on {gameObject.name}");
            }
        }

        private void Update()
        {
            if (_animator == null) return;

            // Update dive progress parameter if diving
            if (_isDiving)
            {
                float elapsed = Time.time - _diveStartTime;
                float estimatedDuration = 0.8f; // Match DodgeDiveComponent.Default.Duration
                float progress = Mathf.Clamp01(elapsed / estimatedDuration);
                
                if (h_DiveProgress != 0)
                    _animator.SetFloat(h_DiveProgress, progress);
                
                // Fallback: Auto-complete dive after duration if animation event wasn't called
                if (elapsed >= estimatedDuration)
                {
                    _isDiving = false;
                    
                    if (h_IsDiving != 0)
                        _animator.SetBool(h_IsDiving, false);
                    
                    if (h_DiveProgress != 0)
                        _animator.SetFloat(h_DiveProgress, 0f);
                    
                    if (EnableDebugLog)
                    {
                        Debug.Log($"[DodgeDiveAnimatorBridge] Auto-completed dive after {elapsed:F2}s");
                    }
                }
            }
            else
            {
                // Ensure progress is reset when not diving
                if (h_DiveProgress != 0 && _animator.GetFloat(h_DiveProgress) != 0f)
                {
                    _animator.SetFloat(h_DiveProgress, 0f);
                }
            }
        }

        private void OnDisable()
        {
            // Clean up state when disabled
            // Check if animator is valid and has a controller before setting parameters
            // (during shutdown/destroy, the controller may be null)
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                if (h_IsDiving != 0) _animator.SetBool(h_IsDiving, false);
                if (h_DiveProgress != 0) _animator.SetFloat(h_DiveProgress, 0f);
            }
            _isDiving = false;
        }
    }
}
