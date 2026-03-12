using Unity.Entities;
using UnityEngine;
using DIG.Player.IK;
using Player.Components;

namespace DIG.Player.View
{
    /// <summary>
    /// Foot IK functionality for PlayerIKBridge.
    /// Handles ground detection and foot placement on uneven terrain.
    /// </summary>
    public partial class PlayerIKBridge
    {
        /// <summary>
        /// Calibrates foot IK from the T-pose/bind pose.
        /// Must be called after animator is in a neutral pose.
        /// </summary>
        private void CalibrateFootIK()
        {
            if (_leftFoot == null || _rightFoot == null || _leftLowerLeg == null || _rightLowerLeg == null || _hips == null)
            {
                UnityEngine.Debug.LogWarning("[FootIK] Cannot calibrate - missing bone transforms");
                return;
            }
            
            Transform root = transform;
            
            // Calibrate left foot
            _footOffset[0] = root.InverseTransformPoint(_leftFoot.position).y - FootOffsetAdjustment;
            _maxLegLength[0] = root.InverseTransformPoint(_leftLowerLeg.position).y - FootOffsetAdjustment;
            
            // Calibrate right foot  
            _footOffset[1] = root.InverseTransformPoint(_rightFoot.position).y - FootOffsetAdjustment;
            _maxLegLength[1] = root.InverseTransformPoint(_rightLowerLeg.position).y - FootOffsetAdjustment;
            
            // Store hips position for body lowering
            _hipsLocalY = root.InverseTransformPoint(_hips.position).y;
            
            _footIKCalibrated = true;
            UnityEngine.Debug.Log($"[FootIK] Calibrated: FootOffset=[{_footOffset[0]:F3}, {_footOffset[1]:F3}] MaxLegLength=[{_maxLegLength[0]:F3}, {_maxLegLength[1]:F3}] HipsY={_hipsLocalY:F3}");
        }
        
        /// <summary>
        /// Gets the position to start the foot raycast from (at lower leg height).
        /// </summary>
        private Vector3 GetFootRaycastPosition(Transform foot, Transform lowerLeg, out float distance)
        {
            Transform root = transform;
            
            // Get positions in local space
            Vector3 localFootPos = root.InverseTransformPoint(foot.position);
            Vector3 localLowerLegPos = root.InverseTransformPoint(lowerLeg.position);
            
            // Distance from lower leg to foot
            distance = localLowerLegPos.y - localFootPos.y;
            
            // Raycast from foot XZ but at lower leg height
            Vector3 raycastLocalPos = localFootPos;
            raycastLocalPos.y = localLowerLegPos.y;
            
            return root.TransformPoint(raycastLocalPos);
        }

