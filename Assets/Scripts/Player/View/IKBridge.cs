using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Player.IK;
using DIG.Player.Abilities; // For RootMotionDelta
using Player.Components; // For FreeClimbState

namespace DIG.Player.View
{
    [RequireComponent(typeof(Animator))]
    public class IKBridge : MonoBehaviour
    {
        private Animator _animator;
        private Entity _entity;
        private EntityManager _entityManager;
        private bool _isLinked = false;

        void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void LinkState(Entity entity, EntityManager em)
        {
            _entity = entity;
            _entityManager = em;
            _isLinked = true;
            UnityEngine.Debug.Log($"[IKBridge] Linked to entity {entity.Index}");
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (!_isLinked || !_entityManager.Exists(_entity))
            {
                if (Time.frameCount % 120 == 0)
                {
                    UnityEngine.Debug.LogWarning($"[IKBridge] OnAnimatorIK called but not linked! isLinked={_isLinked}");
                }
                return;
            }
            if (layerIndex != 0) return; // Base layer only usually

            // Check if currently climbing - if so, let OpsiveClimbingIK handle limb IK
            bool isClimbing = false;
            if (_entityManager.HasComponent<FreeClimbState>(_entity))
            {
                var climbState = _entityManager.GetComponentData<FreeClimbState>(_entity);
                isClimbing = climbState.IsClimbing;
            }

            if (Time.frameCount % 5 == 0)
            {
                UnityEngine.Debug.Log($"[ClimbIK] (IKBridge) Frame {Time.frameCount}: Check. isClimbing={isClimbing}");
            }

            // --- Foot IK --- (DISABLED: was causing legs to go dead, needs debugging)
            // TODO: Re-enable once FootIKSystem is properly tested
            // if (!isClimbing && _entityManager.HasComponent<FootIKState>(_entity))
            // {
            //     ... foot IK code disabled ...
            // }

            // --- Look At IK ---
            if (_entityManager.HasComponent<LookAtIKState>(_entity))
            {
                var lookState = _entityManager.GetComponentData<LookAtIKState>(_entity);
                
                // Debug logging every 60 frames
                if (Time.frameCount % 60 == 0)
                {
                    UnityEngine.Debug.Log($"[IKBridge] LookAt: HasTarget={lookState.HasTarget} Weight={lookState.CurrentWeight:F2} Target={lookState.LookTarget}");
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
