using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Player.Components;

namespace Player.Bridges
{
    /// <summary>
    /// Client-side MonoBehaviour that procedurally places hands on climb surfaces using raycasts.
    /// Works with ClimbAnimatorBridge to set IK target positions.
    /// 
    /// Algorithm (from Invector vFreeClimb.OnAnimatorIK):
    /// 1. Get hand bone world position from Animator
    /// 2. Raycast from hand toward surface
    /// 3. If hit: Set IK position to hit point + offset
    /// 4. Lerp ikWeight for smooth transitions
    /// </summary>
    [RequireComponent(typeof(ClimbAnimatorBridge))]
    [AddComponentMenu("DIG/Player/Free Climb IK Controller")]
    public class FreeClimbIKController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator with climbing rig. Auto-found if not set.")]
        [SerializeField] private Animator animator;
        
        [Tooltip("ClimbAnimatorBridge to update IK targets on.")]
        [SerializeField] private ClimbAnimatorBridge climbBridge;
        
        [Header("Bone References")]
        [Tooltip("Left hand bone or transform for raycast origin (optional - uses Animator if not set)")]
        [SerializeField] private Transform leftHandBone;
        
        [Tooltip("Right hand bone or transform for raycast origin (optional)")]
        [SerializeField] private Transform rightHandBone;
        
        [Tooltip("Left foot bone (optional)")]
        [SerializeField] private Transform leftFootBone;
        
        [Tooltip("Right foot bone (optional)")]
        [SerializeField] private Transform rightFootBone;
        
        [Header("Raycast Settings")]
        [Tooltip("Layers to raycast against for hand placement")]
        [SerializeField] private LayerMask climbableLayers = ~0;
        
        [Tooltip("How far back from hand to start raycast")]
        [Range(0.1f, 1f)]
        [SerializeField] private float rayStartOffset = 0.5f;
        
        [Tooltip("Maximum raycast distance")]
        [Range(0.5f, 2f)]
        [SerializeField] private float rayDistance = 1.0f;
        
        [Tooltip("Vertical offset for hand raycasts (negative = down from bone)")]
        [Range(-0.5f, 0.5f)]
        [SerializeField] private float handVerticalOffset = -0.1f;
        
        [Tooltip("Horizontal spread between hands")]
        [Range(0.1f, 1f)]
        [SerializeField] private float handSpread = 0.3f;
        
        [Header("Foot Settings")]
        [Tooltip("Vertical offset for foot placement below grip")]
        [Range(0.5f, 2f)]
        [SerializeField] private float footVerticalOffset = 1.2f;
        
        [Tooltip("Horizontal spread between feet")]
        [Range(0.1f, 1f)]
        [SerializeField] private float footSpread = 0.2f;
        
        [Header("Smoothing")]
        [Tooltip("How fast IK targets move to new positions")]
        [Range(1f, 20f)]
        [SerializeField] private float ikTargetSpeed = 10f;
        
        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;
        
