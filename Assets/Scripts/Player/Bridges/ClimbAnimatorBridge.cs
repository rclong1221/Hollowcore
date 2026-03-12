using UnityEngine;
using UnityEngine.Events;
using Player.Systems;
using Player.Animation;
using Unity.Mathematics;

namespace Player.Bridges
{
    /// <summary>
    /// Full-featured animation bridge for climbing visuals (Epic 1.9).
    /// Drives climbing animator parameters, IK hand/foot targets, rig weights, and optional root motion.
    /// Receives animation events (OnGrabAnchor, OnReleaseAnchor) to trigger audio/VFX.
    /// 
    /// Designer Workflow:
    /// 1. Attach to player UI prefab alongside Animator
    /// 2. Configure parameter names to match your Animator Controller
    /// 3. Set up IK targets for hands/feet if using procedural IK
    /// 4. Hook up UnityEvents for audio/VFX on climb events
    /// </summary>
    [DisallowMultipleComponent]
    public class ClimbAnimatorBridge : MonoBehaviour, IPlayerAnimationBridge
    {
        #region References
        [Header("References")]
        [Tooltip("Animator component on this player character. Auto-found if not set.")]
        [SerializeField] Animator animator;
        
        [Tooltip("Optional CharacterController for root motion. Usually left empty for hybrid Ghost/UI setups.")]
        [SerializeField] CharacterController characterController;
        #endregion
        
        #region Climbing Parameters
        [Header("Climbing Animator Parameters")]
        [Tooltip("Bool parameter - true while climbing")]
        [SerializeField] string paramIsClimbing = "IsClimbing";
        
        [Tooltip("Float parameter - climb progress (0 = bottom, 1 = top)")]
        [SerializeField] string paramClimbProgress = "ClimbProgress";
        
        [Tooltip("Float parameter - vertical climb speed for animation blending (optional)")]
        [SerializeField] string paramClimbSpeed = "";
        
        [Tooltip("Trigger parameter - fired when grabbing a new anchor (optional)")]
        [SerializeField] string paramGrabTrigger = "";
        
        [Tooltip("Trigger parameter - fired when releasing/dismounting (optional)")]
        [SerializeField] string paramReleaseTrigger = "";
        
        [Tooltip("Float parameter - horizontal movement for pipe/wall traversal (optional)")]
        [SerializeField] string paramClimbHorizontal = "";
        
        [Tooltip("Trigger parameter - fired when climbing up/vaulting over a ledge (optional)")]
        [SerializeField] string paramClimbUpTrigger = "ClimbUpTrigger";
        #endregion
        
        #region Blend Parameters
        [Header("Blend Parameters (Standard)")]
        [Tooltip("Float parameter for overall movement speed")]
        [SerializeField] string paramSpeed = "Speed";
        #endregion
        
        #region Parameters
        [Header("Parameters")]
        [Tooltip("Int parameter - active ability index (503 = FreeClimb)")]
        [SerializeField] string paramAbilityIndex = "AbilityIndex";
        
        [Tooltip("Bool parameter - triggers on ability change")]
        [SerializeField] string paramAbilityChange = "AbilityChange";
        
        [Tooltip("Int parameter - sub-state within ability (0=mount, 2=climb, 5=dismount, 6=vault)")]
        [SerializeField] string paramAbilityIntData = "AbilityIntData";
        
        [Tooltip("Float parameter - blend direction within ability (-1 to 1)")]
        [SerializeField] string paramAbilityFloatData = "AbilityFloatData";
        
        [Tooltip("Float parameter - horizontal movement input for blend trees")]
        [SerializeField] string paramHorizontalMovement = "HorizontalMovement";
        
        [Tooltip("Float parameter - forward/vertical movement input for blend trees")]
        [SerializeField] string paramForwardMovement = "ForwardMovement";

        [Tooltip("Bool parameter - moving state")]
        [SerializeField] string paramMoving = "Moving";

        [Tooltip("Int parameter - movement set ID (0=Combat, 2=Adventure)")]
        [SerializeField] string paramMovementSetID = "MovementSetID";

        [Tooltip("Float parameter - height (0=Stand, 1=Crouch)")]
        [SerializeField] string paramHeight = "Height";
        #endregion
        
        #region IK Settings
        [Header("IK Settings")]
        [Tooltip("Enable procedural IK for hands and feet during climbing")]
        public bool EnableIK = false;
        
        [Tooltip("Opsive-exact IK component (auto-found or assign manually)")]
        [SerializeField] public OpsiveClimbingIK opsiveIK;
        
        [Tooltip("Left hand IK target transform (set at runtime based on anchor)")]
        public Transform LeftHandIKTarget;
        
        [Tooltip("Right hand IK target transform")]
        public Transform RightHandIKTarget;
        
        [Tooltip("Left foot IK target transform")]
        public Transform LeftFootIKTarget;
        
        [Tooltip("Right foot IK target transform")]
        public Transform RightFootIKTarget;
        
