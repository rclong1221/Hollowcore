using UnityEngine;
using System.Collections.Generic;
using Player.Animation;

namespace Player.Bridges
{
    /// <summary>
    /// Simplified climbing IK implementation.
    /// 
    /// JITTER FIX: Completely disabled per-frame raycasts which were hitting different points
    /// on rough geometry each frame. Now uses animation-driven positions with optional
    /// simple offset adjustment toward the wall.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class OpsiveClimbingIK : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        
        [Header("IK Mode")]
        [Tooltip("When enabled, applies simple offset-based IK. When disabled, lets animation fully drive limbs.")]
        [SerializeField] private bool enableIK = true;
        
        [Tooltip("Use simple fixed offsets instead of raycasting (eliminates jitter)")]
        [SerializeField] private bool useSimpleOffsets = true;
        
        [Header("Simple Offsets (No Raycasting)")]
        [Tooltip("How far hands reach toward the wall from animation position")]
        [SerializeField] private float handReachDistance = 0.15f;
        
        [Tooltip("How far feet reach toward the wall from animation position")]
        [SerializeField] private float footReachDistance = 0.1f;
        
        [Header("IK Weights")]
        [Range(0f, 1f)]
        [SerializeField] private float handWeight = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] private float footWeight = 0.3f;
        
        [Header("Smoothing")]
        [Tooltip("How fast IK blends in/out")]
        [Range(1f, 20f)]
        [SerializeField] private float blendSpeed = 8f;
        
        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;
        
        // State
        private bool _isClimbing;
        private bool _isHanging;
        private float _currentWeight;
        private Vector3 _wallNormal;
        private Vector3 _gripPosition;
        
        // Smoothed state (to prevent jitter from network prediction)
        private Vector3 _smoothedWallNormal;
        private bool _initialized;
        
        void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
                
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }
        
        /// <summary>
        /// Call each frame to update climbing state and surface info.
        /// </summary>
        public void SetClimbingState(bool isClimbing, bool isHanging, Vector3 wallNormal, Vector3 gripPosition)
        {
            bool stateChanged = isClimbing != _isClimbing;
            
            _isClimbing = isClimbing;
            _isHanging = isHanging;
            _wallNormal = wallNormal.normalized;
            _gripPosition = gripPosition;
            
            // Initialize on first call or state change
            if (!_initialized || stateChanged)
            {
                _smoothedWallNormal = _wallNormal;
                _initialized = true;
            }
        }
        
        void LateUpdate()
        {
            // Blend weight smoothly
            float targetWeight = (_isClimbing && enableIK) ? 1f : 0f;
            _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, Time.deltaTime * blendSpeed);
            
            // Smooth the wall normal to prevent jitter from network prediction
            if (_isClimbing)
            {
                _smoothedWallNormal = Vector3.Slerp(_smoothedWallNormal, _wallNormal, Time.deltaTime * blendSpeed);
            }
        }
        
        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;
            if (_currentWeight <= 0.01f) return;
            if (layerIndex != 0) return; // Only process on base layer
            
            // Get the direction toward the wall
            Vector3 toWall = -_smoothedWallNormal;
            
            // Apply simple IK - just push limbs slightly toward the wall
            ApplySimpleLimbIK(AvatarIKGoal.LeftHand, toWall, handReachDistance, handWeight);
            ApplySimpleLimbIK(AvatarIKGoal.RightHand, toWall, handReachDistance, handWeight);
            
            if (!_isHanging)
            {
                ApplySimpleLimbIK(AvatarIKGoal.LeftFoot, toWall, footReachDistance, footWeight);
                ApplySimpleLimbIK(AvatarIKGoal.RightFoot, toWall, footReachDistance, footWeight);
            }
        }
        
        /// <summary>
        /// Simple IK: Just offset the animation position toward the wall.
        /// No raycasting = no jitter from geometry variance.
        /// </summary>
        private void ApplySimpleLimbIK(AvatarIKGoal ikGoal, Vector3 toWallDirection, float reachDistance, float limbWeight)
        {
            float weight = _currentWeight * limbWeight;
            if (weight <= 0.01f) return;
            
            // Get current animation-driven position
            Vector3 animPos = animator.GetIKPosition(ikGoal);
            Quaternion animRot = animator.GetIKRotation(ikGoal);
            
            if (useSimpleOffsets)
            {
                // Simple approach: Just push the limb toward the wall
                Vector3 targetPos = animPos + toWallDirection * reachDistance;
                
                // Rotation depends on limb type
                Quaternion targetRot;
                bool isHand = (ikGoal == AvatarIKGoal.LeftHand || ikGoal == AvatarIKGoal.RightHand);
                
                if (isHand)
                {
                    // Hands should grip with fingers pointing UP, palms facing the wall
                    // Start facing the wall, then rotate to get fingers up and palms out
                    targetRot = Quaternion.LookRotation(-toWallDirection, Vector3.up);
                    // Rotate -90 on X to point fingers up, then 180 on Z to flip palms outward
                    targetRot = targetRot * Quaternion.Euler(-90f, 0f, 180f);
                }
                else
                {
                    // Feet press against wall normally
                    targetRot = Quaternion.LookRotation(-toWallDirection, Vector3.up);
                }
                
                animator.SetIKPosition(ikGoal, targetPos);
                animator.SetIKRotation(ikGoal, targetRot);
                animator.SetIKPositionWeight(ikGoal, weight);
                animator.SetIKRotationWeight(ikGoal, weight);
                
                if (debugDraw)
                {
                    Debug.DrawLine(animPos, targetPos, Color.cyan);
                }
            }
            else
            {
                // Disable IK - let animation fully control
                animator.SetIKPositionWeight(ikGoal, 0f);
                animator.SetIKRotationWeight(ikGoal, 0f);
            }
        }
    }
}
