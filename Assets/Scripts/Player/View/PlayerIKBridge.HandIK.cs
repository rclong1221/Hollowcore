using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Player.IK;

namespace DIG.Player.View
{
    /// <summary>
    /// Hand and Upper Arm IK functionality for PlayerIKBridge.
    /// Handles weapon aiming, hand positioning, and recoil springs.
    /// Based on Opsive CharacterIK techniques.
    /// </summary>
    public partial class PlayerIKBridge
    {
        /// <summary>
        /// Updates spring physics for hand recoil.
        /// </summary>
        private void UpdateSprings(float stiffness, float damping)
        {
            for (int i = 0; i < 2; i++)
            {
                // Position spring
                Vector3 force = -stiffness * _handPositionSpringValue[i] - damping * _handPositionSpringVelocity[i];
                _handPositionSpringVelocity[i] += force * Time.deltaTime;
                _handPositionSpringValue[i] += _handPositionSpringVelocity[i] * Time.deltaTime;
                
                // Rotation spring
                force = -stiffness * _handRotationSpringValue[i] - damping * _handRotationSpringVelocity[i];
                _handRotationSpringVelocity[i] += force * Time.deltaTime;
                _handRotationSpringValue[i] += _handRotationSpringVelocity[i] * Time.deltaTime;
            }
        }
        
        /// <summary>
        /// Gets the hand that is further from the camera (for consistent aim direction).
        /// </summary>
        private Transform GetDistantHand()
        {
            if (transform.InverseTransformPoint(_rightHand.position).z < transform.InverseTransformPoint(_leftHand.position).z)
            {
                return _leftHand;
            }
            return _rightHand;
        }
        
        /// <summary>
        /// Calculates target rotation for a hand to face the look direction.
        /// Based on Opsive CharacterIK.GetTargetHandRotation.
        /// </summary>
        private Quaternion GetTargetHandRotation(bool leftHand, AvatarIKGoal ikGoal, Vector3 lookDirection, Vector3 lookAtOffset)
        {
            int handIndex = leftHand ? 0 : 1;
            if (_handRotationIKWeight[handIndex] == 0)
            {
                return _animator.GetIKRotation(ikGoal);
            }
            
            // Use the distant hand so hands always point in the same direction
            Transform distantHand = GetDistantHand();
            Vector3 aimDir = (lookDirection + transform.TransformDirection(lookAtOffset)).normalized;
            
            return Quaternion.LookRotation(aimDir, Vector3.up) 
                   * Quaternion.Inverse(transform.rotation)
                   * Quaternion.Euler(transform.TransformDirection(leftHand ? _handRotationSpringValue[0] : _handRotationSpringValue[1]))
                   * _animator.GetIKRotation(ikGoal);
        }
        
        /// <summary>
        /// Calculates target position for a hand.
        /// Based on Opsive CharacterIK.GetTargetHandPosition.
        /// </summary>
        private Vector3 GetTargetHandPosition(Transform hand, bool leftHand, Vector3 handPositionOffset)
        {
            Vector3 handPosition;
            
            // If upper arm IK is active, use the calculated positions
            if (_upperArmWeight > 0)
            {
                handPosition = (hand == _dominantHand) ? _dominantHandPosition : _nonDominantHandPosition;
            }
            else
            {
                // Use original hand position, or offset from dominant hand
                handPosition = ((hand == _dominantHand || _dominantHand == null) ? hand.position : _dominantHand.TransformPoint(_handOffset));
            }
            
            int handIndex = leftHand ? 0 : 1;
            return handPosition + transform.TransformDirection(_handPositionSpringValue[handIndex]) + transform.TransformDirection(handPositionOffset);
        }
        