        [Tooltip("IK weight when actively climbing (0-1)")]
        [Range(0f, 1f)]
        public float ClimbingIKWeight = 1f;
        
        [Tooltip("How fast IK weights blend in/out")]
        public float IKBlendSpeed = 5f;
        #endregion
        
        #region Root Motion Settings
        [Header("Root Motion Settings")]
        [Tooltip("Apply animator root motion during climbing. Usually false for hybrid Ghost/UI setups.")]
        public bool ApplyRootMotion = false;
        
        [Tooltip("Scale factor for root motion translation")]
        [Range(0f, 2f)]
        public float RootMotionScale = 1f;
        #endregion
        
        #region Events
        [Header("Events (for Audio/VFX)")]
        [Tooltip("Invoked when player starts climbing - use for grab sound")]
        public UnityEvent OnClimbStart;
        
        [Tooltip("Invoked when player dismounts/releases - use for release sound")]
        public UnityEvent OnClimbEnd;
        
        [Tooltip("Invoked by animation event 'OnGrabAnchor' - precise frame for hand grab VFX")]
        public UnityEvent OnGrabAnchorEvent;
        
        [Tooltip("Invoked by animation event 'OnReleaseAnchor' - for hand release VFX")]
        public UnityEvent OnReleaseAnchorEvent;
        
        [Tooltip("Invoked on each climb step animation event")]
        public UnityEvent OnClimbStepEvent;
        #endregion
        
        #region Debug
        [Header("Debug")]
        [Tooltip("Enable debug logging for climb events")]
        public bool DebugLogging = false;
        
        [Tooltip("Global debug toggle for all climb systems")]
        public static bool EnableDebugLog = false;
        #endregion
        
        // Cached hashes
        private int h_IsClimbing;
        private int h_ClimbProgress;
        private int h_ClimbSpeed;
        private int h_GrabTrigger;
        private int h_ReleaseTrigger;
        private int h_ClimbHorizontal;
        private int h_ClimbUpTrigger;
        private int h_Speed;
        
        // 13.26: Opsive parameter hashes
        private int h_AbilityIndex;
        private int h_AbilityChange;
        private int h_AbilityIntData;
        private int h_AbilityFloatData;
        private int h_HorizontalMovement;
        private int h_ForwardMovement;
        private int h_Moving;
        private int h_MovementSetID;
        private int h_Height;
        
        // State tracking
        private bool _wasClimbing;
        private int _lastAbilityIndex; // Track AbilityIndex to detect actual ability transitions
        private int _lastAbilityIntData; // Track AbilityIntData to prevent oscillation
        private float _currentIKWeight;
        private float _lastProgress;
        private float _climbSpeedSmoothed;
        
        // Debouncing: prevent rapid flickering by requiring stable state for 4+ frames
        private int _pendingAbilityIndex;
        private int _pendingAbilityStableFrames;
        private int _pendingAbilityIntData;
        private int _pendingIntDataStableFrames;
        private const int REQUIRED_STABLE_FRAMES = 4; // ~66ms at 60fps
        private const int REQUIRED_INTDATA_STABLE_FRAMES = 2; // Faster for sub-state changes
        
        // IK tracking
        private Vector3 _wallNormal = Vector3.back;
        private Vector3 _gripPosition;
        
        #region Unity Lifecycle
        void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            characterController = GetComponent<CharacterController>();
            CacheHashes();
        }

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            
            CacheHashes();
            InitializeSolver();
            
