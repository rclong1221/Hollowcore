using UnityEngine;
using Player.Bridges;
using Player.Components;

namespace Player.Bridges
{
    [DisallowMultipleComponent]
    public class ProneAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        [Header("Animator")]
        [SerializeField] Animator animator;

        [Header("Parameter Names")]
        [SerializeField] string paramIsCrouching = "IsCrouching";
        [SerializeField] string paramIsProne = "IsProne";
        [SerializeField] string paramIsCrawling = "IsCrawling";
        [SerializeField] string paramProneBlend = "ProneBlend";
        [SerializeField] string paramLandTrigger = "Land";

        // runtime hashes computed from parameter name fields
        int h_IsCrouching;
        int h_IsProne;
        int h_IsCrawling;
        int h_ProneBlend;
        int h_LandTrigger;

        void Reset()
        {
            animator = GetComponentInChildren<Animator>();
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
            h_IsCrouching = !string.IsNullOrEmpty(paramIsCrouching) ? Animator.StringToHash(paramIsCrouching) : 0;
            h_IsProne = !string.IsNullOrEmpty(paramIsProne) ? Animator.StringToHash(paramIsProne) : 0;
            h_IsCrawling = !string.IsNullOrEmpty(paramIsCrawling) ? Animator.StringToHash(paramIsCrawling) : 0;
            h_ProneBlend = !string.IsNullOrEmpty(paramProneBlend) ? Animator.StringToHash(paramProneBlend) : 0;
            h_LandTrigger = !string.IsNullOrEmpty(paramLandTrigger) ? Animator.StringToHash(paramLandTrigger) : 0;
        }

        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            if (animator == null) return;

            // Use the explicit flags from PlayerAnimationState
            if (h_IsCrouching != 0 && HasParameter(h_IsCrouching)) 
                animator.SetBool(h_IsCrouching, state.IsCrouching);
            if (h_IsProne != 0 && HasParameter(h_IsProne)) 
                animator.SetBool(h_IsProne, state.IsProne);
            
            // Derive crawling from IsProne flag and movement speed if not explicit
            bool isCrawling = state.IsProne ? (state.MoveSpeed > 0.25f) : (state.IsCrouching && state.MoveSpeed > 0.25f && state.MoveSpeed < 1.5f);
            if (h_IsCrawling != 0 && HasParameter(h_IsCrawling)) 
                animator.SetBool(h_IsCrawling, isCrawling);
            
            float blend = state.IsProne ? 1f : (isCrawling ? 0.5f : 0f);
            if (h_ProneBlend != 0 && HasParameter(h_ProneBlend)) 
                animator.SetFloat(h_ProneBlend, blend);
            
            // Debug log when prone state changes
            // if (state.IsProne)
            // {
            //     Debug.Log($"[ProneAnimatorBridge] Setting animator: IsCrouching={state.IsCrouching}, IsProne={state.IsProne}, IsCrawling={isCrawling}, Blend={blend}");
            // }
        }

        public void TriggerLanding()
        {
            if (animator == null) return;
            if (h_LandTrigger != 0 && HasParameter(h_LandTrigger)) 
                animator.SetTrigger(h_LandTrigger);
        }

        private bool HasParameter(int hash)
        {
            if (animator == null || hash == 0) return false;
            
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.nameHash == hash)
                    return true;
            }
            return false;
        }

        // Optional methods for other systems to set explicit prone params on the bridge
        public void SetProneState(bool isProne, bool isCrawling, float transitionProgress)
        {
            if (animator == null) return;
            animator.SetBool(h_IsProne, isProne);
            animator.SetBool(h_IsCrawling, isCrawling);
            animator.SetFloat(h_ProneBlend, transitionProgress);
        }
    }
}