        /// <summary>
        /// Rotates upper arms toward the look target.
        /// Based on Opsive CharacterIK.RotateUpperArms.
        /// </summary>
        private void RotateUpperArms(HandIKSettings settings, Vector3 lookDirection)
        {
            float targetWeight = 0f;
            
            if (_dominantUpperArm != null && settings.UpperArmWeight > 0)
            {
                targetWeight = settings.UpperArmWeight;
            }
            
            // Smooth weight transition
            _upperArmWeight = Mathf.MoveTowards(_upperArmWeight, targetWeight, settings.UpperArmAdjustmentSpeed * Time.deltaTime);
            
            if (_upperArmWeight <= 0.01f || _dominantUpperArm == null) return;
            
            // Get look direction in local space
            Vector3 localLookDir = transform.InverseTransformDirection(lookDirection);
            Vector3 lookDir = transform.InverseTransformDirection(transform.forward);
            lookDir.y = localLookDir.y;
            lookDir = transform.TransformDirection(lookDir).normalized;
            
            // Prevent arm from rotating too far behind
            if (localLookDir.y < 0)
            {
                lookDir = Vector3.Lerp(transform.forward, lookDir, 1 - Mathf.Abs(localLookDir.y));
            }
            
            // Calculate target rotation
            Quaternion targetRotation = Quaternion.FromToRotation(transform.forward, lookDir) * _dominantUpperArm.rotation;
            targetRotation = Quaternion.Slerp(_dominantUpperArm.rotation, targetRotation, _upperArmWeight);
            
            // Calculate hand positions based on upper arm rotation
            Vector3 offset = Vector3.Scale(_dominantUpperArm.InverseTransformPoint(_dominantHand.position), _dominantHand.lossyScale);
            _dominantHandPosition = TransformPoint(_dominantUpperArm.position, targetRotation, offset);
            
            // Non-dominant hand follows dominant hand rotation
            AvatarIKGoal dominantGoal = _isRightHandDominant ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            _nonDominantHandPosition = TransformPoint(_dominantHandPosition, _animator.GetIKRotation(dominantGoal), _nonDominantHandOffset);
        }
        
        /// <summary>
        /// Transforms a point by position and rotation.
        /// </summary>
        private static Vector3 TransformPoint(Vector3 position, Quaternion rotation, Vector3 localPoint)
        {
            return position + rotation * localPoint;
        }
        
        /// <summary>
        /// Inverse transforms a point by position and rotation.
        /// </summary>
        private static Vector3 InverseTransformPoint(Vector3 position, Quaternion rotation, Vector3 worldPoint)
        {
            return Quaternion.Inverse(rotation) * (worldPoint - position);
        }

