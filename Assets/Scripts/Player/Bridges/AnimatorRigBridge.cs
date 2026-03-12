using UnityEngine;
using Unity.Mathematics;
using Player.Systems;

namespace Player.Bridges
{
    /// <summary>
    /// Client-side bridge: drive Animator parameters and Animation Rigging weights from the hybrid input/state.
    /// Keeps DOTS authoritative for gameplay state while making visuals/IK responsive on the client.
    /// Intended to be attached to the player GameObject prefab used for local play.
    /// Uses `PlayerInputState` for immediate input-driven visuals and exposes public parameter names
    /// so designers can map Animator parameters without code changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimatorRigBridge : UnityEngine.MonoBehaviour, IPlayerAnimationBridge
    {
        [Header("References")]
        [SerializeField] Animator animator;
        [Tooltip("Optional rig(s) to control weight for IK/constraints (e.g., hand/foot rigs). If Animation Rigging package is not installed, assign the Rig GameObjects here and the bridge will attempt to set a 'weight' property if present.")]
        public Component[] Rigs;

        [Header("Parameter Mapping")]
        [SerializeField] string ParamMoveX = "MoveX";
        [SerializeField] string ParamMoveY = "MoveY";
        [SerializeField] string ParamSpeed = "Speed";
        [SerializeField] string ParamIsCrouch = "IsCrouch";
        [SerializeField] string ParamIsProne = "IsProne";
        [SerializeField] string ParamIsSprint = "IsSprint";
        [SerializeField] string ParamIsGrounded = "IsGrounded";
        [SerializeField] string ParamIsJumping = "IsJumping";
        [SerializeField] string ParamLean = "Lean"; // -1..1
        [SerializeField] string ParamIsSliding = "IsSliding";
        [SerializeField] string ParamVerticalSpeed = "VerticalSpeed";
        [SerializeField] string ParamLanding = "LandingTrigger";

        [Header("Swimming Parameters")]
        [SerializeField] string ParamIsSwimming = "IsSwimming";
        [SerializeField] string ParamIsUnderwater = "IsUnderwater";
        [SerializeField] string ParamSwimActionState = "SwimActionState";
        [SerializeField] string ParamSwimInputMagnitude = "SwimInputMagnitude";
        
        [Header("Locomotion Parameters")]
        [SerializeField] string ParamHorizontalMovement = "HorizontalMovement";
        [SerializeField] string ParamForwardMovement = "ForwardMovement";
        [SerializeField] string ParamMoving = "Moving";
        [Tooltip("Height parameter: 0=standing, 1=crouching, 2=prone")]
        [SerializeField] string ParamHeight = "Height";
        [SerializeField] string ParamMovementSetID = "MovementSetID";
        
        [Header("Ability Parameters")]
        [SerializeField] string ParamAbilityIndex = "AbilityIndex";
        [SerializeField] string ParamAbilityChange = "AbilityChange";
        [SerializeField] string ParamAbilityIntData = "AbilityIntData";
        
        [Header("Turn Animation Parameters")]
        [Tooltip("Yaw parameter for turn-in-place animations (-1=left, 0=none, 1=right)")]
        [SerializeField] string ParamYaw = "Yaw";

        // runtime cached hashes
        int h_MoveX;
        int h_MoveY;
        int h_Speed;
        int h_IsCrouch;
        int h_IsProne;
        int h_IsSprint;
        int h_IsGrounded;
        int h_IsJumping;
        int h_Lean;
        int h_IsSliding;
        int h_VerticalSpeed;
        int h_Landing;
        int h_IsSwimming;
        int h_IsUnderwater;
        int h_SwimActionState;
        int h_SwimInputMagnitude;
        
        // 13.26: Opsive locomotion hashes
        int h_HorizontalMovement;
        int h_ForwardMovement;
        int h_Moving;
        int h_Height;
        int h_MovementSetID;
        
        // Opsive ability system hashes
        int h_AbilityIndex;
        int h_AbilityChange;
        int h_AbilityIntData;
        
        // EPIC 15.20: Turn animation hash
        int h_Yaw;

        [Header("Rig Tuning")]
        [Tooltip("When true, rigs will be driven proportional to absolute Lean value.")]
        public bool DriveRigsByLean = true;
        [Range(0f,1f)]
        public float RigMaxWeight = 1f;

        [Header("Debug / Standalone Testing")]
        [Tooltip("When true, this MonoBehaviour will keep reading PlayerInputState so dropping the prefab into a non-DOTS scene still animates.")]
        public bool UseLegacyInputFallback = false;

        [Tooltip("Enable debug logging from this bridge (recommended false in builds)")]
        public bool EnableDebugLog = false;

        // internal smoothing
        float leanSmooth = 0f;

        // Cached values to re-apply in LateUpdate (after Opsive AnimatorMonitor runs)
        private float _cachedHorizontalMovement;
        private float _cachedForwardMovement;
        private float _cachedSpeed;
        private bool _hasCachedMovementValues;
        const float LeanSmoothSpeed = 8f;

        [Header("Movement Damping")]
        [Tooltip("Damping time for HorizontalMovement/ForwardMovement blend tree parameters. " +
                 "Higher = smoother but less responsive. 0.1-0.15 recommended.")]
        [Range(0f, 0.5f)]
        [SerializeField] float movementDampTime = 0.1f;
        [Tooltip("Damping time for Speed parameter (Idle/Walk/Run/Sprint transitions).")]
        [Range(0f, 0.5f)]
        [SerializeField] float speedDampTime = 0.15f;

        void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            CacheHashes();
        }

