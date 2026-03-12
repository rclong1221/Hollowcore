using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Player.Components;

namespace Player.Bridges
{
    /// <summary>
    /// Drives Blitz animations from ECS state.
    /// Similar pattern to ClimbAnimatorBridge.
    /// Attach to Blitz prefab (Client presentation).
    /// 
    /// IMPORTANT: This reads from the CLIENT WORLD's entity data,
    /// which receives replicated MountMovementInput from the server.
    /// </summary>
    public class BlitzAnimatorBridge : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Blitz's Animator component")]
        public Animator blitzAnimator;
        
        [Header("Entity Binding")]
        [Tooltip("Set at runtime by BlitzAnimatorBindingSystem")]
        public Entity boundEntity;
        
        [Header("Animation Settings")]
        [Tooltip("Speed multiplier for locomotion")]
        public float locomotionSpeedMultiplier = 1f;
        
        [Header("Debug")]
        public bool debugLogging = true;
        
        // Animator parameter hashes
        private int _hashHorizontalMovement;
        private int _hashForwardMovement;
        private int _hashSpeed;
        private int _hashMoving;
        private int _hashAbilityIndex;
        private int _hashAbilityIntData;
        
        private World _clientWorld;
        private EntityManager _entityManager;
        private bool _initialized;
        private bool _hasLoggedBinding;
        
        void Awake()
        {
            if (blitzAnimator == null)
                blitzAnimator = GetComponentInChildren<Animator>();
                
            CacheParameterHashes();
            
            // CRITICAL: Disable Opsive's AnimatorMonitor to prevent it from overwriting our Speed/Movement values
            DisableOpsiveAnimatorMonitor();
        }
        
        void Start()
        {
            // Find the CLIENT world - this is where our animator bridge reads from
            TryFindClientWorld();
        }
        
        /// <summary>
        /// Disables Opsive's AnimatorMonitor and ChildAnimatorMonitor on the Blitz animator.
        /// Without this, Opsive will overwrite our Speed parameter every frame.
        /// </summary>
        void DisableOpsiveAnimatorMonitor()
        {
            if (blitzAnimator == null) return;
            
            // Check on the animator's GameObject
            var opsiveMonitor = blitzAnimator.GetComponent<Opsive.UltimateCharacterController.Character.AnimatorMonitor>();
            if (opsiveMonitor != null)
            {
                opsiveMonitor.enabled = false;
                if (debugLogging)
                    Debug.Log($"[BlitzAnimatorBridge] DISABLED Opsive AnimatorMonitor on '{blitzAnimator.gameObject.name}'");
            }
            
            var childMonitor = blitzAnimator.GetComponent<Opsive.UltimateCharacterController.Character.ChildAnimatorMonitor>();
            if (childMonitor != null)
            {
                childMonitor.enabled = false;
                if (debugLogging)
                    Debug.Log($"[BlitzAnimatorBridge] DISABLED Opsive ChildAnimatorMonitor on '{blitzAnimator.gameObject.name}'");
            }
            
            // Also check parent/root in case it's there
            var rootMonitor = GetComponentInParent<Opsive.UltimateCharacterController.Character.AnimatorMonitor>();
            if (rootMonitor != null && rootMonitor != opsiveMonitor)
            {
                rootMonitor.enabled = false;
                if (debugLogging)
                    Debug.Log($"[BlitzAnimatorBridge] DISABLED Opsive AnimatorMonitor on parent '{rootMonitor.gameObject.name}'");
            }
        }
        