        // State
        private Entity _playerEntity = Entity.Null;
        private EntityManager _entityManager;
        private bool _initialized;
        private bool _wasClimbing = false; // Track previous climb state for trigger detection
        private Vector3 _leftHandTarget;
        private Vector3 _rightHandTarget;
        private Vector3 _leftFootTarget;
        private Vector3 _rightFootTarget;
        private Vector3 _currentGripPos;
        private Vector3 _currentGripNormal;
        
        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            
            if (climbBridge == null)
                climbBridge = GetComponent<ClimbAnimatorBridge>();
        }
        
        void Start()
        {
            // Try to find player entity
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                _initialized = true;
            }
        }
        
        void LateUpdate()
        {
            if (!_initialized || animator == null || climbBridge == null)
                return;
            
            // Find our player entity (search for entity with matching position)
            if (_playerEntity == Entity.Null)
            {
                FindPlayerEntity();
                if (_playerEntity != Entity.Null)
                {
                    // Sync wasClimbing with current state to prevent false triggers
                    var state = _entityManager.GetComponentData<FreeClimbState>(_playerEntity);
                    _wasClimbing = state.IsClimbing && !state.IsTransitioning;
                }
                return;
            }
            
            // Check if entity still exists
            if (!_entityManager.Exists(_playerEntity))
            {
                _playerEntity = Entity.Null;
                _wasClimbing = false; // Reset on entity loss
                return;
            }

            // Heartbeat Log
            if (Time.frameCount % 60 == 0 && _entityManager.HasComponent<FreeClimbState>(_playerEntity))
            {
                 var s = _entityManager.GetComponentData<FreeClimbState>(_playerEntity);
                 // Log if interesting state
                 if (s.IsClimbing || _wasClimbing || s.IsClimbingUp)
                 {
                     Debug.Log($"[FreeClimbIK] Heartbeat: Ent={_playerEntity}, IsClimbing={s.IsClimbing}, IsClimbingUp={s.IsClimbingUp}, WasClimbing={_wasClimbing}");
                 }
            }
            
            // Check if climbing
            if (!_entityManager.HasComponent<FreeClimbState>(_playerEntity))
                return;
            
            var climbState = _entityManager.GetComponentData<FreeClimbState>(_playerEntity);
            
            // Detect climb state changes for triggers
            bool isClimbing = climbState.IsClimbing && !climbState.IsTransitioning;
            
            if (isClimbing && !_wasClimbing)
            {
                // Just started climbing - trigger grab
                climbBridge.TriggerGrab();
            }
            else if (!isClimbing && _wasClimbing)
            {
                // Just stopped climbing - check if it's a ledge climb-up
                if (climbState.IsClimbingUp)
                {
                    // Vaulting over ledge
                    climbBridge.TriggerClimbUp();
                    Debug.Log("[FreeClimbIK] TriggerClimbUp: vaulting over ledge");
                }
                else
                {
                    Debug.Log($"[FreeClimbIK] Normal Release. IsClimbingUp={climbState.IsClimbingUp}, IsTransitioning={climbState.IsTransitioning}");
                    // Normal release/dismount
                    climbBridge.TriggerRelease();
                }
            }
            
            _wasClimbing = isClimbing;

            // During climb-up (Vault), we usually want to disable IK so the animation can move hands to the ledge top.
            // Keeping IK active would pin hands to the wall surface below.
            bool isWallJumping = climbState.IsWallJumping;
            bool isVaulting = climbState.IsClimbingUp;
            
            bool shouldUpdateIK = (isClimbing && !isWallJumping && !isVaulting);
            
            if (!shouldUpdateIK)
                return;
            
            // Get grip position and normal
            _currentGripPos = new Vector3(climbState.GripWorldPosition.x, climbState.GripWorldPosition.y, climbState.GripWorldPosition.z);
            _currentGripNormal = new Vector3(climbState.GripWorldNormal.x, climbState.GripWorldNormal.y, climbState.GripWorldNormal.z);
            
            // Calculate surface-relative axes
            Vector3 surfaceRight = Vector3.Cross(_currentGripNormal, Vector3.up).normalized;
            if (surfaceRight.sqrMagnitude < 0.01f)
                surfaceRight = Vector3.Cross(_currentGripNormal, transform.forward).normalized;
            Vector3 surfaceUp = Vector3.Cross(surfaceRight, _currentGripNormal).normalized;
            
            // Raycast for each limb
            RaycastForHand(true, surfaceRight, surfaceUp, ref _leftHandTarget);
            RaycastForHand(false, surfaceRight, surfaceUp, ref _rightHandTarget);
            RaycastForFoot(true, surfaceRight, surfaceUp, ref _leftFootTarget);
            RaycastForFoot(false, surfaceRight, surfaceUp, ref _rightFootTarget);
            
            // Update IK targets on bridge
            climbBridge.SetIKTargets(_leftHandTarget, _rightHandTarget, _leftFootTarget, _rightFootTarget);
        }
        
        void FindPlayerEntity()
        {
            // Search for player entity near our position
            var query = _entityManager.CreateEntityQuery(typeof(FreeClimbState), typeof(PlayerTag));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            float closestDist = float.MaxValue;
            Entity closest = Entity.Null;
            
            foreach (var entity in entities)
            {
                if (_entityManager.HasComponent<Unity.Transforms.LocalTransform>(entity))
                {
                    var lt = _entityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                    float dist = Vector3.Distance(transform.position, new Vector3(lt.Position.x, lt.Position.y, lt.Position.z));
                    if (dist < closestDist && dist < 1f) // Within 1m
                    {
                        closestDist = dist;
                        closest = entity;
                    }
                }
            }
            
            entities.Dispose();
            _playerEntity = closest;
        }
        
        void RaycastForHand(bool isLeft, Vector3 surfaceRight, Vector3 surfaceUp, ref Vector3 target)
        {
            // Calculate hand position offset from grip
            float hSign = isLeft ? -1f : 1f;
            Vector3 handOffset = (surfaceRight * handSpread * hSign) + (surfaceUp * handVerticalOffset);
            Vector3 targetPos = _currentGripPos + handOffset;
            
            // Raycast origin: behind the target position
            Vector3 rayOrigin = targetPos - _currentGripNormal * rayStartOffset;
            Vector3 rayDir = _currentGripNormal;
            
            if (debugDraw)
            {
                Debug.DrawRay(rayOrigin, rayDir * rayDistance, isLeft ? Color.red : Color.green);
            }
            
            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, rayDistance, climbableLayers))
            {
                // Smooth movement to new target
                target = Vector3.Lerp(target, hit.point, ikTargetSpeed * Time.deltaTime);
            }
            else
            {
                // No hit - use projected position on surface
                target = Vector3.Lerp(target, targetPos, ikTargetSpeed * Time.deltaTime);
            }
        }
        
        void RaycastForFoot(bool isLeft, Vector3 surfaceRight, Vector3 surfaceUp, ref Vector3 target)
        {
            // Feet are below and slightly spread from grip
            float hSign = isLeft ? -1f : 1f;
            Vector3 footOffset = (surfaceRight * footSpread * hSign) - (surfaceUp * footVerticalOffset);
            Vector3 targetPos = _currentGripPos + footOffset;
            
            // Raycast toward surface
            Vector3 rayOrigin = targetPos - _currentGripNormal * rayStartOffset;
            Vector3 rayDir = _currentGripNormal;
            
            if (debugDraw)
            {
                Debug.DrawRay(rayOrigin, rayDir * rayDistance, isLeft ? Color.magenta : Color.yellow);
            }
            
            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, rayDistance, climbableLayers))
            {
                target = Vector3.Lerp(target, hit.point, ikTargetSpeed * Time.deltaTime);
            }
            else
            {
                target = Vector3.Lerp(target, targetPos, ikTargetSpeed * Time.deltaTime);
            }
        }
        
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            // Draw IK targets
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_leftHandTarget, 0.05f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_rightHandTarget, 0.05f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_leftFootTarget, 0.05f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_rightFootTarget, 0.05f);
            
            // Draw grip position
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_currentGripPos, 0.08f);
            Gizmos.DrawRay(_currentGripPos, _currentGripNormal * 0.3f);
        }
    }
}