        void Awake()
        {
            // Force debug logging off in builds/playmode to prevent spam
            EnableDebugLog = false;
        }

        void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            CacheHashes();
            
            // CRITICAL: Disable Opsive's AnimatorMonitor to prevent it from overwriting our movement values
            // Opsive uses raw input (-1 to 1) while we need scaled values (2x for Run animations)
            DisableOpsiveAnimatorMonitor();
            
            // Debug: Log animator controller status
            if (animator != null)
            {
                string ctrlName = animator.runtimeAnimatorController != null 
                    ? animator.runtimeAnimatorController.name 
                    : "NULL - NO CONTROLLER!";
                Debug.Log($"[AnimatorRigBridge] Animator on '{animator.gameObject.name}' Controller={ctrlName} Layers={animator.layerCount} ParamCount={animator.parameterCount}");
            }
            else
            {
                Debug.LogError($"[AnimatorRigBridge] NO ANIMATOR FOUND on {name}!");
            }
        }
        
        /// <summary>
        /// Disables Opsive's AnimatorMonitor and ChildAnimatorMonitor components to prevent them from overwriting our movement parameters.
        /// The AnimatorMonitor sets HorizontalMovement/ForwardMovement to raw input (-1 to 1),
        /// which prevents our 2x scaling for Run animations from working.
        /// </summary>
        void DisableOpsiveAnimatorMonitor()
        {
            // Find the Opsive AnimatorMonitor on the same GameObject as our Animator
            if (animator == null) return;
            
            // Disable AnimatorMonitor
            var opsiveMonitor = animator.GetComponent<Opsive.UltimateCharacterController.Character.AnimatorMonitor>();
            if (opsiveMonitor != null)
            {
                opsiveMonitor.enabled = false;
                Debug.Log($"[AnimatorRigBridge] DISABLED Opsive AnimatorMonitor on '{animator.gameObject.name}'");
            }
            
            // Also disable ChildAnimatorMonitor if present
            var childMonitor = animator.GetComponent<Opsive.UltimateCharacterController.Character.ChildAnimatorMonitor>();
            if (childMonitor != null)
            {
                childMonitor.enabled = false;
                Debug.Log($"[AnimatorRigBridge] DISABLED Opsive ChildAnimatorMonitor on '{animator.gameObject.name}'");
            }
            
            if (opsiveMonitor == null && childMonitor == null)
            {
                Debug.Log($"[AnimatorRigBridge] No Opsive AnimatorMonitor found on '{animator.gameObject.name}'");
            }
        }

        void OnValidate()
        {
            CacheHashes();
        }
        
        /// <summary>
        /// LateUpdate runs AFTER Opsive's AnimatorMonitor, ensuring our movement values persist.
        /// Opsive AnimatorMonitor subscribes to CharacterLocomotion.OnAnimationUpdate and sets
        /// HorizontalMovement/ForwardMovement to raw input (-1 to 1), overwriting our scaled values.
        /// We cache our values in ApplyAnimationState and re-apply them here.
        /// </summary>
        void LateUpdate()
        {
            if (animator == null || !_hasCachedMovementValues) return;
            
            // DEBUG: Log BEFORE re-apply to detect if Opsive overwrote our values
            float beforeH = 0, beforeF = 0, beforeS = 0;
            if (EnableDebugLog && (Mathf.Abs(_cachedHorizontalMovement) > 0.01f || Mathf.Abs(_cachedForwardMovement) > 0.01f))
            {
                beforeH = animator.GetFloat(h_HorizontalMovement);
                beforeF = animator.GetFloat(h_ForwardMovement);
                beforeS = animator.GetFloat(h_Speed);
                bool opsiveOverwrote = Mathf.Abs(beforeH - _cachedHorizontalMovement) > 0.01f || Mathf.Abs(beforeF - _cachedForwardMovement) > 0.01f;
                Debug.Log($"[SPRINT_DEBUG] LateUpdate BEFORE: H={beforeH:F2}, F={beforeF:F2}, Speed={beforeS:F1} | OpsiveOverwrote={opsiveOverwrote} | OurCached=({_cachedHorizontalMovement:F2},{_cachedForwardMovement:F2})");
            }
            
            // Re-apply our cached movement values after Opsive AnimatorMonitor has run
            // Use dampTime to maintain smooth blending (LateUpdate deltaTime)
            float dt = Time.deltaTime;
            if (h_HorizontalMovement != 0) animator.SetFloat(h_HorizontalMovement, _cachedHorizontalMovement, movementDampTime, dt);
            if (h_ForwardMovement != 0) animator.SetFloat(h_ForwardMovement, _cachedForwardMovement, movementDampTime, dt);
            if (h_Speed != 0) animator.SetFloat(h_Speed, _cachedSpeed, speedDampTime, dt);
            
            // DEBUG: Log AFTER re-apply and show current animator state
            if (EnableDebugLog && (Mathf.Abs(_cachedHorizontalMovement) > 0.01f || Mathf.Abs(_cachedForwardMovement) > 0.01f))
            {
                float afterH = animator.GetFloat(h_HorizontalMovement);
                float afterF = animator.GetFloat(h_ForwardMovement);
                float afterS = animator.GetFloat(h_Speed);
                
                // Get current animator state info for layer 0 (base layer with locomotion)
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
                string clipNames = clipInfos.Length > 0 ? string.Join(",", System.Array.ConvertAll(clipInfos, c => $"{c.clip.name}({c.weight:F2})")) : "none";
                
                Debug.Log($"[SPRINT_DEBUG] LateUpdate AFTER: H={afterH:F2}, F={afterF:F2}, Speed={afterS:F1} | Layer0Clips=[{clipNames}] | StateHash={stateInfo.shortNameHash}");
            }
        }

        void CacheHashes()
        {
            h_MoveX = !string.IsNullOrEmpty(ParamMoveX) ? Animator.StringToHash(ParamMoveX) : 0;
            h_MoveY = !string.IsNullOrEmpty(ParamMoveY) ? Animator.StringToHash(ParamMoveY) : 0;
            h_Speed = !string.IsNullOrEmpty(ParamSpeed) ? Animator.StringToHash(ParamSpeed) : 0;
            h_IsCrouch = !string.IsNullOrEmpty(ParamIsCrouch) ? Animator.StringToHash(ParamIsCrouch) : 0;
            h_IsProne = !string.IsNullOrEmpty(ParamIsProne) ? Animator.StringToHash(ParamIsProne) : 0;
            h_IsSprint = !string.IsNullOrEmpty(ParamIsSprint) ? Animator.StringToHash(ParamIsSprint) : 0;
            h_IsGrounded = !string.IsNullOrEmpty(ParamIsGrounded) ? Animator.StringToHash(ParamIsGrounded) : 0;
            h_IsJumping = !string.IsNullOrEmpty(ParamIsJumping) ? Animator.StringToHash(ParamIsJumping) : 0;
            h_Lean = !string.IsNullOrEmpty(ParamLean) ? Animator.StringToHash(ParamLean) : 0;
            h_IsSliding = !string.IsNullOrEmpty(ParamIsSliding) ? Animator.StringToHash(ParamIsSliding) : 0;
            h_VerticalSpeed = !string.IsNullOrEmpty(ParamVerticalSpeed) ? Animator.StringToHash(ParamVerticalSpeed) : 0;
            h_Landing = !string.IsNullOrEmpty(ParamLanding) ? Animator.StringToHash(ParamLanding) : 0;
            h_IsSwimming = !string.IsNullOrEmpty(ParamIsSwimming) ? Animator.StringToHash(ParamIsSwimming) : 0;
            h_IsUnderwater = !string.IsNullOrEmpty(ParamIsUnderwater) ? Animator.StringToHash(ParamIsUnderwater) : 0;
            h_SwimActionState = !string.IsNullOrEmpty(ParamSwimActionState) ? Animator.StringToHash(ParamSwimActionState) : 0;
            h_SwimInputMagnitude = !string.IsNullOrEmpty(ParamSwimInputMagnitude) ? Animator.StringToHash(ParamSwimInputMagnitude) : 0;
            
            // 13.26: Opsive locomotion hashes
            h_HorizontalMovement = !string.IsNullOrEmpty(ParamHorizontalMovement) ? Animator.StringToHash(ParamHorizontalMovement) : 0;
            h_ForwardMovement = !string.IsNullOrEmpty(ParamForwardMovement) ? Animator.StringToHash(ParamForwardMovement) : 0;
            h_Moving = !string.IsNullOrEmpty(ParamMoving) ? Animator.StringToHash(ParamMoving) : 0;
            h_Height = !string.IsNullOrEmpty(ParamHeight) ? Animator.StringToHash(ParamHeight) : 0;
            h_MovementSetID = !string.IsNullOrEmpty(ParamMovementSetID) ? Animator.StringToHash(ParamMovementSetID) : 0;
            
            // Opsive ability system hashes
            h_AbilityIndex = !string.IsNullOrEmpty(ParamAbilityIndex) ? Animator.StringToHash(ParamAbilityIndex) : 0;
            h_AbilityChange = !string.IsNullOrEmpty(ParamAbilityChange) ? Animator.StringToHash(ParamAbilityChange) : 0;
            h_AbilityIntData = !string.IsNullOrEmpty(ParamAbilityIntData) ? Animator.StringToHash(ParamAbilityIntData) : 0;
            
            // EPIC 15.20: Turn animation hash
            h_Yaw = !string.IsNullOrEmpty(ParamYaw) ? Animator.StringToHash(ParamYaw) : 0;

            // Validate against actual controller parameters to avoid "Parameter does not exist" errors
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var validHashes = new System.Collections.Generic.HashSet<int>();
                foreach (var param in animator.parameters)
                {
                    validHashes.Add(param.nameHash);
                }

                if (!validHashes.Contains(h_MoveX)) h_MoveX = 0;
                if (!validHashes.Contains(h_MoveY)) h_MoveY = 0;
                if (!validHashes.Contains(h_Speed)) h_Speed = 0;
                if (!validHashes.Contains(h_IsCrouch)) h_IsCrouch = 0;
                if (!validHashes.Contains(h_IsProne)) h_IsProne = 0;
                if (!validHashes.Contains(h_IsSprint)) h_IsSprint = 0;
                if (!validHashes.Contains(h_IsGrounded)) h_IsGrounded = 0;
                if (!validHashes.Contains(h_IsJumping)) h_IsJumping = 0;
                if (!validHashes.Contains(h_Lean)) h_Lean = 0;
                if (!validHashes.Contains(h_IsSliding)) h_IsSliding = 0;
                if (!validHashes.Contains(h_VerticalSpeed)) h_VerticalSpeed = 0;
                if (!validHashes.Contains(h_Landing)) h_Landing = 0;
                if (!validHashes.Contains(h_IsSwimming)) h_IsSwimming = 0;
                if (!validHashes.Contains(h_IsUnderwater)) h_IsUnderwater = 0;
                if (!validHashes.Contains(h_SwimActionState)) h_SwimActionState = 0;
                if (!validHashes.Contains(h_SwimInputMagnitude)) h_SwimInputMagnitude = 0;
                
                // 13.26: Opsive locomotion validation
                if (!validHashes.Contains(h_HorizontalMovement)) h_HorizontalMovement = 0;
                if (!validHashes.Contains(h_ForwardMovement)) h_ForwardMovement = 0;
                if (!validHashes.Contains(h_Moving)) h_Moving = 0;
                if (!validHashes.Contains(h_Height)) h_Height = 0;
                if (!validHashes.Contains(h_MovementSetID)) h_MovementSetID = 0;
                if (!validHashes.Contains(h_Height)) h_Height = 0;
                
                // Opsive ability system validation
                if (!validHashes.Contains(h_AbilityIndex)) h_AbilityIndex = 0;
                if (!validHashes.Contains(h_AbilityChange)) h_AbilityChange = 0;
                if (!validHashes.Contains(h_AbilityIntData)) h_AbilityIntData = 0;
                
                // EPIC 15.20: Turn animation validation
                if (!validHashes.Contains(h_Yaw)) h_Yaw = 0;
            }
        }

        void Update()
        {
            if (!UseLegacyInputFallback)
                return;

            var legacyState = new PlayerAnimationState
            {
                MoveInput = PlayerInputState.Move,
                MoveSpeed = math.length(PlayerInputState.Move),
                Lean = PlayerInputState.LeanRight ? 1f : (PlayerInputState.LeanLeft ? -1f : 0f),
                MovementState = PlayerMovementState.Running,
                IsGrounded = true,
                IsCrouching = PlayerInputState.Crouch,
                IsSprinting = PlayerInputState.Sprint,
                IsSliding = PlayerInputState.Sprint && PlayerInputState.Crouch && math.length(PlayerInputState.Move) > 0.1f
            };

            ApplyAnimationState(legacyState, Time.deltaTime);
        }

        /// <summary>
        /// Apply DOTS-driven animation parameters. Called from the PlayerAnimatorBridgeSystem once per frame on the client.
        /// </summary>
        public void ApplyAnimationState(PlayerAnimationState state, float deltaTime)
        {
            if (animator == null) return;

            // Movement params
            if (h_MoveX != 0) animator.SetFloat(h_MoveX, state.MoveInput.x);
            if (h_MoveY != 0) animator.SetFloat(h_MoveY, state.MoveInput.y);
            
            // --- MOVEMENT ---
            
            // Calculate input magnitude for responsive animation
            float inputMagnitude = Mathf.Sqrt(state.MoveInput.x * state.MoveInput.x + state.MoveInput.y * state.MoveInput.y);
            bool hasMovementInput = inputMagnitude > 0.1f;
            
            // Calculate Opsive Speed parameter (0=Idle, 1=Walk, 2=Run, 3=Sprint)
            // Use INPUT magnitude for responsiveness, not velocity (which lags behind)
            float opsiveSpeed = 0f;
            if (hasMovementInput)
            {
                opsiveSpeed = 1f; // Walk
                if (state.IsSprinting) opsiveSpeed = 3f; // Sprint
            }
            if (h_Speed != 0) animator.SetFloat(h_Speed, opsiveSpeed, speedDampTime, deltaTime);
            
            // DEBUG: Log sprint state issues
            if (EnableDebugLog && hasMovementInput)
            {
                Debug.Log($"[SPRINT_DEBUG] ApplyAnimationState: IsSprinting={state.IsSprinting}, Speed={opsiveSpeed}, h_Speed={h_Speed}, MoveInput=({state.MoveInput.x:F2}, {state.MoveInput.y:F2}), MovementState={state.MovementState}");
            }

            // Set Sprint boolean if supported (Opsive Demo Controller doesn't use this, but Warrok might)
            if (h_IsSprint != 0) animator.SetBool(h_IsSprint, state.IsSprinting);
            
            // Crouch / Prone
            if (h_IsCrouch != 0) animator.SetBool(h_IsCrouch, state.IsCrouching);
            if (h_IsProne != 0) animator.SetBool(h_IsProne, state.IsProne);
            
            // Debug: Log sprint parameters when sprinting
            // Log removed
            
            // Ground / Jump state
            if (h_IsGrounded != 0) animator.SetBool(h_IsGrounded, state.IsGrounded);
            if (h_IsJumping != 0) animator.SetBool(h_IsJumping, state.IsJumping);
            if (h_VerticalSpeed != 0) animator.SetFloat(h_VerticalSpeed, state.VerticalSpeed);
            
            // EPIC 15.20: Turn-in-place animation (tank turn)
            if (h_Yaw != 0) animator.SetFloat(h_Yaw, state.Yaw);
            
            // Debug log when not grounded
            // Log removed
            
            // Debug log when prone
            // Log removed

            // Lean smoothing keeps bridge behaviour consistent with legacy mono flow
            leanSmooth = Mathf.MoveTowards(leanSmooth, state.Lean, LeanSmoothSpeed * deltaTime);
            if (h_Lean != 0) animator.SetFloat(h_Lean, leanSmooth);

            // Drive rigs if configured
            if (DriveRigsByLean && Rigs != null && Rigs.Length > 0)
            {
                float w = math.abs(leanSmooth) * RigMaxWeight;
                for (int i = 0; i < Rigs.Length; i++)
                {
                    var r = Rigs[i];
                    if (r == null) continue;

                    var t = r.GetType();
                    var prop = t.GetProperty("weight");
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(float))
                    {
                        prop.SetValue(r, w, null);
                        continue;
                    }

                    var field = t.GetField("weight");
                    if (field != null && field.FieldType == typeof(float))
                    {
                        field.SetValue(r, w);
                    }
                }
            }

            if (h_IsSliding != 0)
            {
                animator.SetBool(h_IsSliding, state.IsSliding);
                // Log removed
            }

            // Swimming animation parameters
            if (h_IsSwimming != 0) animator.SetBool(h_IsSwimming, state.IsSwimming);
            if (h_IsUnderwater != 0) animator.SetBool(h_IsUnderwater, state.IsUnderwater);
            if (h_SwimActionState != 0) animator.SetInteger(h_SwimActionState, state.SwimActionState);
            if (h_SwimInputMagnitude != 0) animator.SetFloat(h_SwimInputMagnitude, state.SwimInputMagnitude);

            // Log removed
            
            // 13.26: Opsive locomotion parameters for blend trees
            // The blend tree uses position magnitude to determine Walk vs Run:
            // - Walk animations are at positions ~1 (e.g., WalkFwd at Y=1)
            // - Run animations are at positions ~2 (e.g., RunFwd at Y=2)
            // So we need to SCALE the input values when sprinting to reach Run positions
            float horiz = state.MoveInput.x;
            float fwd = state.MoveInput.y;
            
            // Scale to 2x when sprinting to reach Run animation positions in blend tree
            if (state.IsSprinting)
            {
                horiz *= 2f;
                fwd *= 2f;
            }
            
            // DEBUG: Log the final values being sent to animator
            if (EnableDebugLog && (Mathf.Abs(horiz) > 0.01f || Mathf.Abs(fwd) > 0.01f))
            {
                Debug.Log($"[SPRINT_DEBUG] BeforeSet: IsSprinting={state.IsSprinting}, horiz={horiz:F2}, fwd={fwd:F2}, h_Horiz={h_HorizontalMovement}, h_Fwd={h_ForwardMovement}, isClimbing={state.AbilityIndex}");
            }
            
            // CONFLICT FIX: Skip setting movement params when climbing - ClimbAnimatorBridge handles these
            bool isClimbingAbility = state.AbilityIndex == 503 || state.AbilityIndex == 104;
            
            if (!isClimbingAbility)
            {
                if (h_HorizontalMovement != 0) animator.SetFloat(h_HorizontalMovement, horiz, movementDampTime, deltaTime);
                if (h_ForwardMovement != 0) animator.SetFloat(h_ForwardMovement, fwd, movementDampTime, deltaTime);
                // Use input magnitude for Moving, not velocity (for responsive animation)
                float inputMag = Mathf.Sqrt(horiz * horiz + fwd * fwd);
                if (h_Moving != 0) animator.SetBool(h_Moving, inputMag > 0.1f);
                
                // Cache values to re-apply in LateUpdate after Opsive AnimatorMonitor runs
                _cachedHorizontalMovement = horiz;
                _cachedForwardMovement = fwd;
                _cachedSpeed = opsiveSpeed;
                _hasCachedMovementValues = true;
                
                // DEBUG: Verify what actually got set
                if (EnableDebugLog && (Mathf.Abs(horiz) > 0.01f || Mathf.Abs(fwd) > 0.01f))
                {
                    float actualH = animator.GetFloat(h_HorizontalMovement);
                    float actualF = animator.GetFloat(h_ForwardMovement);
                    float actualS = animator.GetFloat(h_Speed);
                    Debug.Log($"[SPRINT_DEBUG] AfterSet: actualH={actualH:F2}, actualF={actualF:F2}, actualSpeed={actualS:F1}, cached=({_cachedHorizontalMovement:F2},{_cachedForwardMovement:F2},{_cachedSpeed:F1})");
                }
            }
            
            // Opsive Height parameter: 0=standing, 1=crouching, 2=prone
            // This drives Opsive's HeightChange ability blend trees
            if (h_Height != 0) animator.SetFloat(h_Height, state.Height);
            
            // DO NOT set MovementSetID here!
            // MovementSetID is controlled EXCLUSIVELY by WeaponEquipVisualBridge based on equipped weapon type:
            // 0 = Guns (Assault Rifle, Shotgun, Pistol, etc.)
            // 1 = Melee (Katana, Knife)
            // 2 = Bow
            // Setting it to 2 here was breaking weapon animations because the animator thought we always had a Bow!

            // SINGLE SOURCE OF TRUTH: AbilityIndex/IntData/Change now handled EXCLUSIVELY by ClimbAnimatorBridge
            // This prevents conflicts where both bridges overwrite each other's values
            // if (h_AbilityIndex != 0) animator.SetInteger(h_AbilityIndex, state.AbilityIndex);
            // if (h_AbilityIntData != 0) animator.SetInteger(h_AbilityIntData, state.AbilityIntData);
            // if (h_AbilityChange != 0 && state.AbilityChange) animator.SetTrigger(h_AbilityChange);
            
            // Debug: Log ALL Opsive locomotion params when enabled
            // Debug logging removed
        }

        /// <summary>
        /// Trigger landing animation (call from AnimatorEventBridge.OnLanding or a DOTS adapter).
        /// </summary>
        public void TriggerLanding()
        {
            if (animator == null) return;
            if (h_Landing == 0) return;
            animator.SetTrigger(h_Landing);
        }

        /// <summary>
        /// Stub to catch Opsive Animation Events (e.g., 'ExecuteEvent') triggered by animation clips.
        /// Prevents "AnimationEvent has no receiver" errors.
        /// </summary>
        public void ExecuteEvent(string eventName)
        {
            // Intentionally empty stub.
            // Opsive animations trigger this for footstep sounds, item usage, etc.
            // We can dispatch these to a sound system later if needed.
        }
    }
}