        void TryFindClientWorld()
        {
            // Look for client world specifically
            foreach (var world in World.All)
            {
                if (world.Name.Contains("Client") && !world.Name.Contains("Thin"))
                {
                    _clientWorld = world;
                    _entityManager = world.EntityManager;
                    _initialized = true;
                    if (debugLogging)
                        Debug.Log($"[BlitzAnimatorBridge] Found client world: {world.Name}");
                    return;
                }
            }
            
            // Fallback to default world (for single-player/editor testing)
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                _clientWorld = World.DefaultGameObjectInjectionWorld;
                _entityManager = _clientWorld.EntityManager;
                _initialized = true;
                if (debugLogging)
                    Debug.Log($"[BlitzAnimatorBridge] Using default world: {_clientWorld.Name}");
            }
        }
        
        void CacheParameterHashes()
        {
            _hashHorizontalMovement = Animator.StringToHash("HorizontalMovement");
            _hashForwardMovement = Animator.StringToHash("ForwardMovement");
            _hashSpeed = Animator.StringToHash("Speed");
            _hashMoving = Animator.StringToHash("Moving");
            _hashAbilityIndex = Animator.StringToHash("AbilityIndex");
            _hashAbilityIntData = Animator.StringToHash("AbilityIntData");
        }
        
        void LateUpdate()
        {
            // Retry finding client world if not initialized
            if (!_initialized)
            {
                TryFindClientWorld();
                if (!_initialized)
                    return;
            }
            
            if (blitzAnimator == null)
                return;
            
            // Entity not bound yet - binding system will set it
            if (boundEntity == Entity.Null)
            {
                return;
            }
            
            if (!_entityManager.Exists(boundEntity))
            {
                if (debugLogging && _hasLoggedBinding)
                    Debug.LogWarning($"[BlitzAnimatorBridge] Bound entity {boundEntity.Index} no longer exists!");
                return;
            }
            
            if (!_hasLoggedBinding && debugLogging)
            {
                Debug.Log($"[BlitzAnimatorBridge] Entity {boundEntity.Index} bound and exists in {_clientWorld.Name}");
                _hasLoggedBinding = true;
            }
            
            // Read MountMovementInput directly - this is replicated from server via [GhostField]
            if (_entityManager.HasComponent<MountMovementInput>(boundEntity))
            {
                var mountInput = _entityManager.GetComponentData<MountMovementInput>(boundEntity);
                
                float horizontal = mountInput.HorizontalInput;
                float forward = mountInput.ForwardInput;
                bool isSprinting = mountInput.SprintInput > 0;
                
                // BlitzDemo controller blend tree uses Speed thresholds: 0-13+
                // 0 = Idle, ~2 = Walk/Trot, ~5+ = Run/Gallop
                // This matches Opsive's typical Speed parameter scale
                float speed = 0f;
                if (Mathf.Abs(forward) > 0.01f || Mathf.Abs(horizontal) > 0.01f)
                {
                    // Walk = 2, Run = 6 (well into the blend tree's run range)
                    speed = isSprinting ? 6f : 2f;
                }
                
                bool moving = speed > 0.01f;
                
                // Debug: log when we actually get input
                if (debugLogging && moving)
                {
                    Debug.Log($"[BlitzAnimatorBridge] Input: H={horizontal:F2}, F={forward:F2}, Speed={speed:F2}, Sprint={isSprinting}");
                }
                
                blitzAnimator.SetFloat(_hashHorizontalMovement, horizontal * locomotionSpeedMultiplier);
                blitzAnimator.SetFloat(_hashForwardMovement, forward * locomotionSpeedMultiplier);
                blitzAnimator.SetFloat(_hashSpeed, speed);
                blitzAnimator.SetBool(_hashMoving, moving);
            }
            else
            {
                // No input component - just idle
                SetIdleState();
            }
        }
        
        void SetIdleState()
        {
            blitzAnimator.SetFloat(_hashHorizontalMovement, 0f);
            blitzAnimator.SetFloat(_hashForwardMovement, 0f);
            blitzAnimator.SetFloat(_hashSpeed, 0f);
            blitzAnimator.SetBool(_hashMoving, false);
        }
        
        /// <summary>
        /// Bind to a specific entity (called by BlitzAnimatorBindingSystem).
        /// </summary>
        public void BindToEntity(Entity entity)
        {
            boundEntity = entity;
            _hasLoggedBinding = false;
            
            if (debugLogging)
                Debug.Log($"[BlitzAnimatorBridge] BindToEntity called with entity {entity.Index}:{entity.Version}");
        }
        
        /// <summary>
        /// Set locomotion directly (for testing or AI control).
        /// </summary>
        public void SetLocomotion(float horizontal, float forward, float speed)
        {
            if (blitzAnimator == null) return;
            
            bool moving = Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(forward) > 0.01f;
            
            blitzAnimator.SetFloat(_hashHorizontalMovement, horizontal);
            blitzAnimator.SetFloat(_hashForwardMovement, forward);
            blitzAnimator.SetFloat(_hashSpeed, speed);
            blitzAnimator.SetBool(_hashMoving, moving);
        }
    }
}

