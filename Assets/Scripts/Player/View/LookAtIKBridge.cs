using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Player.IK;
using DIG.Player.Abilities; // For RootMotionDelta
using Player.Components; // For FreeClimbState

namespace DIG.Player.View
{
    /// <summary>
    /// Bridges LookAt IK from ECS (LookAtIKState) to Animator.SetLookAtPosition.
    /// Also handles Foot IK for proper ground placement.
    /// Uses techniques from Opsive CharacterIK: raycast from lower leg, T-pose calibration,
    /// only apply IK when foot would be below ground.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class LookAtIKBridge : MonoBehaviour
    {
        private Animator _animator;
        private Entity _entity;
        private EntityManager _entityManager;
        private bool _isLinked = false;
        
        // Foot IK state (calibrated on first frame, smoothed over time)
        private bool _footIKCalibrated = false;
        private float[] _footOffset = new float[2]; // T-pose foot height above ground
        private float[] _maxLegLength = new float[2]; // Distance from lower leg to foot
        private float[] _footIKWeight = new float[2]; // Smoothed weights
        private float _hipsOffset = 0f; // How much to lower the hips
        private float _hipsLocalY; // T-pose hips local Y position
        
        // Foot IK settings (could come from component, using sensible defaults)
        private const float FootOffsetAdjustment = 0.005f; // Small adjustment like Opsive
        private const float FootWeightActiveSpeed = 10f;
        private const float FootWeightInactiveSpeed = 2f;
        private const float HipsAdjustmentSpeed = 4f;
        
        // Cached bone transforms
        private Transform _leftFoot, _rightFoot, _leftLowerLeg, _rightLowerLeg, _hips;

        void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void LinkState(Entity entity, EntityManager em)
        {
            _entity = entity;
            _entityManager = em;
            _isLinked = true;
            
            // Cache bone transforms for foot IK
            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftLowerLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            
            // Mark for T-pose calibration on first frame
            _footIKCalibrated = false;
            
            UnityEngine.Debug.Log($"[LookAtIK] Bridge linked to entity {entity.Index}:{entity.Version}");
        }
        
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

        void OnAnimatorIK(int layerIndex)
        {
            if (!_isLinked || !_entityManager.Exists(_entity))
            {
                if (Time.frameCount % 120 == 0)
                {
                    UnityEngine.Debug.LogWarning($"[LookAtIK] OnAnimatorIK called but not linked! isLinked={_isLinked}");
                }
                return;
            }
            if (layerIndex != 0) return; // Base layer only usually

            // Climbing check (for future use - climbing IK handled separately)
            bool isClimbing = false;
            if (_entityManager.HasComponent<FreeClimbState>(_entity))
            {
                var climbState = _entityManager.GetComponentData<FreeClimbState>(_entity);
                isClimbing = climbState.IsClimbing;
            }

            // --- Foot IK ---
            // Makes feet plant correctly on slopes and uneven terrain
            // Uses Opsive's approach: raycast from lower leg height, only apply IK when foot would be below ground
            if (!isClimbing && _entityManager.HasComponent<FootIKSettings>(_entity))
            {
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
                
                // Debug logging
                if (Time.frameCount % 300 == 0)
                {
                    UnityEngine.Debug.Log($"[FootIK] Weights=[{_footIKWeight[0]:F2}, {_footIKWeight[1]:F2}] HipsOffset={_hipsOffset:F3} Grounded={isGrounded}");
                }
            }

            // --- Look At IK ---
            if (_entityManager.HasComponent<LookAtIKState>(_entity))
            {
                var lookState = _entityManager.GetComponentData<LookAtIKState>(_entity);
                
                // Also get AimDirection to compare
                float3 aimPoint = float3.zero;
                if (_entityManager.HasComponent<AimDirection>(_entity))
                {
                    var aimDir = _entityManager.GetComponentData<AimDirection>(_entity);
                    aimPoint = aimDir.AimPoint;
                }
                
                // Debug logging every 300 frames (~5 seconds)
                if (Time.frameCount % 300 == 0)
                {
                    UnityEngine.Debug.Log($"[LookAtIK] Bridge: Entity={_entity.Index}:{_entity.Version} HasTarget={lookState.HasTarget} Weight={lookState.CurrentWeight:F2} Target={lookState.LookTarget} Smoothed={lookState.SmoothedTarget} AimPoint={aimPoint}");
                }
                
                if (lookState.HasTarget && lookState.CurrentWeight > 0.01f)
                {
                    _animator.SetLookAtPosition(lookState.LookTarget);

                    // ECS calculates simplified weights, but here we can read detailed settings if we wanted
                    // For now, assume TargetWeight applies to all for simplicity, or hardcode distribution
                    float w = lookState.CurrentWeight;
                    _animator.SetLookAtWeight(w, w * 0.3f, w, 0.5f, 0.5f);
                }
            }

            // --- Hand IK (EPIC 13.17.2) --- (DISABLED: same issue as foot IK, needs debugging)
            // TODO: Re-enable once HandIKSystem is properly tested
            // if (!isClimbing && _entityManager.HasComponent<HandIKState>(_entity))
            // {
            //     ... hand IK code disabled ...
            // }
        }
        void OnAnimatorMove()
        {
            if (!_isLinked || !_entityManager.Exists(_entity)) return;

            // Capture Root Motion Delta
            // We only write this if the entity has the component (meaning it's expecting root motion)
            if (_entityManager.HasComponent<RootMotionDelta>(_entity))
            {
                var deltaData = _entityManager.GetComponentData<RootMotionDelta>(_entity);
                
                // Only write if system requested root motion usage (or we decide to always write?)
                // Writing always allows the system to decide when to use it.
                // However, we must be careful about main thread write access.
                // EntityManager.SetComponentData is valid on Main Thread.
                
                // Accumulate delta? Or just set?
                // OnAnimatorMove is called once per frame. The system consumes it.
                // But the system runs in FixedUpdate/Update loop which might be different rate.
                // Ideally, we accumulate until consumed.
                
                // For simplicity in Phase 1: Overwrite.
                // Note: Position is delta, Rotation is delta.
                
                deltaData.PositionDelta = _animator.deltaPosition;
                deltaData.RotationDelta = _animator.deltaRotation;
                
                _entityManager.SetComponentData(_entity, deltaData);
            }
        }
    }
}
