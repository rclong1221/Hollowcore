using Unity.Entities;
using UnityEngine;
using DIG.Player.IK;

namespace DIG.Player.View
{
    /// <summary>
    /// IK Target Interpolation functionality for PlayerIKBridge.
    /// Handles smooth transitions for ability-driven IK (climbing, vaulting, etc).
    /// </summary>
    public partial class PlayerIKBridge
    {
        /// <summary>
        /// Updates IK target interpolations from the IKTargetOverride buffer.
        /// </summary>
        private void UpdateIKTargetInterpolations()
        {
            if (!_entityManager.HasBuffer<IKTargetOverride>(_entity)) return;
            
            var buffer = _entityManager.GetBuffer<IKTargetOverride>(_entity);
            
            // Process each override in the buffer
            for (int b = 0; b < buffer.Length; b++)
            {
                var overrideData = buffer[b];
                int goalIndex = (int)overrideData.Goal;
                
                if (goalIndex < 0 || goalIndex >= IKGoalCount) continue;
                
                if (overrideData.Active)
                {
                    // Start or update interpolation
                    if (_ikInterpolationStart[goalIndex] < 0)
                    {
                        // Starting new interpolation - capture current pose
                        _ikInterpolationStart[goalIndex] = Time.time;
                        _ikInterpolationDuration[goalIndex] = overrideData.Duration;
                    }
                    
                    // Calculate interpolation progress
                    float elapsed = Time.time - _ikInterpolationStart[goalIndex];
                    float duration = _ikInterpolationDuration[goalIndex];
                    float t = duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1f;
                    
                    // Get current IK position/rotation
                    Vector3 currentPos = GetCurrentIKPosition(overrideData.Goal);
                    Quaternion currentRot = GetCurrentIKRotation(overrideData.Goal);
                    
                    // Interpolate to target
                    _ikInterpolationPosition[goalIndex] = Vector3.Lerp(currentPos, overrideData.Position, t);
                    _ikInterpolationRotation[goalIndex] = Quaternion.Slerp(currentRot, overrideData.Rotation, t);
                    _ikTargetActive[goalIndex] = true;
                }
                else if (_ikTargetActive[goalIndex])
                {
                    // Target was active but now inactive - interpolate back to animation
                    if (_ikInterpolationStart[goalIndex] >= 0)
                    {
                        float elapsed = Time.time - _ikInterpolationStart[goalIndex];
                        float duration = _ikInterpolationDuration[goalIndex];
                        float t = duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1f;
                        
                        if (t >= 1f)
                        {
                            // Finished interpolating back
                            _ikTargetActive[goalIndex] = false;
                            _ikInterpolationStart[goalIndex] = -1f;
                        }
                        else
                        {
                            // Still interpolating back to animation pose
                            Vector3 animPos = GetCurrentIKPosition(overrideData.Goal);
                            Quaternion animRot = GetCurrentIKRotation(overrideData.Goal);
                            
                            _ikInterpolationPosition[goalIndex] = Vector3.Lerp(_ikInterpolationPosition[goalIndex], animPos, t);
                            _ikInterpolationRotation[goalIndex] = Quaternion.Slerp(_ikInterpolationRotation[goalIndex], animRot, t);
                        }
                    }
                    else
                    {
                        _ikTargetActive[goalIndex] = false;
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets the current IK position for a goal.
        /// </summary>
        private Vector3 GetCurrentIKPosition(IKGoal goal)
        {
            switch (goal)
            {
                case IKGoal.LeftHand: return _animator.GetIKPosition(AvatarIKGoal.LeftHand);
                case IKGoal.RightHand: return _animator.GetIKPosition(AvatarIKGoal.RightHand);
                case IKGoal.LeftFoot: return _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
                case IKGoal.RightFoot: return _animator.GetIKPosition(AvatarIKGoal.RightFoot);
                case IKGoal.LeftElbow: return _animator.GetIKHintPosition(AvatarIKHint.LeftElbow);
                case IKGoal.RightElbow: return _animator.GetIKHintPosition(AvatarIKHint.RightElbow);
                case IKGoal.LeftKnee: return _animator.GetIKHintPosition(AvatarIKHint.LeftKnee);
                case IKGoal.RightKnee: return _animator.GetIKHintPosition(AvatarIKHint.RightKnee);
                default: return Vector3.zero;
            }
        }
        
        /// <summary>
        /// Gets the current IK rotation for a goal.
        /// </summary>
        private Quaternion GetCurrentIKRotation(IKGoal goal)
        {
            switch (goal)
            {
                case IKGoal.LeftHand: return _animator.GetIKRotation(AvatarIKGoal.LeftHand);
                case IKGoal.RightHand: return _animator.GetIKRotation(AvatarIKGoal.RightHand);
                case IKGoal.LeftFoot: return _animator.GetIKRotation(AvatarIKGoal.LeftFoot);
                case IKGoal.RightFoot: return _animator.GetIKRotation(AvatarIKGoal.RightFoot);
                default: return Quaternion.identity; // Hints don't have rotation
            }
        }
        
        /// <summary>
        /// Applies any active IK target interpolations.
        /// </summary>
        private void ApplyIKTargetInterpolations()
        {
            for (int i = 0; i < IKGoalCount; i++)
            {
                // Skip if not active or not interpolating
                if (!_ikTargetActive[i] || _ikInterpolationStart[i] < 0) continue;
                
                IKGoal goal = (IKGoal)i;
                float weight = 1f; // TODO: Could interpolate weight too
                
                switch (goal)
                {
                    case IKGoal.LeftHand:
                        _animator.SetIKPosition(AvatarIKGoal.LeftHand, _ikInterpolationPosition[i]);
                        _animator.SetIKRotation(AvatarIKGoal.LeftHand, _ikInterpolationRotation[i]);
                        _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
                        _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
                        break;
                    case IKGoal.RightHand:
                        _animator.SetIKPosition(AvatarIKGoal.RightHand, _ikInterpolationPosition[i]);
                        _animator.SetIKRotation(AvatarIKGoal.RightHand, _ikInterpolationRotation[i]);
                        _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
                        _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
                        break;
                    case IKGoal.LeftFoot:
                        _animator.SetIKPosition(AvatarIKGoal.LeftFoot, _ikInterpolationPosition[i]);
                        _animator.SetIKRotation(AvatarIKGoal.LeftFoot, _ikInterpolationRotation[i]);
                        _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, weight);
                        _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, weight);
                        break;
                    case IKGoal.RightFoot:
                        _animator.SetIKPosition(AvatarIKGoal.RightFoot, _ikInterpolationPosition[i]);
                        _animator.SetIKRotation(AvatarIKGoal.RightFoot, _ikInterpolationRotation[i]);
                        _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, weight);
                        _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, weight);
                        break;
                    case IKGoal.LeftElbow:
                        _animator.SetIKHintPosition(AvatarIKHint.LeftElbow, _ikInterpolationPosition[i]);
                        _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, weight);
                        break;
                    case IKGoal.RightElbow:
                        _animator.SetIKHintPosition(AvatarIKHint.RightElbow, _ikInterpolationPosition[i]);
                        _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);
                        break;
                    case IKGoal.LeftKnee:
                        _animator.SetIKHintPosition(AvatarIKHint.LeftKnee, _ikInterpolationPosition[i]);
                        _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, weight);
                        break;
                    case IKGoal.RightKnee:
                        _animator.SetIKHintPosition(AvatarIKHint.RightKnee, _ikInterpolationPosition[i]);
                        _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, weight);
                        break;
                }
            }
        }
    }
}