        /// <summary>
        /// Positions the lower body - foot IK for ground placement.
        /// Called on base layer.
        /// </summary>
        private void PositionLowerBody(bool isClimbing)
        {
            if (isClimbing || !_entityManager.HasComponent<FootIKSettings>(_entity)) return;
            
            // Calibrate on first frame
            if (!_footIKCalibrated)
            {
                CalibrateFootIK();
            }
            
            var settings = _entityManager.GetComponentData<FootIKSettings>(_entity);
            
            // Get grounded state
            bool isGrounded = true;
            if (_entityManager.HasComponent<PlayerState>(_entity))
            {
                var playerState = _entityManager.GetComponentData<PlayerState>(_entity);
                isGrounded = playerState.IsGrounded;
            }
            
            // Layer mask - exclude player and other non-ground layers
            LayerMask groundMask = ~(1 << 3 | 1 << 2); // Exclude player (3) and IgnoreRaycast (2)
            
            Transform root = transform;
            float hipsOffset = 0f; // How much to lower hips this frame
            
            // Ground detection data
            float[] groundDistance = { float.MaxValue, float.MaxValue };
            Vector3[] groundPoint = new Vector3[2];
            Vector3[] groundNormal = new Vector3[2];
            float[] raycastDistance = new float[2];
            
            if (isGrounded && _footIKCalibrated)
            {
                // Pass 1: Raycast to find ground for each foot
                Transform[] feet = { _leftFoot, _rightFoot };
                Transform[] lowerLegs = { _leftLowerLeg, _rightLowerLeg };
                
                for (int i = 0; i < 2; i++)
                {
                    // Raycast from lower leg height down to find ground
                    Vector3 rayOrigin = GetFootRaycastPosition(feet[i], lowerLegs[i], out float distance);
                    float rayLength = distance + _footOffset[i] + _maxLegLength[i];
                    
                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
                    {
                        // Only accept hits below the character's radius-ish
                        if (root.InverseTransformPoint(hit.point).y < 0.3f)
                        {
                            raycastDistance[i] = distance;
                            groundDistance[i] = hit.distance;
                            groundPoint[i] = hit.point;
                            groundNormal[i] = hit.normal;
                            
                            // Calculate hip offset needed to reach this ground point
                            float footLocalY = root.InverseTransformPoint(feet[i].position).y;
                            float offset = groundDistance[i] - raycastDistance[i] - footLocalY;
                            if (offset > hipsOffset)
                            {
                                hipsOffset = offset;
                            }
                        }
                    }
                }
            }
            
            // Smoothly interpolate hips offset
            _hipsOffset = Mathf.Lerp(_hipsOffset, hipsOffset, HipsAdjustmentSpeed * Time.deltaTime);
            
            // Clamp hips offset to reasonable range
            _hipsOffset = Mathf.Clamp(_hipsOffset, 0f, settings.BodyHeightAdjustment);
            
            // Apply hips offset (lower the body)
            if (_hipsOffset > 0.001f)
            {
                Vector3 bodyPos = _animator.bodyPosition;
                bodyPos.y -= _hipsOffset;
                _animator.bodyPosition = bodyPos;
            }
            
            // Pass 2: Position feet
            Transform[] feetPass2 = { _leftFoot, _rightFoot };
            AvatarIKGoal[] ikGoals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
            
            for (int i = 0; i < 2; i++)
            {
                Vector3 position = _animator.GetIKPosition(ikGoals[i]);
                Quaternion rotation = _animator.GetIKRotation(ikGoals[i]);
                float targetWeight = 0f;
                float adjustmentSpeed = FootWeightInactiveSpeed;
                
                if (isGrounded && groundDistance[i] != float.MaxValue && groundDistance[i] > 0)
                {
                    // Only apply IK if foot would be below the ground
                    float footAboveGround = root.InverseTransformDirection(position - groundPoint[i]).y - _footOffset[i] - _hipsOffset;
                    
                    if (footAboveGround < 0)
                    {
                        // Foot would clip into ground - apply IK to lift it
                        Vector3 localFootPos = root.InverseTransformPoint(position);
                        localFootPos.y = root.InverseTransformPoint(groundPoint[i]).y;
                        position = root.TransformPoint(localFootPos) + Vector3.up * (_footOffset[i] + _hipsOffset);
                        
                        // Align rotation to ground normal
                        rotation = Quaternion.LookRotation(
                            Vector3.Cross(groundNormal[i], rotation * -Vector3.right), 
                            Vector3.up);
                        
                        targetWeight = settings.FootIKWeight;
                        adjustmentSpeed = FootWeightActiveSpeed;
                    }
                }
                
                // Smooth weight transition
                _footIKWeight[i] = Mathf.MoveTowards(_footIKWeight[i], targetWeight, adjustmentSpeed * Time.deltaTime);
                
                // Apply IK
                _animator.SetIKPosition(ikGoals[i], position);
                _animator.SetIKPositionWeight(ikGoals[i], _footIKWeight[i]);
                _animator.SetIKRotation(ikGoals[i], rotation);
                _animator.SetIKRotationWeight(ikGoals[i], _footIKWeight[i] * 0.5f); // Less rotation weight
            }
            
            // --- Knee Hints ---
            // Set knee hint positions for better leg bending on stairs/slopes
            // Use lower leg positions projected forward to prevent weird knee bending
            if (_footIKWeight[0] > 0.01f || _footIKWeight[1] > 0.01f)
            {
                // Left knee - hint should be forward of lower leg
                Vector3 leftKneeHint = _leftLowerLeg.position + transform.forward * 0.3f;
                _animator.SetIKHintPosition(AvatarIKHint.LeftKnee, leftKneeHint);
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, _footIKWeight[0] * 0.5f);
                
                // Right knee
                Vector3 rightKneeHint = _rightLowerLeg.position + transform.forward * 0.3f;
                _animator.SetIKHintPosition(AvatarIKHint.RightKnee, rightKneeHint);
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, _footIKWeight[1] * 0.5f);
            }
            
            // Debug logging
            if (Time.frameCount % 300 == 0)
            {
                UnityEngine.Debug.Log($"[FootIK] Weights=[{_footIKWeight[0]:F2}, {_footIKWeight[1]:F2}] HipsOffset={_hipsOffset:F3} Grounded={isGrounded}");
            }
        }
    }
}