            // CRITICAL: Disable Opsive's AnimatorMonitor to prevent it from overwriting our movement values
            DisableOpsiveAnimatorMonitor();
        }
        
        /// <summary>
        /// Disables Opsive's AnimatorMonitor and ChildAnimatorMonitor components to prevent them from overwriting our movement parameters.
        /// </summary>
        void DisableOpsiveAnimatorMonitor()
        {
            if (animator == null) return;
            
            var opsiveMonitor = animator.GetComponent<Opsive.UltimateCharacterController.Character.AnimatorMonitor>();
            if (opsiveMonitor != null)
            {
                opsiveMonitor.enabled = false;
                Debug.Log($"[ClimbAnimatorBridge] DISABLED Opsive AnimatorMonitor on '{animator.gameObject.name}'");
            }
            
            var childMonitor = animator.GetComponent<Opsive.UltimateCharacterController.Character.ChildAnimatorMonitor>();
            if (childMonitor != null)
            {
                childMonitor.enabled = false;
                Debug.Log($"[ClimbAnimatorBridge] DISABLED Opsive ChildAnimatorMonitor on '{animator.gameObject.name}'");
            }
        }
        
        void InitializeSolver()
        {
            // Initialize OpsiveClimbingIK (the only IK solver)
            if (opsiveIK == null)
            {
                opsiveIK = GetComponent<OpsiveClimbingIK>();
                if (opsiveIK == null)
                {
                    opsiveIK = gameObject.AddComponent<OpsiveClimbingIK>();
                }
            }
        }

        void OnValidate()
        {
            CacheHashes();
        }
        
        void Update()
        {
            // Blend IK weights smoothly
            float targetWeight = (_wasClimbing && EnableIK) ? ClimbingIKWeight : 0f;
            _currentIKWeight = Mathf.MoveTowards(_currentIKWeight, targetWeight, IKBlendSpeed * Time.deltaTime);
        }
        
        void OnAnimatorIK(int layerIndex)
        {
            // OpsiveClimbingIK handles its own OnAnimatorIK callback
            // This method is now just a passthrough
            if (animator == null || !EnableIK) return;
        }
        
        void OnAnimatorMove()
        {
            if (!ApplyRootMotion) return;
            if (animator == null) return;
            if (!_wasClimbing) return;
            
            Vector3 deltaPosition = animator.deltaPosition * RootMotionScale;
            
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(deltaPosition);
            }
            else
            {
                transform.position += deltaPosition;
            }
            
            transform.rotation *= animator.deltaRotation;
        }
        #endregion
        
        #region Private Methods
        void CacheHashes()
        {
            h_IsClimbing = !string.IsNullOrEmpty(paramIsClimbing) ? Animator.StringToHash(paramIsClimbing) : 0;
            h_ClimbProgress = !string.IsNullOrEmpty(paramClimbProgress) ? Animator.StringToHash(paramClimbProgress) : 0;
            h_ClimbSpeed = !string.IsNullOrEmpty(paramClimbSpeed) ? Animator.StringToHash(paramClimbSpeed) : 0;
            h_GrabTrigger = !string.IsNullOrEmpty(paramGrabTrigger) ? Animator.StringToHash(paramGrabTrigger) : 0;
            h_ReleaseTrigger = !string.IsNullOrEmpty(paramReleaseTrigger) ? Animator.StringToHash(paramReleaseTrigger) : 0;
            h_ClimbHorizontal = !string.IsNullOrEmpty(paramClimbHorizontal) ? Animator.StringToHash(paramClimbHorizontal) : 0;
            h_ClimbUpTrigger = !string.IsNullOrEmpty(paramClimbUpTrigger) ? Animator.StringToHash(paramClimbUpTrigger) : 0;
            h_Speed = !string.IsNullOrEmpty(paramSpeed) ? Animator.StringToHash(paramSpeed) : 0;
            
            // 13.26: Opsive parameter hashes
            h_AbilityIndex = !string.IsNullOrEmpty(paramAbilityIndex) ? Animator.StringToHash(paramAbilityIndex) : 0;
            h_AbilityChange = !string.IsNullOrEmpty(paramAbilityChange) ? Animator.StringToHash(paramAbilityChange) : 0;
            h_AbilityIntData = !string.IsNullOrEmpty(paramAbilityIntData) ? Animator.StringToHash(paramAbilityIntData) : 0;
            h_AbilityFloatData = !string.IsNullOrEmpty(paramAbilityFloatData) ? Animator.StringToHash(paramAbilityFloatData) : 0;
            h_HorizontalMovement = !string.IsNullOrEmpty(paramHorizontalMovement) ? Animator.StringToHash(paramHorizontalMovement) : 0;
            h_ForwardMovement = !string.IsNullOrEmpty(paramForwardMovement) ? Animator.StringToHash(paramForwardMovement) : 0;
            h_Moving = !string.IsNullOrEmpty(paramMoving) ? Animator.StringToHash(paramMoving) : 0;
            h_Height = !string.IsNullOrEmpty(paramHeight) ? Animator.StringToHash(paramHeight) : 0;
            h_MovementSetID = !string.IsNullOrEmpty(paramMovementSetID) ? Animator.StringToHash(paramMovementSetID) : 0;
            
            // Validate against actual controller parameters to avoid "Parameter does not exist" errors
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var validHashes = new System.Collections.Generic.HashSet<int>();
                foreach (var param in animator.parameters)
                {
                    validHashes.Add(param.nameHash);
                }
                
                if (!validHashes.Contains(h_IsClimbing)) h_IsClimbing = 0;
                if (!validHashes.Contains(h_ClimbProgress)) h_ClimbProgress = 0;
                if (!validHashes.Contains(h_ClimbSpeed)) h_ClimbSpeed = 0;
                if (!validHashes.Contains(h_GrabTrigger)) h_GrabTrigger = 0;
                if (!validHashes.Contains(h_ReleaseTrigger)) h_ReleaseTrigger = 0;
                if (!validHashes.Contains(h_ClimbHorizontal)) h_ClimbHorizontal = 0;
                if (!validHashes.Contains(h_ClimbUpTrigger)) h_ClimbUpTrigger = 0;
                if (!validHashes.Contains(h_Speed)) h_Speed = 0;
                
                if (!validHashes.Contains(h_AbilityIndex)) 
                {
                    Debug.LogError("[ClimbAnimatorBridge] CRITICAL: Parameter 'AbilityIndex' missing in Animator Controller! Climbing animations will NOT play. Please update the controller.");
                    h_AbilityIndex = 0;
                }
                if (!validHashes.Contains(h_AbilityChange)) h_AbilityChange = 0;
                if (!validHashes.Contains(h_AbilityIntData)) h_AbilityIntData = 0;
                if (!validHashes.Contains(h_AbilityFloatData)) h_AbilityFloatData = 0;
                if (!validHashes.Contains(h_HorizontalMovement)) h_HorizontalMovement = 0;
                if (!validHashes.Contains(h_ForwardMovement)) h_ForwardMovement = 0;
                if (!validHashes.Contains(h_Moving)) h_Moving = 0;
                if (!validHashes.Contains(h_Height)) h_Height = 0;
                if (!validHashes.Contains(h_MovementSetID)) h_MovementSetID = 0;
            }
        }
        #endregion
        
        #region Public API
        /// <summary>
        /// Trigger the grab animation and fire events.
        /// Called when player mounts a climbable.
        /// </summary>
        public void TriggerGrab()
        {
            if (animator == null) return;
            
            // Debug.Log($"[CLIMB_DIAG] TriggerGrab() called!");
            
            if (h_GrabTrigger != 0)
                animator.SetTrigger(h_GrabTrigger);
            
            OnClimbStart?.Invoke();
        }
        
        /// <summary>
        /// Trigger the release animation and fire events.
        /// Called when player dismounts from a climbable.
        /// </summary>
        public void TriggerRelease()
        {
            if (animator == null) return;
            
            // Debug.Log($"[CLIMB_DIAG] TriggerRelease() called!");
            
            if (h_ReleaseTrigger != 0)
                animator.SetTrigger(h_ReleaseTrigger);
            
            OnClimbEnd?.Invoke();
        }
        
        /// <summary>
        /// Trigger the climb-up/vault animation.
        /// Called when player vaults over a ledge.
        /// </summary>
        public void TriggerClimbUp()
        {
            if (animator == null) return;
            
            if (h_ClimbUpTrigger != 0)
                animator.SetTrigger(h_ClimbUpTrigger);
            
            // Log removed
        }
        
        /// <summary>
        /// Update IK state for Opsive climbing system.
        /// Called from PlayerAnimatorBridgeSystem when climbing is active.
        /// </summary>
        public void UpdateClimbingIK(Vector3 gripPosition, Vector3 wallNormal, bool isHanging)
        {
            _wallNormal = wallNormal;
            _gripPosition = gripPosition;
            
            // Pass climbing state to OpsiveClimbingIK
            // Auto-create if needed
            if (opsiveIK == null)
            {
                // Try to find on animator's gameobject (where OnAnimatorIK runs)
                if (animator != null)
                {
                    opsiveIK = animator.GetComponent<OpsiveClimbingIK>();
                    if (opsiveIK == null)
                    {
                        opsiveIK = animator.gameObject.AddComponent<OpsiveClimbingIK>();
                        // Log removed
                    }
                }
            }

            if (opsiveIK != null)
            {
                // We assume isClimbing=true if this method is called by the system
                // (System filters by ECS IsClimbing state)
                opsiveIK.SetClimbingState(true, isHanging, wallNormal, gripPosition);

                // if (DebugLogging && Time.frameCount % 60 == 0)
                //    Debug.Log($"[ClimbIK] SetClimbingState: EnableIK={EnableIK}, isHanging={isHanging}, wall={wallNormal}, grip={gripPosition}");
            }
        }

        private void DisableIK()
        {
            if (opsiveIK != null)
            {
                opsiveIK.SetClimbingState(false, false, Vector3.zero, Vector3.zero);
            }
        }
        
        /// <summary>
        /// Set horizontal climbing input for pipe/wall traversal animations.
        /// </summary>
        public void SetHorizontalInput(float horizontal)
        {
            if (animator == null) return;
            if (h_ClimbHorizontal != 0)
                animator.SetFloat(h_ClimbHorizontal, horizontal);
        }

        /// <summary>
        /// Update IK target positions from external controller (e.g. FreeClimbIKController).
        /// </summary>
        public void SetIKTargets(Vector3 leftHand, Vector3 rightHand, Vector3 leftFoot, Vector3 rightFoot)
        {
            if (LeftHandIKTarget != null) LeftHandIKTarget.position = leftHand;
            if (RightHandIKTarget != null) RightHandIKTarget.position = rightHand;
            if (LeftFootIKTarget != null) LeftFootIKTarget.position = leftFoot;
            if (RightFootIKTarget != null) RightFootIKTarget.position = rightFoot;
        }
        
        /// <summary>
        /// Check if currently in climbing state.
        /// </summary>
        public bool IsClimbing => _wasClimbing;
        
        /// <summary>
        /// Get current climb progress (0-1).
        /// </summary>
        public float ClimbProgress => _lastProgress;
        #endregion
        
        #region Animation Event Receivers
        /// <summary>
        /// Called by animation event when hand grabs an anchor.
        /// Add this event to your climbing animation clips.
        /// </summary>
        public void OnGrabAnchor()
        {
            OnGrabAnchorEvent?.Invoke();
            // Log removed
        }
        
        /// <summary>
        /// Called by animation event when hand releases an anchor.
        /// Add this event to your climbing animation clips.
        /// </summary>
        public void OnReleaseAnchor()
        {
            OnReleaseAnchorEvent?.Invoke();
            // Log removed
        }
        
        /// <summary>
        /// Called by animation event on each climb step.
        /// Add this event to your climbing loop animation.
        /// </summary>
        public void OnClimbStep()
        {
            OnClimbStepEvent?.Invoke();
            // Log removed
        }
        
        /// <summary>
        /// Generic animation event handler for custom events.
        /// </summary>
        public void OnAnimationEvent(string eventName)
        {
            // Log removed
            
            switch (eventName)
            {
                case "GrabAnchor":
                    OnGrabAnchor();
                    break;
                case "ReleaseAnchor":
                    OnReleaseAnchor();
                    break;
                case "ClimbStep":
                    OnClimbStep();
                    break;
            }
        }
        
        // ============================================
        // OPSIVE ANIMATION EVENT HANDLERS
        // Called automatically by Opsive animation clips
        // ============================================
        
        /// <summary>
        /// Called by Opsive animation event when mount animation is complete.
        /// The character is now in climbing position and ready to receive input.
        /// </summary>
        public void OnAnimatorFreeClimbStartInPosition()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorFreeClimbStartInPosition - Mount animation complete");
            
            Player.Components.FreeClimbAnimationEvents.QueueEvent(
                Player.Components.FreeClimbAnimationEvents.EventType.StartInPosition);
            
            OnGrabAnchorEvent?.Invoke();
        }
        
        /// <summary>
        /// Called by Opsive animation event when dismount animation is complete.
        /// The character has finished the dismount and should exit climb state.
        /// </summary>
        public void OnAnimatorFreeClimbComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorFreeClimbComplete - Dismount animation complete");
            
            Player.Components.FreeClimbAnimationEvents.QueueEvent(
                Player.Components.FreeClimbAnimationEvents.EventType.Complete);
            
            OnReleaseAnchorEvent?.Invoke();
        }
        
        /// <summary>
        /// Called by Opsive animation event when corner turn animation is complete.
        /// The character can resume normal climbing movement.
        /// </summary>
        public void OnAnimatorFreeClimbTurnComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorFreeClimbTurnComplete - Turn animation complete");
            
            Player.Components.FreeClimbAnimationEvents.QueueEvent(
                Player.Components.FreeClimbAnimationEvents.EventType.TurnComplete);
        }
        
        /// <summary>
        /// Called by Opsive animation event when hang start animation is complete.
        /// The character is now in hang position.
        /// </summary>
        public void OnAnimatorHangStartInPosition()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorHangStartInPosition - Hang transition complete");
            
            Player.Components.FreeClimbAnimationEvents.QueueEvent(
                Player.Components.FreeClimbAnimationEvents.EventType.HangStartInPosition);
        }
        
        /// <summary>
        /// Called by Opsive animation event when hang ability is complete (pull-up).
        /// The character should exit hang state.
        /// </summary>
        public void OnAnimatorHangComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorHangComplete - Hang exit complete");

            Player.Components.FreeClimbAnimationEvents.QueueEvent(
                Player.Components.FreeClimbAnimationEvents.EventType.HangComplete);
        }
        #endregion

        #region Agility Animation Events (EPIC 14.12)
        /// <summary>
        /// Called by Opsive animation event when dodge animation completes.
        /// Queues event for ECS to clear DodgeState.IsDodging.
        /// </summary>
        public void OnAnimatorDodgeComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorDodgeComplete - Dodge animation complete");

            Player.Components.AgilityAnimationEvents.QueueDodgeComplete();
        }

        /// <summary>
        /// Called by Opsive animation event when roll animation completes.
        /// Queues event for ECS to clear RollState.IsRolling.
        /// </summary>
        public void OnAnimatorRollComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorRollComplete - Roll animation complete");

            Player.Components.AgilityAnimationEvents.QueueRollComplete();
        }

        /// <summary>
        /// Called by Opsive animation event when vault animation completes.
        /// Queues event for ECS to clear VaultState.IsVaulting.
        /// </summary>
        public void OnAnimatorVaultComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorVaultComplete - Vault animation complete");

            Player.Components.AgilityAnimationEvents.QueueVaultComplete();
        }

        /// <summary>
        /// Called by Opsive animation event when crawl stop animation completes.
        /// Queues event for ECS to clear CrawlState.IsCrawling.
        /// </summary>
        public void OnAnimatorCrawlComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorCrawlComplete - Crawl animation complete");

            Player.Components.AgilityAnimationEvents.QueueCrawlComplete();
        }
        #endregion
        
        #region Swimming Pack Animation Events (EPIC 14.13)
        /// <summary>
        /// Called by Opsive animation event when swim enter animation completes.
        /// </summary>
        public void OnAnimatorSwimEnteredWater()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorSwimEnteredWater");
            
            // Queue event logic can be added here if ECS needs to react to exact animation timing
        }

        /// <summary>
        /// Called by Opsive animation event when swim exit animation completes.
        /// </summary>
        public void OnAnimatorSwimExitedWater()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorSwimExitedWater");
        }

        /// <summary>
        /// Called by Opsive animation event when dive adds force.
        /// </summary>
        public void OnAnimatorDiveAddForce()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorDiveAddForce");
        }

        /// <summary>
        /// Called by Opsive animation event when dive animation completes.
        /// </summary>
        public void OnAnimatorDiveComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorDiveComplete");
        }

        /// <summary>
        /// Called by Opsive animation event when climb from water completes.
        /// </summary>
        /*
        // METHOD NAME CONFLICT: Opsive uses "OnAnimatorClimbComplete" for both FreeClimb and ClimbFromWater?
        // Checking ClimbFromWater.cs source: 
        // EventHandler.RegisterEvent(m_GameObject, "OnAnimatorClimbComplete", OnClimbComplete);
        // Checking FreeClimb.cs source:
        // EventHandler.RegisterEvent(m_GameObject, "OnAnimatorFreeClimbComplete", OnFreeClimbComplete);
        
        // Wait, ClimbFromWater.cs uses "OnAnimatorClimbComplete"?! 
        // Verify against ClimbAnimatorBridge's existing methods.
        // There is no existing OnAnimatorClimbComplete. There is OnClimbStep (generic).
        // So this is safe to add.
        */
        public void OnAnimatorClimbComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorClimbComplete (Water)");
        }

        /// <summary>
        /// Called by Opsive animation event when drown animation completes.
        /// </summary>
        public void OnAnimatorDrownComplete()
        {
            if (DebugLogging || EnableDebugLog)
                Debug.Log("[ClimbAnimatorBridge] OnAnimatorDrownComplete");
        }
        #endregion
        
        
        #region IPlayerAnimationBridge Implementation


        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            if (animator == null) return;

            // [SWIM_DIAG]
            if (state.AbilityIndex == 301 || _lastAbilityIndex == 301)
            {
                if (state.AbilityIndex != _lastAbilityIndex)
                {
                    Debug.Log($"[SWIM_DIAG] Bridge: Ability Change Detected! Old={_lastAbilityIndex} New={state.AbilityIndex} IntData={state.AbilityIntData}");
                }
            }

            // Detect ability transitions using AbilityIndex (more reliable than IsClimbing bool)
            // AbilityIndex 503 = FreeClimb, 104 = Hang
            bool isClimbAbilityActive = state.AbilityIndex == 503 || state.AbilityIndex == 104;
            bool wasClimbAbilityActive = _lastAbilityIndex == 503 || _lastAbilityIndex == 104;

            bool justStartedClimbing = isClimbAbilityActive && !wasClimbAbilityActive;
            bool justStoppedClimbing = !isClimbAbilityActive && wasClimbAbilityActive;

            if (justStartedClimbing)
            {
                TriggerGrab();
            }
            else if (justStoppedClimbing)
            {
                TriggerRelease();
                DisableIK();
            }

            _wasClimbing = state.IsClimbing;
            // NOTE: _lastAbilityIndex is updated at END of method to allow change detection
            _lastProgress = state.ClimbProgress;
            
            // Calculate climb speed from progress delta
            float progressDelta = (state.ClimbProgress - _lastProgress) / Mathf.Max(deltaTime, 0.001f);
            _climbSpeedSmoothed = Mathf.Lerp(_climbSpeedSmoothed, progressDelta, 10f * deltaTime);

            // DIAGNOSTIC: Commented out - issue was IK jitter, not animator state
            // bool isIdleClimbing = state.AbilityIndex == 503 && 
            //                       Mathf.Abs(state.MoveInput.x) < 0.01f && 
            //                       Mathf.Abs(state.MoveInput.y) < 0.01f;
            // if (isIdleClimbing && Time.frameCount % 10 == 0) // Log every 10 frames
            // {
            //     var baseStateInfo = animator.GetCurrentAnimatorStateInfo(0);
            //     var fullBodyStateInfo = animator.IsInTransition(1) ? animator.GetNextAnimatorStateInfo(1) : animator.GetCurrentAnimatorStateInfo(1);
            //     bool baseInTransition = animator.IsInTransition(0);
            //     bool fullBodyInTransition = animator.IsInTransition(1);
            //     int actualAbilityIndex = h_AbilityIndex != 0 ? animator.GetInteger(h_AbilityIndex) : -1;
            //     int actualAbilityIntData = h_AbilityIntData != 0 ? animator.GetInteger(h_AbilityIntData) : -1;
            //     Debug.Log($"[CLIMB_DIAG] Frame={Time.frameCount} | BaseState={baseStateInfo.shortNameHash} InTrans={baseInTransition} | FullBodyState={fullBodyStateInfo.shortNameHash} InTrans={fullBodyInTransition} | ActualAbilIdx={actualAbilityIndex} ActualIntData={actualAbilityIntData}");
            // }

            // Set animator parameters - ONLY when changed to prevent state flicker
            if (h_IsClimbing != 0)
            {
                bool currentIsClimbing = animator.GetBool(h_IsClimbing);
                if (currentIsClimbing != state.IsClimbing)
                    animator.SetBool(h_IsClimbing, state.IsClimbing);
            }
            
            if (h_ClimbProgress != 0)
                animator.SetFloat(h_ClimbProgress, state.ClimbProgress);
            
            if (h_ClimbSpeed != 0)
                animator.SetFloat(h_ClimbSpeed, _climbSpeedSmoothed);

            if (h_Speed != 0)
                animator.SetFloat(h_Speed, state.MoveSpeed);

            if (h_Moving != 0)
            {
                // Opsive usually requires Moving=true for ability transitions
                // Use a small threshold to determine movement
                bool shouldSetMoving = state.MoveSpeed > 0.1f || math.lengthsq(state.MoveInput) > 0.01f;
                // Force Moving=true if we are in a state that requires it for transition (like Swim Entry might?)
                // But for now, just map it correctly.
                animator.SetBool(h_Moving, shouldSetMoving);
            }
            
            // 13.26: Opsive ability parameters

            // DO NOT set MovementSetID here!
            // MovementSetID is controlled EXCLUSIVELY by WeaponEquipVisualBridge based on equipped weapon type:
            // 0 = Guns (Assault Rifle, Shotgun, Pistol, etc.)
            // 1 = Melee (Katana, Knife)
            // 2 = Bow
            // Setting it to 2 here was breaking weapon animations!

            // Opsive Height logic for crouch
            if (h_Height != 0)
                animator.SetFloat(h_Height, state.Height);

            if (h_AbilityIndex != 0)
            {
                // Only set AbilityIndex when it actually changes to prevent animator flicker
                int currentAbilityIndex = animator.GetInteger(h_AbilityIndex);
                if (currentAbilityIndex != state.AbilityIndex)
                {
                    animator.SetInteger(h_AbilityIndex, state.AbilityIndex); // 814
                    
                    // Trigger AbilityChange to satisfy AnyState transition conditions
                    // This is REQUIRED for Opsive Animator transitions to work!
                    if (h_AbilityChange != 0) 
                    {
                        animator.SetTrigger(h_AbilityChange);
                    }
                }
            }

            // DEBOUNCE AbilityIntData to prevent rapid oscillation
            if (h_AbilityIntData != 0)
            {
                // Only apply IntData changes after they've been stable
                if (_pendingAbilityIntData != state.AbilityIntData)
                {
                    // New IntData detected - start counting stability frames
                    _pendingAbilityIntData = state.AbilityIntData;
                    _pendingIntDataStableFrames = 1;
                }
                else
                {
                    _pendingIntDataStableFrames++;
                }
                
                // Only update animator if stable long enough OR if it's a priority state
                // EPIC 14.24: Added 7 (Pull Up) and 10 (Hang Entry) to priority states
                bool isPriorityState = state.AbilityIntData == 6 ||   // FreeClimb Vault
                                       state.AbilityIntData == 7 ||   // Hang Pull Up
                                       state.AbilityIntData == 10;    // Hang Entry
                bool isStable = _pendingIntDataStableFrames >= REQUIRED_INTDATA_STABLE_FRAMES;
                
                if (isPriorityState || isStable)
                {
                    if (_lastAbilityIntData != _pendingAbilityIntData)
                    {
                        animator.SetInteger(h_AbilityIntData, _pendingAbilityIntData);
                        _lastAbilityIntData = _pendingAbilityIntData;
                    }
                }
            }

            if (h_AbilityFloatData != 0)
                animator.SetFloat(h_AbilityFloatData, state.AbilityFloatData);

            // CRITICAL FIX: Fire AbilityChange trigger based on LOCAL state tracking
            // Using debouncing to prevent flickering from prediction tick jitter
            bool rawAbilityChanged = _lastAbilityIndex != state.AbilityIndex && state.AbilityIndex != 0;
            
            // Debounce: require stable state for REQUIRED_STABLE_FRAMES before applying
            if (_pendingAbilityIndex != state.AbilityIndex)
            {
                // New ability detected - start counting stability frames
                // New ability detected - start counting stability frames
                // Debug.Log removed
                _pendingAbilityIndex = state.AbilityIndex;
                _pendingAbilityStableFrames = 1;
            }
            else
            {
                // Same ability as pending - increment stable frame count
                _pendingAbilityStableFrames++;
            }
            
            // Only fire trigger if ability has been stable long enough
            bool shouldFireTrigger = _pendingAbilityStableFrames >= REQUIRED_STABLE_FRAMES && 
                                     _lastAbilityIndex != _pendingAbilityIndex && 
                                     _pendingAbilityIndex != 0;
                                     
            if (shouldFireTrigger)
            {
                // FORCE SYNC: Ensure AbilityIntData is up to date before firing change
                // This prevents entering FreeClimb(503) with IntData=0 (which might cause immediate exit)
                if (_pendingAbilityIntData != _lastAbilityIntData)
                {
                    animator.SetInteger(h_AbilityIntData, _pendingAbilityIntData);
                    _lastAbilityIntData = _pendingAbilityIntData;
                }
            
                // Debug.Log($"[CLIMB_TRACE] FIRING AbilityChange! _lastAbilityIndex={_lastAbilityIndex} -> _pendingAbilityIndex={_pendingAbilityIndex}");
                if (h_AbilityChange != 0)
                    animator.SetTrigger(h_AbilityChange);
                    
                // Update tracking - this is now the confirmed stable ability
                _lastAbilityIndex = _pendingAbilityIndex;
            }
            else if (_pendingAbilityStableFrames >= REQUIRED_STABLE_FRAMES && _pendingAbilityIndex == 0)
            {
                // CRITICAL FIX: If we have stabilized at 0 (Locomotion), we MUST reset _lastAbilityIndex to 0.
                // Otherwise, if we try to enter the SAME ability again later (e.g. Roll 102 -> 0 -> Roll 102),
                // the check (_lastAbilityIndex != _pendingAbilityIndex) will fail because _lastAbilityIndex is still 102.
                if (_lastAbilityIndex != 0)
                {
                    _lastAbilityIndex = 0;
                    // Optional: Fire AbilityChange for return to locomotion if needed, but usually not required for exit
                    if (h_AbilityChange != 0)
                         animator.SetTrigger(h_AbilityChange);
                }
            }
            else if (_pendingAbilityStableFrames == 1 && _pendingAbilityIndex != _lastAbilityIndex)
            {
                // Just started waiting for stability
            }
            
            // FIX 4: STATE-BASED OVERRIDE - Bypass debounce when ECS is authoritative
            // If ECS says NOT climbing but bridge still thinks we ARE, force sync... BUT ONLY IF STABLE
            // This prevents single-frame dropouts from killing the climb state
            bool ecsNotClimbing = !state.IsClimbing && state.AbilityIndex != 503 && state.AbilityIndex != 104;
            bool bridgeThinksCimbing = _lastAbilityIndex == 503 || _lastAbilityIndex == 104;
            
            if (ecsNotClimbing && bridgeThinksCimbing)
            {
                // Verify this isn't just a flicker - confirm '0' is stable
                if (_pendingAbilityIndex == 0 && _pendingAbilityStableFrames >= REQUIRED_STABLE_FRAMES)
                {
                    if (h_AbilityIndex != 0)
                        animator.SetInteger(h_AbilityIndex, state.AbilityIndex); // Set to 0
                    if (h_AbilityChange != 0)
                        animator.SetTrigger(h_AbilityChange);
                    
                    _lastAbilityIndex = state.AbilityIndex;
                    // Reset pending to avoid re-triggering logic later
                    _pendingAbilityIndex = 0; 
                    _pendingAbilityStableFrames = 0;
                }
            }
            
            // Movement parameters for blend trees
            float horiz = state.MoveInput.x;
            float fwd = state.MoveInput.y;
            
            // SPRINT: Scale movement input by 2x to reach Run animation positions in blend tree
            // Blend tree has Walk at ±1 positions and Run at ±2 positions
            if (state.IsSprinting)
            {
                horiz *= 2f;
                fwd *= 2f;
            }

            if (h_HorizontalMovement != 0)
                animator.SetFloat(h_HorizontalMovement, horiz);
            
            if (h_ForwardMovement != 0)
                animator.SetFloat(h_ForwardMovement, fwd);
                
            // Moving = true if either speed > threshold OR input is present
            // This ensures locomotion can resume immediately after landing
            bool isMoving = state.MoveSpeed > 0.1f || 
                           (state.MoveInput.x * state.MoveInput.x + state.MoveInput.y * state.MoveInput.y) > 0.01f;
            if (h_Moving != 0)
            {
                bool currentMoving = animator.GetBool(h_Moving);
                if (currentMoving != isMoving)
                    animator.SetBool(h_Moving, isMoving);
            }

            // VERIFICATION DEBUG:
            if (state.IsSprinting)
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                int currIdx = h_AbilityIndex != 0 ? animator.GetInteger(h_AbilityIndex) : -1;
                int currInt = h_AbilityIntData != 0 ? animator.GetInteger(h_AbilityIntData) : -1;
                int currSet = h_MovementSetID != 0 ? animator.GetInteger(h_MovementSetID) : -1;
                bool currMov = h_Moving != 0 && animator.GetBool(h_Moving);
                float currFwd = h_ForwardMovement != 0 ? animator.GetFloat(h_ForwardMovement) : -99f;
                
                // Force log every 10 frames to capture functionality without spamming too hard, but enough to see the freeze state
                if (Time.frameCount % 10 == 0)
                {

                }
            }
        }

        public void TriggerLanding()
        {
            // No-op for climb bridge - handled by LandingAnimatorBridge
        }
        #endregion
    }
}