        /// <summary>
        /// Positions the upper body - hand IK for weapons and aiming.
        /// Called on upper body layer.
        /// </summary>
        private void PositionUpperBody(bool isClimbing)
        {
            if (isClimbing || !_entityManager.HasComponent<HandIKSettings>(_entity) || !_entityManager.HasComponent<HandIKState>(_entity)) return;
            
            var handSettings = _entityManager.GetComponentData<HandIKSettings>(_entity);
            var handState = _entityManager.GetComponentData<HandIKState>(_entity);
            
            // Update springs for recoil
            UpdateSprings(handSettings.SpringStiffness, handSettings.SpringDamping);
            
            // Get look direction for aiming
            Vector3 lookDirection = transform.forward; // Default
            if (_entityManager.HasComponent<AimDirection>(_entity))
            {
                var aimDir = _entityManager.GetComponentData<AimDirection>(_entity);
                if (math.lengthsq(aimDir.AimPoint) > 0.01f)
                {
                    Vector3 headPos = _head != null ? _head.position : transform.position + Vector3.up * 1.5f;
                    lookDirection = ((Vector3)aimDir.AimPoint - headPos).normalized;
                }
            }
            
            // Check if we should apply hand IK (aiming or using item)
            bool shouldApplyHandIK = handState.IsAiming || handState.IsUsingItem || handState.HasLeftTarget || handState.HasRightTarget;
            
            // Rotate upper arms first (affects hand positions)
            if (shouldApplyHandIK)
            {
                RotateUpperArms(handSettings, lookDirection);
            }
            
            // Store non-dominant hand offset from dominant hand
            AvatarIKGoal dominantGoal = _isRightHandDominant ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            AvatarIKGoal nonDominantGoal = _isRightHandDominant ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            _nonDominantHandOffset = InverseTransformPoint(
                _animator.GetIKPosition(dominantGoal), 
                _animator.GetIKRotation(dominantGoal), 
                _animator.GetIKPosition(nonDominantGoal));
            
            // --- Hand Rotation ---
            for (int i = 0; i < 2; i++)
            {
                bool leftHand = (i == 0);
                AvatarIKGoal ikGoal = leftHand ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
                
                // Determine target weight
                float targetWeight = 0f;
                bool hasTarget = leftHand ? handState.HasLeftTarget : handState.HasRightTarget;
                if (handSettings.HandWeight > 0 && (hasTarget || handState.IsAiming || handState.IsUsingItem))
                {
                    targetWeight = handSettings.HandWeight;
                }
                
                // Smooth weight transition
                _handRotationIKWeight[i] = Mathf.MoveTowards(_handRotationIKWeight[i], targetWeight, handSettings.HandAdjustmentSpeed * Time.deltaTime);
                
                // Calculate target rotation
                Quaternion targetRotation;
                if (hasTarget)
                {
                    targetRotation = leftHand ? (Quaternion)handState.LeftHandRotation : (Quaternion)handState.RightHandRotation;
                }
                else
                {
                    targetRotation = GetTargetHandRotation(leftHand, ikGoal, lookDirection, handSettings.HandPositionOffset);
                }
                
                // Apply rotation IK
                _animator.SetIKRotation(ikGoal, targetRotation);
                _animator.SetIKRotationWeight(ikGoal, _handRotationIKWeight[i]);
            }
            
            // --- Hand Position ---
            for (int i = 0; i < 2; i++)
            {
                bool leftHand = (i == 0);
                AvatarIKGoal ikGoal = leftHand ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
                Transform hand = leftHand ? _leftHand : _rightHand;
                
                // Determine target weight
                float targetWeight = 0f;
                bool hasTarget = leftHand ? handState.HasLeftTarget : handState.HasRightTarget;
                if (hasTarget || handState.IsAiming || handState.IsUsingItem || _upperArmWeight > 0)
                {
                    targetWeight = handSettings.HandWeight;
                }
                
                // Smooth weight transition
                _handPositionIKWeight[i] = Mathf.MoveTowards(_handPositionIKWeight[i], targetWeight, handSettings.HandAdjustmentSpeed * Time.deltaTime);
                
                // Calculate target position
                Vector3 targetPosition;
                if (hasTarget)
                {
                    targetPosition = leftHand ? (Vector3)handState.LeftHandTarget : (Vector3)handState.RightHandTarget;
                }
                else if (_handPositionIKWeight[i] > 0)
                {
                    targetPosition = GetTargetHandPosition(hand, leftHand, handSettings.HandPositionOffset);
                }
                else
                {
                    targetPosition = _animator.GetIKPosition(ikGoal);
                }
                
                // Apply position IK
                _animator.SetIKPosition(ikGoal, targetPosition);
                _animator.SetIKPositionWeight(ikGoal, _handPositionIKWeight[i]);
            }
            
            // Debug logging
            if (Time.frameCount % 300 == 0 && shouldApplyHandIK)
            {
                UnityEngine.Debug.Log($"[HandIK] Weights: LPos={_handPositionIKWeight[0]:F2} RPos={_handPositionIKWeight[1]:F2} UpperArm={_upperArmWeight:F2} Aiming={handState.IsAiming}");
            }
            
            // Apply IK Target Interpolations (for ability-driven targets)
            ApplyIKTargetInterpolations();
        }

        /// <summary>
        /// Second pass for positioning the non-dominant hand relative to the dominant hand.
        /// Used for two-handed weapons. Called on a higher layer pass.
        /// </summary>
        private void PositionHandsSecondPass()
        {
            if (!_entityManager.HasComponent<HandIKState>(_entity)) return;
            
            var handState = _entityManager.GetComponentData<HandIKState>(_entity);
            
            // Only needed for two-handed weapons
            if (!handState.IsAiming && !handState.IsUsingItem) return;
            
            // Get dominant hand position (updated from first pass)
            AvatarIKGoal dominantGoal = _isRightHandDominant ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            AvatarIKGoal nonDominantGoal = _isRightHandDominant ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            
            Vector3 dominantPos = _animator.GetIKPosition(dominantGoal);
            Quaternion dominantRot = _animator.GetIKRotation(dominantGoal);
            
            // Transform the offset back to world space
            Vector3 nonDominantPos = TransformPoint(dominantPos, dominantRot, _nonDominantHandOffset);
            
            // Apply to non-dominant hand
            _animator.SetIKPosition(nonDominantGoal, nonDominantPos);
            _animator.SetIKPositionWeight(nonDominantGoal, _handPositionIKWeight[_isRightHandDominant ? 0 : 1]);
        }
    }
}
