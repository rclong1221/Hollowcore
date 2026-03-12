using UnityEngine;
using Unity.Mathematics;
using Player.Systems;

namespace Player.Animation
{
    /// <summary>
    /// Client-side ragdoll presentation for death state (EPIC 10.17 Part B).
    /// Attached to the Warrok_Client presentation GameObject.
    /// 
    /// This MonoBehaviour is DRIVEN BY RagdollPresentationSystem (ECS).
    /// When death state changes, the system calls UpdateRagdollState().
    /// 
    /// On ragdoll: Disables Animator, enables physics on visual bones.
    /// After settling: Sends final position to server via RPC.
    /// On revival: Disables ragdoll physics, re-enables Animator.
    /// </summary>
    public class RagdollPresentationBridge : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator component to disable during ragdoll")]
        public Animator PlayerAnimator;
        
        [Tooltip("Root bone of the ragdoll (e.g., Hips/Pelvis)")]
        public Transform RagdollRoot;
        
        [Header("Settings")]
        [Tooltip("Force to apply when entering ragdoll (for dramatic effect)")]
        public float DeathImpulseForce = 0f; // Disabled by default
        
        [Tooltip("Velocity threshold below which ragdoll is considered 'settled'")]
        public float SettleVelocityThreshold = 0.1f;
        
        [Tooltip("Time the ragdoll must be below velocity threshold to be considered settled")]
        public float SettleTimeRequired = 1.5f;
        
        [Header("Debug")]
        // public bool DebugLogging = true; // Use Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled instead
        
        // Cached ragdoll bones (found at Awake)
        private Rigidbody[] _ragdollBodies;
        private Collider[] _ragdollColliders;
        private Rigidbody _hipsRigidbody; // Primary body to track for settling
        
        // Main player collider (capsule) - must be disabled during ragdoll to prevent collision
        private Collider _mainCollider;
        private CharacterController _characterController; // Also needs to be disabled
        private Player.Controllers.KinematicCharacterController _kinematicCharacterController; // Client-side physics - causes ragdoll launch via ResolveOverlaps
        
        // State tracking
        private bool _isRagdolled = false;
        private bool _hasSettled = false;
        private float _settleTimer = 0f;
        
        // Delayed collider activation - prevents overlap impulse on ragdoll start
        private bool _collidersEnabled = false;
        private int _colliderEnableDelayFrames = 0;
        private const int COLLIDER_ENABLE_DELAY = 2; // Wait 2 physics frames before enabling colliders
        
        // Velocity clamping - prevents explosive launches while allowing full physics interactions
        private const float MAX_RAGDOLL_VELOCITY = 5f; // Max m/s any bone can travel
        
        // For detaching ragdoll from presentation sync
        private Transform _originalParent;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;

        // ===== SERVER SYNC DATA (EPIC 13.19 - for non-owned players) =====
        private bool _hasRemoteSyncData = false;
        private Vector3 _remoteSyncPosition;
        private Quaternion _remoteSyncRotation;
        private Vector3 _remoteSyncVelocity;
        
        // ===== PUSH DETECTION (EPIC 13.19 - for sending push RPCs) =====
        private int _ownerGhostId = -1;  // GhostId of the player who owns this ragdoll
        private const float MIN_PUSH_IMPULSE = 50f;  // Minimum impulse to trigger push sync
        
        /// <summary>
        /// Public accessor for ragdoll state - used by camera to know when to follow ragdoll.
        /// </summary>
        public bool IsRagdolled => _isRagdolled;
        
        /// <summary>
        /// Position to follow during ragdoll (hips position).
        /// </summary>
        public Vector3 RagdollPosition => RagdollRoot != null ? RagdollRoot.position : transform.position;

        /// <summary>
        /// Called by RagdollHipsSyncReaderSystem with server-authoritative position (EPIC 13.19).
        /// Only used for non-owned players (remote ragdolls).
        /// </summary>
        public void SetRemoteSyncData(Vector3 position, Quaternion rotation, Vector3 velocity, bool isActive, int ghostId)
        {
            bool wasActive = _hasRemoteSyncData;
            _hasRemoteSyncData = isActive;
            _remoteSyncPosition = position;
            _remoteSyncRotation = rotation;
            _remoteSyncVelocity = velocity;
            _ownerGhostId = ghostId;

            // Log first sync data received
            if (isActive && !wasActive && LogEnabled)
            {
                Debug.Log($"[RagdollSync:Bridge] [ID:{GetInstanceID()}] '{gameObject.name}' received FIRST sync data: pos={position}, vel={velocity}, RagdollRoot={(RagdollRoot != null ? RagdollRoot.name : "NULL")}");
            }
        }
        
