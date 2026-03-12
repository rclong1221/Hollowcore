using UnityEngine;
using Player.Systems;

namespace Player.Bridges
{
    /// <summary>
    /// Animator bridge for slide mechanic.
    /// Drives animator parameters for slide animations and handles animation events.
    /// </summary>
    [DisallowMultipleComponent]
    public class SlideAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        [Header("Animator")]
        [SerializeField] Animator animator;

        [Header("Parameter Names")]
        [Tooltip("Bool parameter - true when sliding")]
        [SerializeField] string paramIsSliding = "IsSliding";
        
        [Tooltip("Float parameter - current slide speed (m/s)")]
        [SerializeField] string paramSlideSpeed = "SlideSpeed";
        
        [Tooltip("Int parameter - trigger type (0=Manual, 1=Slope, 2=Slippery)")]
        [SerializeField] string paramSlideTriggerType = "SlideTriggerType";
        
        [Header("Settings")]
        [Tooltip("Enable debug logging")]
        [SerializeField] bool enableDebugLog = false;

        int h_IsSliding;
        int h_SlideSpeed;
        int h_SlideTriggerType;
        
        private bool _isSliding;
        private float _slideSpeed;
        private int _triggerType;

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
            h_IsSliding = !string.IsNullOrEmpty(paramIsSliding) ? Animator.StringToHash(paramIsSliding) : 0;
            h_SlideSpeed = !string.IsNullOrEmpty(paramSlideSpeed) ? Animator.StringToHash(paramSlideSpeed) : 0;
            h_SlideTriggerType = !string.IsNullOrEmpty(paramSlideTriggerType) ? Animator.StringToHash(paramSlideTriggerType) : 0;
        }

        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            if (animator == null) return;
            
            // SlideAnimatorBridge doesn't need to update based on generic animation state
            // Slide state is updated via StartSlide/EndSlide methods
        }

        public void TriggerLanding()
        {
            // no-op for slide bridge
        }

        /// <summary>
        /// Start slide animation with specified speed and trigger type.
        /// Called by SlideAnimationTriggerSystem.
        /// </summary>
        public void StartSlide(float speed, int triggerType)
        {
            if (animator == null) return;
            
            _isSliding = true;
            _slideSpeed = speed;
            _triggerType = triggerType;
            
            // Set animator parameters
            if (h_IsSliding != 0)
                animator.SetBool(h_IsSliding, true);
            
            if (h_SlideSpeed != 0)
                animator.SetFloat(h_SlideSpeed, speed);
            
            if (h_SlideTriggerType != 0)
                animator.SetInteger(h_SlideTriggerType, triggerType);
            
            if (enableDebugLog)
                Debug.Log($"[SlideAnimatorBridge] Start slide - speed={speed:F2} m/s, triggerType={triggerType}");
        }

        /// <summary>
        /// Update slide speed during active slide.
        /// </summary>
        public void UpdateSlideSpeed(float speed)
        {
            if (animator == null || !_isSliding) return;
            
            _slideSpeed = speed;
            
            if (h_SlideSpeed != 0)
                animator.SetFloat(h_SlideSpeed, speed);
        }

        /// <summary>
        /// End slide animation.
        /// Called by SlideAnimationTriggerSystem.
        /// </summary>
        public void EndSlide()
        {
            if (animator == null) return;
            
            _isSliding = false;
            _slideSpeed = 0f;
            
            // Clear animator parameters
            if (h_IsSliding != 0)
                animator.SetBool(h_IsSliding, false);
            
            if (h_SlideSpeed != 0)
                animator.SetFloat(h_SlideSpeed, 0f);
            
            if (enableDebugLog)
                Debug.Log($"[SlideAnimatorBridge] End slide");
        }

        #region Animation Events (called from animation clips)
        
        /// <summary>
        /// Animation event called when slide animation starts.
        /// Can be used to trigger VFX/audio at precise animation frame.
        /// </summary>
        public void OnSlideStart()
        {
            if (enableDebugLog)
                Debug.Log($"[SlideAnimatorBridge] OnSlideStart animation event");
            
            // TODO: Trigger slide start VFX/audio
            // AudioManager.PlaySlideStart(transform.position);
        }

        /// <summary>
        /// Animation event called when slide animation ends.
        /// Can be used to trigger VFX/audio at precise animation frame.
        /// </summary>
        public void OnSlideEnd()
        {
            if (enableDebugLog)
                Debug.Log($"[SlideAnimatorBridge] OnSlideEnd animation event");
            
            // TODO: Trigger slide end VFX/audio
            // AudioManager.PlaySlideEnd(transform.position);
        }

        /// <summary>
        /// Animation event called during slide for continuous audio/VFX.
        /// Can be placed on multiple frames for looping slide sounds.
        /// </summary>
        public void OnSlideLoop()
        {
            if (enableDebugLog)
                Debug.Log($"[SlideAnimatorBridge] OnSlideLoop animation event");
            
            // TODO: Play slide loop audio/particles
            // This could check ground material and play appropriate surface sounds
            // AudioManager.PlaySlideLoop(transform.position, GetGroundMaterial());
        }
        
        #endregion
    }
}
