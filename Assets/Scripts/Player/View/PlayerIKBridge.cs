using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Player.IK;
using DIG.Player.Abilities; // For RootMotionDelta
using Player.Components; // For FreeClimbState

namespace DIG.Player.View
{
    /// <summary>
    /// Player IK bridge - handles all character IK via OnAnimatorIK.
    /// Split into partial classes for maintainability:
    /// - PlayerIKBridge.cs         - Core fields, initialization, dispatch
    /// - PlayerIKBridge.FootIK.cs  - Foot ground placement
    /// - PlayerIKBridge.LookAtIK.cs - Head/body tracking
    /// - PlayerIKBridge.HandIK.cs  - Hand/arm positioning for weapons
    /// - PlayerIKBridge.Interpolation.cs - Ability-driven IK transitions
    /// - PlayerIKBridge.Events.cs  - Death/respawn/equipment events
    /// 
    /// Uses techniques from Opsive CharacterIK.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public partial class PlayerIKBridge : MonoBehaviour
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
        
        // Hand IK state
        private float[] _handPositionIKWeight = new float[2];
        private float[] _handRotationIKWeight = new float[2];
        private float _upperArmWeight = 0f;
        private Vector3 _dominantHandPosition;
        private Vector3 _nonDominantHandPosition;
        private Vector3 _nonDominantHandOffset;
        private Vector3 _handOffset;
        
        // Spring state for recoil
        private Vector3[] _handPositionSpringValue = new Vector3[2];
        private Vector3[] _handPositionSpringVelocity = new Vector3[2];
        private Vector3[] _handRotationSpringValue = new Vector3[2];
        private Vector3[] _handRotationSpringVelocity = new Vector3[2];
        
        // Cached bone transforms
        private Transform _leftFoot, _rightFoot, _leftLowerLeg, _rightLowerLeg, _hips;
        private Transform _leftHand, _rightHand, _leftUpperArm, _rightUpperArm;
        private Transform _head;
        
        // Dominant hand tracking
        private Transform _dominantHand;
        private Transform _nonDominantHand;
        private Transform _dominantUpperArm;
        private bool _isRightHandDominant = true;
        
        // IK Target Interpolation (for ability-driven IK like climbing)
        private const int IKGoalCount = 8; // From IKGoal enum
        private float[] _ikInterpolationStart = new float[IKGoalCount]; // -1 = not interpolating
        private float[] _ikInterpolationDuration = new float[IKGoalCount];
        private Vector3[] _ikInterpolationPosition = new Vector3[IKGoalCount];
        private Quaternion[] _ikInterpolationRotation = new Quaternion[IKGoalCount];
        private bool[] _ikTargetActive = new bool[IKGoalCount];
        
        // Animation Layer Indices (configurable based on animator controller)
        private const int BaseLayerIndex = 0;       // Foot IK + Look At
        private const int UpperBodyLayerIndex = 4;  // Hand rotation + Upper arm + Hand position (matches Opsive default)
        private bool _requireSecondHandPositioning = false; // Set when two-handed weapon needs second pass
        private bool _ikEnabled = true; // Master IK enable flag (disabled on death)

        void Awake()
        {
            _animator = GetComponent<Animator>();
            
            // Initialize IK interpolation arrays to inactive state
            for (int i = 0; i < IKGoalCount; i++)
            {
                _ikInterpolationStart[i] = -1f; // -1 means not interpolating
                _ikInterpolationRotation[i] = Quaternion.identity;
            }
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
            
            // Cache bone transforms for hand IK
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            
            // Set default dominant hand (right)
            SetDominantHand(true);
            
            // Mark for T-pose calibration on first frame
            _footIKCalibrated = false;
            
            UnityEngine.Debug.Log($"[PlayerIK] Bridge linked to entity {entity.Index}:{entity.Version}");
        }
        
        /// <summary>
        /// Sets which hand is dominant (holds the weapon).
        /// </summary>
        public void SetDominantHand(bool rightHanded)
        {
            _isRightHandDominant = rightHanded;
            if (rightHanded)
            {
                _dominantHand = _rightHand;
                _nonDominantHand = _leftHand;
                _dominantUpperArm = _rightUpperArm;
            }
            else
            {
                _dominantHand = _leftHand;
                _nonDominantHand = _rightHand;
                _dominantUpperArm = _leftUpperArm;
            }
        }
        
        /// <summary>
        /// Adds recoil force to hand springs.
        /// </summary>
        public void AddRecoilForce(Vector3 positionalForce, Vector3 rotationalForce, bool globalForce = true)
        {
            if (globalForce || !_isRightHandDominant) // Left hand
            {
                _handPositionSpringVelocity[0] += positionalForce;
                _handRotationSpringVelocity[0] += rotationalForce;
            }
            if (globalForce || _isRightHandDominant) // Right hand
            {
                _handPositionSpringVelocity[1] += positionalForce;
                _handRotationSpringVelocity[1] += rotationalForce;
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (!_isLinked || !_entityManager.Exists(_entity))
            {
                if (Time.frameCount % 120 == 0)
                {
                    UnityEngine.Debug.LogWarning($"[PlayerIK] OnAnimatorIK called but not linked! isLinked={_isLinked}");
                }
                return;
            }
            
            // Master IK disable check (death, etc.)
            if (!_ikEnabled) return;

            // Climbing check (for future use - climbing IK handled separately)
            bool isClimbing = false;
            if (_entityManager.HasComponent<FreeClimbState>(_entity))
            {
                var climbState = _entityManager.GetComponentData<FreeClimbState>(_entity);
                isClimbing = climbState.IsClimbing;
            }
            
            // Multi-layer IK support (like Opsive CharacterIK)
            if (layerIndex == BaseLayerIndex)
            {
                // --- Base Layer: Foot IK + Look At + IK Target Interpolation ---
                
                // IK Target Interpolation - updates ability-driven IK targets
                UpdateIKTargetInterpolations();
                
                // Foot IK
                PositionLowerBody(isClimbing);
                
                // Look At IK
                LookAtTarget();
            }
            else if (layerIndex == UpperBodyLayerIndex)
            {
                // --- Upper Body Layer: Hand rotation + Upper arm + Hand position ---
                PositionUpperBody(isClimbing);
            }
            else if (_requireSecondHandPositioning)
            {
                // --- Full Body Layer: Secondary hand positioning for two-handed weapons ---
                PositionHandsSecondPass();
            }
            
            // Apply IK Target Interpolations (overrides other IK with ability-driven targets)
            // Done on base layer only to avoid double-application
            if (layerIndex == BaseLayerIndex)
            {
                ApplyIKTargetInterpolations();
            }
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