        /// <summary>
        /// Overload for backward compatibility.
        /// </summary>
        public void SetRemoteSyncData(Vector3 position, Quaternion rotation, bool isActive)
        {
            SetRemoteSyncData(position, rotation, Vector3.zero, isActive, -1);
        }

        /// <summary>
        /// Shortcut for RagdollSettleClientSystem.DiagnosticsEnabled
        /// </summary>
        private static bool LogEnabled => Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled;
        
        private void Awake()
        {
            // Find Animator if not assigned
            if (PlayerAnimator == null)
            {
                PlayerAnimator = GetComponentInChildren<Animator>();
            }
            
            // Find ragdoll root if not assigned
            if (RagdollRoot == null)
            {
                // Try common bone names
                RagdollRoot = transform.Find("Armature/Hips") ?? 
                              transform.Find("Hips") ?? 
                              transform.Find("Armature/Pelvis") ??
                              transform.Find("Pelvis");
            }
            
            // Cache ragdoll components
            if (RagdollRoot != null)
            {
                _ragdollBodies = RagdollRoot.GetComponentsInChildren<Rigidbody>();
                _ragdollColliders = RagdollRoot.GetComponentsInChildren<Collider>();
                
                // Cache hips rigidbody for velocity tracking
                _hipsRigidbody = RagdollRoot.GetComponent<Rigidbody>();
                
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                {
                    Debug.Log($"[RagdollPresentation] Found {_ragdollBodies.Length} ragdoll bodies on {gameObject.name}");
                }
                
                // CRITICAL: Prevent bone-to-bone collisions that cause explosive separation
                // All ragdoll bones start overlapping each other - Unity pushes them apart violently
                SetupBoneCollisionIgnore();
                
                // Start with ragdoll disabled (kinematic)
                SetRagdollEnabled(false);
            }
            else if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                Debug.LogWarning($"[RagdollPresentation] No RagdollRoot found on {gameObject.name}. Ragdoll will not work.");
            }
            
            // Cache main player collider (typically a CapsuleCollider on the root)
            _mainCollider = GetComponent<CapsuleCollider>();
            if (_mainCollider == null)
            {
                _mainCollider = GetComponent<Collider>(); // Fallback to any collider
            }
            
            // Cache CharacterController - also causes physics interference
            _characterController = GetComponent<CharacterController>();
            
            // Cache KinematicCharacterController - THIS IS THE MAIN CULPRIT!
            // Its ResolveOverlaps() method uses Physics.ComputePenetration which explosively separates ragdoll bones
            _kinematicCharacterController = GetComponent<Player.Controllers.KinematicCharacterController>();
        }
        
        private void OnDestroy()
        {
            if (_isRagdolled)
            {
                // Instance may be null during shutdown
                Player.PhysicsSimulation.RagdollPhysicsSimulator.Instance?.UnregisterRagdoll(GetInstanceID());
            }
        }
        
        /// <summary>
        /// Detect when another player pushes this ragdoll and send RPC to server.
        /// Only the player who CAN see the ragdoll should send push events to avoid duplicates.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            // Only process if ragdolled and we are NOT the owner (owner's pushes are handled locally)
            if (!_isRagdolled || _isOwned || _ownerGhostId < 0) return;
            
            // Check if the collision has significant impulse
            Vector3 impulse = collision.impulse;
            float impulseMag = impulse.magnitude;
            
            if (impulseMag < MIN_PUSH_IMPULSE) return;
            
            // Check if the pusher is a player character (has CharacterController or KCC)
            var pusherCC = collision.gameObject.GetComponentInParent<CharacterController>();
            var pusherKCC = collision.gameObject.GetComponentInParent<Player.Controllers.KinematicCharacterController>();
            
            if (pusherCC == null && pusherKCC == null) return;
            
            // Queue push event for ECS system to send to server
            Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
            Player.Systems.RagdollPushClientSystem.QueuePush(_ownerGhostId, contactPoint, impulse);
            
            if (LogEnabled)
            {
                Debug.Log($"[RagdollSync:Bridge] Push detected on '{gameObject.name}' (Ghost {_ownerGhostId}): impulse={impulse} mag={impulseMag:F1}");
            }
        }
        
        // Tracking for Update-based diagnostics
        private Vector3 _lastUpdateHipsPosition = Vector3.zero;
        private Vector3 _lastUpdateParentPosition = Vector3.zero;
        
        private void Update()
        {
            if (!_isRagdolled || _hipsRigidbody == null) return;
            
            // Only track after settlement reported
            if (!_hasReportedSettlement) return;
            
            Vector3 currentHipsPos = _hipsRigidbody.position;
            Vector3 parentPos = _originalParent != null ? _originalParent.position : Vector3.zero;
            
            float hipsDelta = (_lastUpdateHipsPosition != Vector3.zero) 
                ? Vector3.Distance(currentHipsPos, _lastUpdateHipsPosition) 
                : 0f;
            float parentDelta = (_lastUpdateParentPosition != Vector3.zero && _originalParent != null)
                ? Vector3.Distance(parentPos, _lastUpdateParentPosition)
                : 0f;
                
            // Detect position jump in Update (could be from transform sync)
            if (hipsDelta > 0.1f && LogEnabled)
            {
                Debug.LogWarning($"[RagdollPresentation] UPDATE POSITION JUMP! " +
                    $"Hips moved {hipsDelta:F2}m from {_lastUpdateHipsPosition} to {currentHipsPos}");
            }
            
            // Check if the original parent (presentation GameObject) is moving
            if (parentDelta > 0.1f && _originalParent != null && LogEnabled)
            {
                Debug.LogWarning($"[RagdollPresentation] PARENT MOVING! " +
                    $"Parent moved {parentDelta:F2}m from {_lastUpdateParentPosition} to {parentPos}. " +
                    $"This should NOT affect ragdoll since it's unparented!");
            }
            
            _lastUpdateHipsPosition = currentHipsPos;
            _lastUpdateParentPosition = parentPos;
        }
        
        private void FixedUpdate()
        {
            if (!_isRagdolled) return;
            
            // CRITICAL FIX: Delayed collider activation
            // Wait a few physics frames before enabling colliders to let ragdoll fall freely
            // This prevents the explosive impulse from initial ground/object penetration
            if (!_collidersEnabled)
            {
                _colliderEnableDelayFrames++;
                
                // Zero velocity each frame until colliders are enabled
                if (_hipsRigidbody != null)
                {
                    foreach (var rb in _ragdollBodies)
                    {
                        if (rb != null)
                        {
                            rb.linearVelocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }
                }
                
                if (_colliderEnableDelayFrames >= COLLIDER_ENABLE_DELAY)
                {
                    EnableRagdollColliders(true);
                    _collidersEnabled = true;
                    if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                        Debug.Log($"[RagdollPresentation] Colliders ENABLED after {COLLIDER_ENABLE_DELAY} frame delay");
                }
            }
            
            // Bug 2.8.2 Fix: Use centralized simulator instead of direct Physics.Simulate
            // Logic moved to RagdollPhysicsSimulator.cs
            
            // DIAGNOSTIC: Detect sudden velocity spikes after settling
            // This helps identify what's pushing the ragdoll
            DetectVelocitySpike();
            
            // DEBUG: Log velocity on first few frames after ragdoll activation
            _ragdollFrameCount++;
            if (_ragdollFrameCount <= 5 && Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled && _hipsRigidbody != null)
            {
                Debug.Log($"[RagdollPresentation] FixedUpdate Frame {_ragdollFrameCount}: hips.velocity={_hipsRigidbody.linearVelocity} mag={_hipsRigidbody.linearVelocity.magnitude:F2}");
            }
            
            // Check if ragdoll has settled (low velocity for a period of time)
            CheckIfSettled();
            
            // Sync visual position for remote players
            SyncToEntityPosition();
        }
        
        // Track previous velocity for spike detection
        private Vector3 _previousHipsVelocity = Vector3.zero;
        private Vector3 _previousHipsPosition = Vector3.zero;
        
        /// <summary>
        /// Detect sudden velocity spikes that indicate something pushed the ragdoll.
        /// </summary>
        private void DetectVelocitySpike()
        {
            if (_hipsRigidbody == null) return;
            
            Vector3 currentVel = _hipsRigidbody.linearVelocity;
            Vector3 currentPos = _hipsRigidbody.position;
            float velocityChange = (currentVel - _previousHipsVelocity).magnitude;
            float positionChange = (currentPos - _previousHipsPosition).magnitude;
            
            // Start checking immediately for better diagnostics
            if (_ragdollFrameCount > 1)
            {
                // Detect sudden velocity increase (acceleration > 1 m/s per frame = force applied)
                if (velocityChange > 1f && currentVel.magnitude > 0.5f && LogEnabled)
                {
                    Debug.LogWarning($"[RagdollPresentation] VELOCITY SPIKE! " +
                        $"Frame {_ragdollFrameCount}: " +
                        $"delta={velocityChange:F2} m/s, " +
                        $"vel={currentVel} (mag={currentVel.magnitude:F2}), " +
                        $"pos={currentPos}, " +
                        $"hasSettled={_hasSettled}, hasReported={_hasReportedSettlement}");
                }
                
                // Detect sudden position teleport (> 0.5m in one frame = something moved us)
                if (positionChange > 0.5f && LogEnabled)
                {
                    Debug.LogWarning($"[RagdollPresentation] POSITION JUMP! " +
                        $"Frame {_ragdollFrameCount}: " +
                        $"moved {positionChange:F2}m, " +
                        $"from={_previousHipsPosition} to={currentPos}, " +
                        $"hasSettled={_hasSettled}");
                }
            }
            
            _previousHipsVelocity = currentVel;
            _previousHipsPosition = currentPos;
        }
        
        private int _ragdollFrameCount = 0;


        public void EnterRagdoll()
        {
            if (_isRagdolled) return;
            if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            {
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.LogWarning($"[RagdollPresentation] No ragdoll bodies found on {gameObject.name}");
                return;
            }
            
            _isRagdolled = true;
            _hasSettled = false;  // Reset settle state for new ragdoll
            _hasReportedSettlement = false; // Allow reporting for this death
            HasSettled = false;   // Reset API state
            _settleTimer = 0f;
            _ragdollFrameCount = 0; // Reset frame counter for diagnostics
            
            // Register with physics simulator
            Player.PhysicsSimulation.RagdollPhysicsSimulator.Instance.RegisterRagdoll(GetInstanceID());
            
            // CRITICAL: Unparent ragdoll root so ECS transform sync doesn't affect it
            // This allows the ragdoll to fall freely while the entity position stays put
            if (RagdollRoot != null)
            {
                _originalParent = RagdollRoot.parent;
                _originalLocalPosition = RagdollRoot.localPosition;
                _originalLocalRotation = RagdollRoot.localRotation;
                
                // Move to world space
                RagdollRoot.SetParent(null, true);
                
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] Unparented ragdoll root to world space");
                
                // CRITICAL FIX: Disable ALL colliders on the presentation GameObject hierarchy
                // This prevents ghost sync moving the presentation from pushing the ragdoll
                if (_originalParent != null)
                {
                    DisableAllCollidersOnTransform(_originalParent);
                }
            }
            
            // DEBUG: Force log to confirm this method is called
            Debug.Log($"[RagdollPresentation] [ID:{GetInstanceID()}] Entering ragdoll on {gameObject.name} (IsOwned={_isOwned}, Diag={Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled})");

            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                Debug.Log($"[RagdollPresentation] Animator: {(PlayerAnimator != null ? PlayerAnimator.name : "NULL")} enabled={PlayerAnimator?.enabled}");
            }
            
            // Disable Animator to stop overriding bone poses
            if (PlayerAnimator != null)
            {
                PlayerAnimator.enabled = false;
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] Animator DISABLED");
            }
            else if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                Debug.LogWarning($"[RagdollPresentation] No Animator found!");
            }
            
            // Enable physics for ALL ragdolls (owned and non-owned)
            // Non-owned ragdolls use local physics for visual fidelity with server position as spring target
            
            // Disable main player collider to prevent explosive collision with ragdoll bones
            if (_mainCollider != null)
            {
                _mainCollider.enabled = false;
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] DISABLED main collider to prevent ragdoll collision");
            }
            
            // Also disable CharacterController - it has its own capsule that causes interference
            if (_characterController != null)
            {
                _characterController.enabled = false;
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] DISABLED CharacterController to prevent ragdoll collision");
            }
            
            // Disable KinematicCharacterController - its ResolveOverlaps explosively separates ragdoll bones
            if (_kinematicCharacterController != null)
            {
                _kinematicCharacterController.enabled = false;
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] DISABLED KinematicCharacterController to prevent ResolveOverlaps pushing ragdoll bones");
            }
            
            // DEBUG: Log velocity BEFORE enabling physics
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled && _hipsRigidbody != null)
            {
                Debug.Log($"[RagdollPresentation] BEFORE SetRagdollEnabled: hips.velocity={_hipsRigidbody.linearVelocity} isKinematic={_hipsRigidbody.isKinematic}");
            }
            
            // Enable ragdoll physics for ALL ragdolls (local simulation for visual fidelity)
            SetRagdollEnabled(true);
            
            // DEBUG: Log velocity AFTER enabling physics
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled && _hipsRigidbody != null)
            {
                Debug.Log($"[RagdollPresentation] AFTER SetRagdollEnabled: hips.velocity={_hipsRigidbody.linearVelocity} isKinematic={_hipsRigidbody.isKinematic}");
            }
            
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                Debug.Log($"[RagdollPresentation] Physics ENABLED ({(_isOwned ? "owned" : "non-owned")} ragdoll)");
            
            // Log state of each rigidbody including LAYER
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                foreach (var rb in _ragdollBodies)
                {
                    if (rb != null)
                    {
                        string layerName = LayerMask.LayerToName(rb.gameObject.layer);
                        Debug.Log($"[RagdollPresentation] Body '{rb.name}': layer={layerName}({rb.gameObject.layer}), isKinematic={rb.isKinematic}, useGravity={rb.useGravity}");
                    }
                }
                
                // Log collider info
                if (_ragdollColliders != null)
                {
                    foreach (var col in _ragdollColliders)
                    {
                        if (col != null)
                        {
                            Debug.Log($"[RagdollPresentation] Collider '{col.name}': enabled={col.enabled}, isTrigger={col.isTrigger}");
                        }
                    }
                }
            }
        }

        public void ExitRagdoll()
        {
            if (!_isRagdolled) return;
            _isRagdolled = false;
            
            // Unregister from physics simulator
            Player.PhysicsSimulation.RagdollPhysicsSimulator.Instance.UnregisterRagdoll(GetInstanceID());
            
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                Debug.Log($"[RagdollPresentation] Exiting ragdoll on {gameObject.name}");
            }
            
            // Disable ragdoll physics
            SetRagdollEnabled(false);
            
            // Re-enable main player collider
            if (_mainCollider != null)
            {
                _mainCollider.enabled = true;
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] RE-ENABLED main collider");
            }
            
            // Re-enable CharacterController
            if (_characterController != null)
            {
                _characterController.enabled = true;
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] RE-ENABLED CharacterController");
            }
            
            // Re-enable KinematicCharacterController
            if (_kinematicCharacterController != null)
            {
                _kinematicCharacterController.enabled = true;
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] RE-ENABLED KinematicCharacterController");
            }
            
            // CRITICAL: Reparent ragdoll root back to original parent
            if (RagdollRoot != null && _originalParent != null)
            {
                RagdollRoot.SetParent(_originalParent, false);
                RagdollRoot.localPosition = _originalLocalPosition;
                RagdollRoot.localRotation = _originalLocalRotation;
                
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.Log($"[RagdollPresentation] Reparented ragdoll root back to {_originalParent.name}");
            }
            
            // Re-enable Animator - it will blend back to default pose
            if (PlayerAnimator != null)
            {
                PlayerAnimator.enabled = true;

                // Force reset to idle state
                PlayerAnimator.Rebind();
                PlayerAnimator.Update(0f);
            }

            // EPIC 13.19: Clear remote sync state
            _hasRemoteSyncData = false;
        }

        [Tooltip("Distance threshold for snapping unowned ragdolls to entity position")]
        public float VisualSyncThreshold = 2.0f;
        
        [Tooltip("Interpolation speed for non-owned ragdolls following entity position")]
        public float VisualSyncLerpSpeed = 10f;
        
        [Header("Server Sync Spring Settings (Non-Owned Ragdolls)")]
        [Tooltip("Spring force strength for pulling ragdoll toward server position")]
        public float ServerSyncSpringStrength = 50f;
        
        [Tooltip("Damping factor to prevent oscillation")]
        public float ServerSyncDamping = 5f;
        
        [Tooltip("Minimum distance before spring force is applied")]
        public float ServerSyncDeadzone = 0.05f;

        // Periodic logging counter
        private int _syncLogCounter = 0;
        
        private void SyncToEntityPosition()
        {
            // Only sync if we are NOT the owner (owned players use local physics, server is authoritative)
            if (_isOwned || RagdollRoot == null || _hipsRigidbody == null) return;

            // EPIC 13.19: Use server-authoritative sync data if available
            if (_hasRemoteSyncData)
            {
                Vector3 targetPos = _remoteSyncPosition;
                Vector3 currentPos = _hipsRigidbody.position;
                float dist = Vector3.Distance(currentPos, targetPos);
                
                // Periodic diagnostic log every 10 frames (~0.2 second) for debugging
                _syncLogCounter++;
                if (LogEnabled && (_syncLogCounter % 10 == 0))
                {
                    Debug.Log($"[RagdollSync:Client] [ID:{GetInstanceID()}] '{gameObject.name}' Ghost={_ownerGhostId} | ServerPos={targetPos} | LocalPos={currentPos} | Dist={dist:F2}m | SpringForce={(dist > ServerSyncDeadzone ? (dist * ServerSyncSpringStrength) : 0):F1}");
                }

                if (dist > VisualSyncThreshold)
                {
                    // Large desync - snap immediately and zero velocity
                    _hipsRigidbody.position = targetPos;
                    _hipsRigidbody.rotation = _remoteSyncRotation;
                    _hipsRigidbody.linearVelocity = Vector3.zero;
                    _hipsRigidbody.angularVelocity = Vector3.zero;

                    if (LogEnabled)
                    {
                        Debug.LogWarning($"[RagdollSync:Bridge] SNAP! '{gameObject.name}' Ghost={_ownerGhostId} moved {dist:F2}m to server pos {targetPos}");
                    }
                }
                else if (dist > ServerSyncDeadzone)
                {
                    // Small desync - apply spring force toward server position
                    // This allows local physics to run while gently pulling toward the authoritative position
                    Vector3 toTarget = targetPos - currentPos;
                    Vector3 springForce = toTarget * ServerSyncSpringStrength;
                    
                    // Apply damping to prevent oscillation
                    Vector3 dampingForce = -_hipsRigidbody.linearVelocity * ServerSyncDamping;
                    
                    // Apply combined force
                    _hipsRigidbody.AddForce(springForce + dampingForce, ForceMode.Acceleration);

                    if (LogEnabled && (_syncLogCounter % 10 == 0))
                    {
                        Debug.Log($"[RagdollSync:Bridge:APPLY] Ghost={_ownerGhostId} Applying Spring Force: {springForce} Damping: {dampingForce} ToTarget: {toTarget}");
                    }
                    
                    // Also apply torque toward target rotation
                    Quaternion rotDiff = _remoteSyncRotation * Quaternion.Inverse(_hipsRigidbody.rotation);
                    rotDiff.ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f) angle -= 360f;
                    if (Mathf.Abs(angle) > 1f)
                    {
                        Vector3 torque = axis * (angle * Mathf.Deg2Rad * ServerSyncSpringStrength * 0.5f);
                        _hipsRigidbody.AddTorque(torque - _hipsRigidbody.angularVelocity * ServerSyncDamping, ForceMode.Acceleration);
                    }
                }
                return;
            }

            // Fallback: no server sync yet - ragdoll runs purely locally
            // This happens briefly when ragdoll first activates before server data arrives
        }
        
        /// <summary>
        /// Checks if the ragdoll has stopped moving and sends position to server.
        /// </summary>
        private void CheckIfSettled()
        {
            // Already reported settlement this death - don't re-check or re-send
            if (_hasReportedSettlement) return;
            
            if (_hasSettled) 
            {
                // Task 2.8.7: If settled but moving again (e.g. pushed), reset settle state
                // NOTE: This just resets the local settle flag, not the reported flag
                if (_hipsRigidbody != null && _hipsRigidbody.linearVelocity.magnitude > SettleVelocityThreshold * 2.0f)
                {
                    _hasSettled = false;
                    _settleTimer = 0f;
                    if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled) Debug.Log($"[RagdollPresentation] Ragdoll moving again (vel={_hipsRigidbody.linearVelocity.magnitude:F2}) - un-settled");
                }
                else
                {
                    return; // Still settled
                }
            }
            
            if (_hipsRigidbody == null) return;
            
            float velocity = _hipsRigidbody.linearVelocity.magnitude;
            
            if (velocity < SettleVelocityThreshold)
            {
                _settleTimer += Time.fixedDeltaTime;
                
                // Debug progress (every 0.5s of progress)
                if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled && _settleTimer > 0.1f && _settleTimer % 0.5f < Time.fixedDeltaTime)
                {
                    Debug.Log($"[RagdollPresentation] Settling... Timer={_settleTimer:F2}/{SettleTimeRequired} Vel={velocity:F3}");
                }
                
                if (_settleTimer >= SettleTimeRequired)
                {
                    _hasSettled = true;
                    OnRagdollSettled();
                }
            }
            else
            {
                // Reset timer if moving again
                if (_settleTimer > 0.5f && Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled) 
                {
                     Debug.Log($"[RagdollPresentation] Settlement interrupted! Velocity {velocity:F3} > {SettleVelocityThreshold}. Timer reset.");
                }
                _settleTimer = 0f;
            }
        }
        
        /// <summary>
        /// Called when ragdoll has settled - notify server of final position.
        /// Only sends ONE RPC per death, regardless of subsequent movement.
        /// </summary>
        private void OnRagdollSettled()
        {
            if (RagdollRoot == null) return;
            if (_hasReportedSettlement) return; // Already reported this death
            
            Vector3 finalPosition = RagdollRoot.position;
            Quaternion finalRotation = RagdollRoot.rotation;
            
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                Debug.Log($"[RagdollPresentation] Ragdoll settled at position {finalPosition}");
            }
            
            // Note: We no longer freeze ragdoll bodies here - velocity clamping in FixedUpdate
            // handles preventing explosive launches while still allowing physics interactions
            
            if (!_isOwned) return;

            // Mark as reported - only send ONE RPC per death
            _hasReportedSettlement = true;

            // Notify via polling state (RagdollSettleClientSystem checks this)
            HasSettled = true;
            SettledPosition = new float3(finalPosition.x, finalPosition.y, finalPosition.z);
            SettledRotation = new quaternion(finalRotation.x, finalRotation.y, finalRotation.z, finalRotation.w);
            
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                Debug.Log($"[RagdollPresentation] Settlement reported to server (will not re-report)");
            }
        }
        
        /// <summary>
        /// Clamp all ragdoll body velocities to prevent explosive launches.
        /// This allows full physics interactions but caps max speed.
        /// </summary>
        private void ClampRagdollVelocities()
        {
            if (_ragdollBodies == null) return;
            
            foreach (var rb in _ragdollBodies)
            {
                if (rb != null && !rb.isKinematic)
                {
                    // Clamp linear velocity
                    if (rb.linearVelocity.magnitude > MAX_RAGDOLL_VELOCITY)
                    {
                        rb.linearVelocity = rb.linearVelocity.normalized * MAX_RAGDOLL_VELOCITY;
                    }
                    
                    // Also clamp angular velocity to prevent spinning out of control
                    const float MAX_ANGULAR_VELOCITY = 10f;
                    if (rb.angularVelocity.magnitude > MAX_ANGULAR_VELOCITY)
                    {
                        rb.angularVelocity = rb.angularVelocity.normalized * MAX_ANGULAR_VELOCITY;
                    }
                }
            }
        }
        
        // Flag to ensure we only send ONE settle RPC per death
        private bool _hasReportedSettlement = false;
        
        // Polling API for RagdollSettleClientSystem
        public bool HasSettled { get; private set; }
        public float3 SettledPosition { get; private set; }
        public quaternion SettledRotation { get; private set; }
        
        /// <summary>
        /// Set up Physics.IgnoreCollision between all ragdoll bone colliders.
        /// This prevents explosive separation when bones start overlapping each other.
        /// </summary>
        private void SetupBoneCollisionIgnore()
        {
            if (_ragdollColliders == null || _ragdollColliders.Length < 2) return;
            
            int ignoreCount = 0;
            for (int i = 0; i < _ragdollColliders.Length; i++)
            {
                for (int j = i + 1; j < _ragdollColliders.Length; j++)
                {
                    if (_ragdollColliders[i] != null && _ragdollColliders[j] != null)
                    {
                        Physics.IgnoreCollision(_ragdollColliders[i], _ragdollColliders[j], true);
                        ignoreCount++;
                    }
                }
            }
            
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                Debug.Log($"[RagdollPresentation] Set up {ignoreCount} bone-to-bone collision ignores on {gameObject.name}");
            }
        }
        
        /// <summary>
        /// Disable all colliders on a transform hierarchy.
        /// This prevents the ghost sync from causing physics interactions with the ragdoll.
        /// </summary>
        private void DisableAllCollidersOnTransform(Transform root)
        {
            if (root == null) return;
            
            // Get all colliders in the hierarchy (but not in the ragdoll which is now unparented)
            var colliders = root.GetComponentsInChildren<Collider>(true);
            int disabledCount = 0;
            
            foreach (var col in colliders)
            {
                if (col != null && col.enabled)
                {
                    col.enabled = false;
                    disabledCount++;
                    
                    if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled)
                    {
                        Debug.Log($"[RagdollPresentation] DISABLED collider on presentation: {col.gameObject.name} ({col.GetType().Name})");
                    }
                }
            }
            
            if (Player.Systems.RagdollSettleClientSystem.DiagnosticsEnabled && disabledCount > 0)
            {
                Debug.Log($"[RagdollPresentation] Disabled {disabledCount} colliders on presentation GameObject to prevent ghost sync interference");
            }
        }
        
        public void ClearSettleState()
        {
            HasSettled = false;
        }
        
        private bool _isOwned = false;

        /// <summary>
        /// Called by RagdollPresentationSystem each frame with current death state.
        /// Handles transitions between ragdoll and normal states.
        /// </summary>
        public void UpdateRagdollState(bool shouldRagdoll, bool isOwned)
        {
            _isOwned = isOwned;
            // DEBUG: Trace call to ensure we aren't filtering it out
            if (!isOwned && shouldRagdoll)
                Debug.Log($"[RagdollPresentation:Bridge] [ID:{GetInstanceID()}] UpdateRagdollState shouldRagdoll={shouldRagdoll} isOwned={isOwned} _isRagdolled={_isRagdolled}");

            if (shouldRagdoll && !_isRagdolled)
            {
                EnterRagdoll();
            }
            else if (!shouldRagdoll && _isRagdolled)
            {
                ExitRagdoll();
            }
        }
        
        /// <summary>
        /// Enable or disable ragdoll physics on all bones.
        /// NOTE: When enabling, colliders are NOT enabled immediately - they use delayed activation
        /// to prevent explosive separation from initial overlaps.
        /// </summary>
        private void SetRagdollEnabled(bool enabled)
        {
            if (_ragdollBodies == null) return;
            
            foreach (var rb in _ragdollBodies)
            {
                if (rb != null)
                {
                    rb.isKinematic = !enabled;
                    rb.useGravity = enabled;
                    
                    // BUGFIX: Reset velocity when enabling physics
                    // Prevents accumulated velocity from animator/prior state from launching ragdoll
                    if (enabled)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
            
            // CRITICAL FIX: When ENABLING ragdoll, do NOT enable colliders yet!
            // They will be enabled via delayed activation in FixedUpdate.
            // When DISABLING ragdoll, disable colliders immediately.
            if (!enabled)
            {
                EnableRagdollColliders(false);
            }
            else
            {
                // Start with colliders DISABLED - they'll be enabled after delay
                EnableRagdollColliders(false);
                _collidersEnabled = false;
                _colliderEnableDelayFrames = 0;
            }
        }
        
        /// <summary>
        /// Enable or disable all ragdoll bone colliders.
        /// </summary>
        private void EnableRagdollColliders(bool enabled)
        {
            if (_ragdollColliders == null) return;
            
            foreach (var col in _ragdollColliders)
            {
                if (col != null)
                    col.enabled = enabled;
            }
        }
        
        /// <summary>
        /// Manual trigger for testing in Editor.
        /// </summary>
        [ContextMenu("Test Enter Ragdoll")]
        public void TestEnterRagdoll()
        {
            EnterRagdoll();
        }
        
        [ContextMenu("Test Exit Ragdoll")]
        public void TestExitRagdoll()
        {
            ExitRagdoll();
        }
    }
}
