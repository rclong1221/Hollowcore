using UnityEngine;
using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Player.Components;
using DIG.Player.Components;
using DIG.Targeting.Core;
using DIG.Targeting.Systems;
using DIG.Core.Input;
using DIG.CameraSystem;

namespace DIG.CameraSystem.Cinemachine
{
    /// <summary>
    /// Camera modes for CinemachineCameraController.
    /// </summary>
    public enum CinemachineCameraMode
    {
        ThirdPerson,    // Behind character, orbit with mouse
        FirstPerson,    // Eye level
        Isometric       // Top-down/isometric for ARPG, MOBA, TwinStick
    }

    /// <summary>
    /// EPIC 14.18 - Cinemachine Camera Controller
    /// MonoBehaviour bridge between ECS player data and Cinemachine virtual cameras.
    /// 
    /// Responsibilities:
    /// - Manages virtual camera priorities for view switching
    /// - Updates follow target transform from ECS data
    /// - Handles zoom by adjusting camera distance
    /// - Provides smooth FPS↔TPS transitions via Cinemachine Brain blending
    /// 
    /// Setup:
    /// 1. Add to camera rig GameObject
    /// 2. Assign virtual cameras in inspector
    /// 3. Create FollowTarget transform (can be child of this object)
    /// 4. Brain on Main Camera handles blending automatically
    /// </summary>
    public class CinemachineCameraController : MonoBehaviour
    {
        // ============================================================
        // SINGLETON
        // ============================================================
        
        private static CinemachineCameraController _instance;
        public static CinemachineCameraController Instance => _instance;
        public static bool HasInstance => _instance != null;
        
        // ============================================================
        // REFERENCES
        // ============================================================
        
        [Header("Virtual Cameras")]
        [Tooltip("Third-person orbit camera (behind character)")]
        [SerializeField] private CinemachineCamera _thirdPersonCamera;
        
        [Tooltip("First-person camera (at eye level)")]
        [SerializeField] private CinemachineCamera _firstPersonCamera;
        
        [Tooltip("Isometric/top-down camera (for ARPG, MOBA, TwinStick)")]
        [SerializeField] private CinemachineCamera _isometricCamera;
        
        [Header("Follow Target")]
        [Tooltip("Transform that virtual cameras follow. Updated from ECS each frame.")]
        [SerializeField] private Transform _followTarget;
        
        [Tooltip("Transform for first-person camera position (eye level)")]
        [SerializeField] private Transform _fpsTarget;
        
        [Header("Lock-On Settings")]
        [Tooltip("Transform positioned at lock-on target. Created automatically if not assigned.")]
        [SerializeField] private Transform _lockOnTarget;
        
        [Header("Settings")]
        [Tooltip("Priority for the active camera (higher = active)")]
        [SerializeField] private int _activePriority = 10;
        
        [Tooltip("Priority for inactive cameras")]
        [SerializeField] private int _inactivePriority = 0;
        
        [Tooltip("Distance threshold to switch to FPS (when zoom brings camera this close)")]
        [SerializeField] private float _fpsDistanceThreshold = 0.5f;
        
        [Header("Third-Person Settings")]
        [Tooltip("Minimum camera distance (zoomed in)")]
        [SerializeField] private float _minDistance = 2f;
        
        [Tooltip("Maximum camera distance (zoomed out)")]
        [SerializeField] private float _maxDistance = 15f;
        
        [Tooltip("Shoulder offset for over-the-shoulder view")]
        [SerializeField] private Vector3 _shoulderOffset = new Vector3(0.5f, 0f, 0f);
        
        [Header("First-Person Settings")]
        [Tooltip("Offset from character root to eye position")]
        [SerializeField] private Vector3 _eyeOffset = new Vector3(0f, 1.7f, 0f);
        
        [Header("Isometric Settings")]
        [Tooltip("Yaw angle for isometric view (0 = north, 45 = NE diamond view)")]
        [SerializeField] private float _isometricYaw = 45f;
        
        // ============================================================
        // STATE
        // ============================================================
        
        private CinemachineCameraMode _currentCameraMode = CinemachineCameraMode.ThirdPerson;
        private CameraViewType _currentViewType = CameraViewType.Combat;
        private float _currentDistance = 8f;
        private float _currentYaw = 0f;
        private float _currentPitch = 25f;
        private bool _isLockedOn = false;
        
        // Smooth transition when lock breaks (prevents flicker)
        private float _lockBreakTransitionTimer = 0f;
        private float _lockBreakStartYaw = 0f;
        private float _lockBreakStartPitch = 0f;
        private const float LOCK_BREAK_TRANSITION_DURATION = 0.2f;
        
        // EPIC 15.16: Camera arrival detection for Lock Phase state machine
        // When camera is within this threshold of target, signal Locking -> Locked
        private const float CAMERA_ARRIVAL_THRESHOLD_DEGREES = 5f;
        
        // EPIC 15.16: Expose camera arrival state for CameraLockOnSystem to query
        private float _currentYawError = 0f;
        private float _currentPitchError = 0f;
        
        private World _clientWorld;
        private Entity _playerEntity;
        private bool _isInitialized;
        
        // Cinemachine component caches
        private CinemachineThirdPersonFollow _tpFollow;
        private CinemachineOrbitalFollow _tpOrbital; // For orbit control
        private CinemachinePanTilt _fpsPanTilt; // CM3 uses PanTilt instead of POV
        private CinemachineRotationComposer _tpRotationComposer; // For lock-on smooth aiming
        
        // Camera mode adapter for CameraModeProvider integration
        private CinemachineCameraModeAdapter _cameraModeAdapter;
        
        // EPIC 15.20: Cached query for ParadigmSettings singleton (avoids GC alloc per frame)
        private EntityQuery _paradigmSettingsQuery;
        
        // ============================================================
        // PROPERTIES
        // ============================================================
        
        /// <summary>Current camera mode (ThirdPerson, FirstPerson, Isometric).</summary>
        public CinemachineCameraMode CurrentCameraMode => _currentCameraMode;
        
        /// <summary>Current active view type.</summary>
        public CameraViewType CurrentViewType => _currentViewType;
        
        /// <summary>Whether in first-person mode.</summary>
        public bool IsFirstPerson => _currentCameraMode == CinemachineCameraMode.FirstPerson || 
            (_currentCameraMode == CinemachineCameraMode.ThirdPerson && _currentDistance < _fpsDistanceThreshold);
        
        /// <summary>Whether in isometric mode.</summary>
        public bool IsIsometric => _currentCameraMode == CinemachineCameraMode.Isometric;
        
        /// <summary>Current camera distance (third-person only).</summary>
        public float CurrentDistance => _currentDistance;
        
        /// <summary>The follow target transform for external systems.</summary>
        public Transform FollowTarget => _followTarget;
        
        /// <summary>
        /// EPIC 15.16: Whether the camera has arrived at its lock target.
        /// True when yaw and pitch errors are both below the arrival threshold.
        /// Used by CameraLockOnSystem to transition from Locking -> Locked phase.
        /// </summary>
        public bool HasCameraArrivedAtTarget => _isLockedOn && 
            _currentYawError < CAMERA_ARRIVAL_THRESHOLD_DEGREES && 
            _currentPitchError < CAMERA_ARRIVAL_THRESHOLD_DEGREES;
        
        /// <summary>Current yaw error from desired lock-on direction (degrees).</summary>
        public float CurrentYawError => _currentYawError;
        
        /// <summary>Current pitch error from desired lock-on direction (degrees).</summary>
        public float CurrentPitchError => _currentPitchError;
        
        // ============================================================
        // EVENTS
        // ============================================================
        
        /// <summary>
        /// Fired when camera mode changes. Parameters: (previousMode, newMode)
        /// </summary>
        public event System.Action<CinemachineCameraMode, CinemachineCameraMode> OnCameraModeChanged;
        
        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[CinemachineCameraController] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            ValidateSetup();
            CacheComponents();
            
            // EPIC 15.20: Subscribe to paradigm changes to auto-switch camera mode
            if (ParadigmStateMachine.Instance != null)
            {
                ParadigmStateMachine.Instance.OnParadigmChanged += OnParadigmChanged;
                // Apply initial paradigm's camera mode
                var activeProfile = ParadigmStateMachine.Instance.ActiveProfile;
                if (activeProfile != null)
                {
                    OnParadigmChanged(activeProfile);
                }
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            
            // EPIC 15.20: Unsubscribe from paradigm changes
            if (ParadigmStateMachine.Instance != null)
            {
                ParadigmStateMachine.Instance.OnParadigmChanged -= OnParadigmChanged;
            }
        }
        
        /// <summary>
        /// EPIC 15.20: Handle paradigm changes by switching to appropriate camera mode.
        /// </summary>
        private void OnParadigmChanged(InputParadigmProfile profile)
        {
            if (profile == null || profile.compatibleCameraModes == null || profile.compatibleCameraModes.Length == 0)
                return;
            
            // Map the first compatible CameraMode to CinemachineCameraMode
            var targetCameraMode = MapToCinemachineCameraMode(profile.compatibleCameraModes[0]);
            
            if (targetCameraMode != _currentCameraMode)
            {
                Debug.Log($"[CinemachineCameraController] Paradigm '{profile.displayName}' requires camera mode {targetCameraMode}");
                SetCameraMode(targetCameraMode);
            }
        }
        
        /// <summary>
        /// Maps CameraMode enum to CinemachineCameraMode.
        /// </summary>
        private CinemachineCameraMode MapToCinemachineCameraMode(CameraMode cameraMode)
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => CinemachineCameraMode.FirstPerson,
                CameraMode.IsometricFixed => CinemachineCameraMode.Isometric,
                CameraMode.IsometricRotatable => CinemachineCameraMode.Isometric,
                CameraMode.TopDownFixed => CinemachineCameraMode.Isometric,
                _ => CinemachineCameraMode.ThirdPerson // ThirdPersonFollow, etc.
            };
        }
        
        private void LateUpdate()
        {
            // EPIC 18.13: Yield to death camera / cutscene / editor camera override.
            // Disable virtual camera priorities so Cinemachine Brain doesn't fight
            // the override camera driving Camera.main (handled by CameraManager).
            if (DIG.DeathCamera.CameraAuthorityGate.IsOverridden)
            {
                if (_isInitialized)
                    DisableAllVirtualCameras();
                return;
            }

            if (!_isInitialized)
            {
                TryInitialize();
                return;
            }

            // Re-enable virtual cameras if they were disabled by authority override
            RestoreVirtualCameraPriorities();

            UpdateFromECS();
            UpdateVirtualCameras();
        }
        
        // ============================================================
        // INITIALIZATION
        // ============================================================
        
        private void ValidateSetup()
        {
            if (_thirdPersonCamera == null)
            {
                Debug.LogError("[CinemachineCameraController] Third-person camera not assigned!");
            }
            
            if (_firstPersonCamera == null)
            {
                Debug.LogError("[CinemachineCameraController] First-person camera not assigned!");
            }
            
            if (_followTarget == null)
            {
                // Create follow target if not assigned
                var go = new GameObject("CinemachineFollowTarget");
                go.transform.SetParent(transform);
                _followTarget = go.transform;
                Debug.Log("[CinemachineCameraController] Created follow target transform.");
            }
            
            if (_fpsTarget == null)
            {
                // Create FPS target if not assigned
                var go = new GameObject("CinemachineFPSTarget");
                go.transform.SetParent(transform);
                _fpsTarget = go.transform;
                Debug.Log("[CinemachineCameraController] Created FPS target transform.");
            }
            
            if (_lockOnTarget == null)
            {
                // Create lock-on target transform - this will be positioned at the enemy
                var go = new GameObject("CinemachineLockOnTarget");
                go.transform.SetParent(null); // World space, not parented
                DontDestroyOnLoad(go);
                _lockOnTarget = go.transform;
                Debug.Log("[CinemachineCameraController] Created lock-on target transform.");
            }
        }
        
        private void CacheComponents()
        {
            if (_thirdPersonCamera != null)
            {
                _tpFollow = _thirdPersonCamera.GetComponent<CinemachineThirdPersonFollow>();
                _tpOrbital = _thirdPersonCamera.GetComponent<CinemachineOrbitalFollow>();
                
                if (_tpFollow == null && _tpOrbital == null)
                {
                    // Try to get from CinemachineCamera's component pipeline
                    var components = _thirdPersonCamera.GetComponents<CinemachineComponentBase>();
                    foreach (var comp in components)
                    {
                        if (comp is CinemachineThirdPersonFollow tpf)
                            _tpFollow = tpf;
                        if (comp is CinemachineOrbitalFollow orbital)
                            _tpOrbital = orbital;
                    }
                }
            }
            
            if (_firstPersonCamera != null)
            {
                _fpsPanTilt = _firstPersonCamera.GetComponent<CinemachinePanTilt>();
                if (_fpsPanTilt == null)
                {
                    var components = _firstPersonCamera.GetComponents<CinemachineComponentBase>();
                    foreach (var comp in components)
                    {
                        if (comp is CinemachinePanTilt panTilt)
                        {
                            _fpsPanTilt = panTilt;
                            break;
                        }
                    }
                }
            }
        }
        
        private void TryInitialize()
        {
            // Find client world
            if (_clientWorld == null || !_clientWorld.IsCreated)
            {
                foreach (var world in World.All)
                {
                    if (world.IsCreated && world.Name == "ClientWorld")
                    {
                        _clientWorld = world;
                        break;
                    }
                }
                
                if (_clientWorld == null) return;
            }
            
            // Find local player entity
            var query = _clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>()
            );
            
            if (!query.IsEmpty)
            {
                var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                if (entities.Length > 0)
                {
                    _playerEntity = entities[0];
                    _isInitialized = true;
                    Debug.Log($"[CinemachineCameraController] Initialized with player entity: {_playerEntity}");
                }
                entities.Dispose();
            }
            query.Dispose();
        }
        
        // ============================================================
        // ECS INTEGRATION
        // ============================================================
        
        private void UpdateFromECS()
        {
            if (_clientWorld == null || !_clientWorld.IsCreated) return;
            if (_playerEntity == Entity.Null || !_clientWorld.EntityManager.Exists(_playerEntity)) 
            {
                _isInitialized = false;
                return;
            }
            
            var em = _clientWorld.EntityManager;
            
            // Get player position
            if (em.HasComponent<Unity.Transforms.LocalTransform>(_playerEntity))
            {
                var transform = em.GetComponentData<Unity.Transforms.LocalTransform>(_playerEntity);
                
                // Update follow target position
                _followTarget.position = transform.Position;
            }
            
            // Get camera settings
            if (em.HasComponent<PlayerCameraSettings>(_playerEntity))
            {
                var settings = em.GetComponentData<PlayerCameraSettings>(_playerEntity);
                
                _currentDistance = settings.CurrentDistance;
                
                // EPIC 15.16: Souls-like Lock-On Camera
                // Camera orbits BEHIND the player, facing toward target
                // Camera does NOT zoom in or go first-person
                // Player can move freely, camera auto-adjusts to keep target in view
                bool isLocked = false;
                bool justUnlocked = false;
                float3 targetPos = float3.zero;
                
                if (em.HasComponent<CameraTargetLockState>(_playerEntity))
                {
                    var lockState = em.GetComponentData<CameraTargetLockState>(_playerEntity);
                    isLocked = lockState.IsLocked;
                    targetPos = lockState.LastTargetPosition;
                    justUnlocked = lockState.JustUnlocked; // ECS signals this frame is transition
                }
                
                // Only consider locked if we have a valid target position (not at origin)
                if (isLocked && math.lengthsq(targetPos) < 0.01f)
                {
                    isLocked = false;
                }
                
                // EPIC 15.16: Check lock behavior type
                // Use static override for immediate sync (same source as CameraLockOnSystem)
                LockBehaviorType lockBehaviorType = TargetingModeTester.StaticModeSet 
                    ? TargetingModeTester.StaticCurrentMode 
                    : LockBehaviorHelper.GetCurrentMode();
                
                // EPIC 15.16: Get current lock phase for state machine
                LockPhase lockPhase = LockPhase.Unlocked;
                if (em.HasComponent<CameraTargetLockState>(_playerEntity))
                {
                    lockPhase = em.GetComponentData<CameraTargetLockState>(_playerEntity).Phase;
                }
                
                // Both HardLock and SoftLock force camera to target while locked
                // FirstPerson, TwinStick, Isometric, OTS do NOT force camera
                bool shouldForceCamera = isLocked && 
                    (lockBehaviorType == LockBehaviorType.HardLock || 
                     lockBehaviorType == LockBehaviorType.SoftLock);
                
                if (shouldForceCamera)
                {
                    if (!_isLockedOn)
                    {
                        _isLockedOn = true;
                        Debug.Log($"[CinemachineCamera] Lock-on ENGAGED, target at {targetPos}");
                    }
                    
                    // Calculate direction from PLAYER to TARGET (horizontal only for yaw)
                    Vector3 playerPos = _followTarget.position;
                    Vector3 toTarget = (Vector3)targetPos - playerPos;
                    float horizontalDist = new Vector2(toTarget.x, toTarget.z).magnitude;
                    
                    // Debug logging every 30 frames to track state
                    if (Time.frameCount % 30 == 0)
                    {
                        Debug.Log($"[LockOn] Player:{playerPos:F1} Target:{targetPos} dist:{horizontalDist:F1} yaw:{_currentYaw:F1} pitch:{_currentPitch:F1}");
                    }
                    
                    if (horizontalDist > 0.5f)
                    {
                        // Calculate desired yaw: camera should be BEHIND player, looking TOWARD target
                        // So camera yaw = direction from player to target
                        float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                        if (desiredYaw < 0f) desiredYaw += 360f;
                        
                        // Calculate desired pitch to look slightly down at target
                        // Use the height difference between camera position and target
                        float cameraHeight = playerPos.y + 1.7f; // Approximate camera pivot height
                        float heightDiff = (float)targetPos.y - cameraHeight;
                        float desiredPitch = Mathf.Atan2(-heightDiff, horizontalDist) * Mathf.Rad2Deg;
                        desiredPitch = Mathf.Clamp(desiredPitch, -30f, 45f); // Reasonable pitch limits
                        
                        // Smoothly interpolate toward desired angles
                        // This creates the smooth tracking effect
                        float rotSpeed = 10f; // Degrees/second factor (increased for faster arrival)
                        
                        // Calculate and store errors for CameraLockOnSystem to query
                        _currentYawError = Mathf.Abs(Mathf.DeltaAngle(_currentYaw, desiredYaw));
                        _currentPitchError = Mathf.Abs(_currentPitch - desiredPitch);
                        
                        // Snap to target when very close (prevents asymptotic lerp from never reaching)
                        if (_currentYawError < 1f && _currentPitchError < 1f)
                        {
                            _currentYaw = desiredYaw;
                            _currentPitch = desiredPitch;
                            _currentYawError = 0f;
                            _currentPitchError = 0f;
                        }
                        else
                        {
                            _currentYaw = Mathf.LerpAngle(_currentYaw, desiredYaw, Time.deltaTime * rotSpeed);
                            _currentPitch = Mathf.Lerp(_currentPitch, desiredPitch, Time.deltaTime * rotSpeed);
                        }
                        
                        // Note: Camera arrival detection (Locking -> Locked phase transition)
                        // is now handled by CameraLockOnSystem querying HasCameraArrivedAtTarget
                    }
                    
                    // Apply the calculated rotation to follow target
                    // This drives where CinemachineThirdPersonFollow positions the camera
                    // Use consistent format: pitch in X, yaw in Y (same as unlocked)
                    _followTarget.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
                }
                else
                {
                    // Not forcing camera (either unlocked OR using Soft Lock / other modes)
                    // Reset error values when not locked on
                    _currentYawError = float.MaxValue;
                    _currentPitchError = float.MaxValue;
                    
                    // Use JustUnlocked flag from ECS for cross-system timing coordination
                    if (_isLockedOn || justUnlocked)
                    {
                        _isLockedOn = false;
                        Debug.Log("[CinemachineCamera] Lock-on camera tracking DISENGAGED");
                        
                        // Start smooth transition from lock angles to player-controlled angles
                        _lockBreakTransitionTimer = LOCK_BREAK_TRANSITION_DURATION;
                        _lockBreakStartYaw = _currentYaw;
                        _lockBreakStartPitch = _currentPitch;
                        
                        // When lock ends, update ECS settings to match current camera angles
                        // This prevents snapping back to pre-lock rotation
                        if (em.HasComponent<PlayerCameraSettings>(_playerEntity))
                        {
                            var camSettings = em.GetComponentData<PlayerCameraSettings>(_playerEntity);
                            camSettings.Yaw = _currentYaw;
                            camSettings.Pitch = _currentPitch;
                            em.SetComponentData(_playerEntity, camSettings);
                        }
                    }
                    
                    // Target values from ECS (mouse/stick look)
                    float targetYaw = settings.Yaw;
                    float targetPitch = settings.Pitch;
                    
                    // Smooth transition when lock just broke (prevents flicker)
                    if (_lockBreakTransitionTimer > 0)
                    {
                        _lockBreakTransitionTimer -= Time.deltaTime;
                        float t = 1f - (_lockBreakTransitionTimer / LOCK_BREAK_TRANSITION_DURATION);
                        t = t * t * (3f - 2f * t); // Smoothstep for easing
                        
                        _currentYaw = Mathf.LerpAngle(_lockBreakStartYaw, targetYaw, t);
                        _currentPitch = Mathf.Lerp(_lockBreakStartPitch, targetPitch, t);
                    }
                    else
                    {
                        // Normal: use ECS values directly
                        _currentYaw = targetYaw;
                        _currentPitch = targetPitch;
                    }
                    
                    // Apply rotation to follow target - use consistent format with pitch
                    _followTarget.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
                }
                
                // Update FPS target
                _fpsTarget.position = _followTarget.position + (Vector3)settings.FPSOffset;
                _fpsTarget.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);

                // Write camera angles back to ECS so ghost replication sends live values
                // to non-owner clients (spectator camera reads these via PlayerCameraSettings).
                // Without this, the ghost component stays at defaults (Yaw=0, Pitch=25, Dist=8).
                settings.Yaw = _currentYaw;
                settings.Pitch = _currentPitch;
                settings.CurrentDistance = _currentDistance;
                em.SetComponentData(_playerEntity, settings);
            }

            // Get view type and apply offsets
            if (em.HasComponent<CameraViewConfig>(_playerEntity))
            {
                var viewConfig = em.GetComponentData<CameraViewConfig>(_playerEntity);
                _currentViewType = viewConfig.ActiveViewType;

                // Apply Pivot Offset (Height)
                // This allows raising the camera target (e.g. to head height) so player appears lower in frame
                float3 pivotOffset = (_currentViewType == CameraViewType.Combat) 
                                    ? viewConfig.CombatPivotOffset 
                                    : viewConfig.AdventurePivotOffset;
                
                // Add offset to the base transform position
                _followTarget.position += (Vector3)pivotOffset;
            }
        }
        
        // ============================================================
        // VIRTUAL CAMERA MANAGEMENT
        // ============================================================
        
        private void UpdateVirtualCameras()
        {
            bool useFPS = IsFirstPerson;
            bool useIsometric = IsIsometric;
            
            // Update priorities for camera switching
            if (_thirdPersonCamera != null)
            {
                _thirdPersonCamera.Priority = (useFPS || useIsometric) ? _inactivePriority : _activePriority;
            }
            
            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.Priority = useFPS ? _activePriority : _inactivePriority;
            }
            
            if (_isometricCamera != null)
            {
                _isometricCamera.Priority = useIsometric ? _activePriority : _inactivePriority;
            }
            
            // EPIC 15.20: Sync camera yaw to ParadigmSettings singleton
            // This is critical for screen-relative WASD movement in ANY camera mode
            // Uses the singleton pattern (works without netcode player entity)
            if (_clientWorld != null && _clientWorld.IsCreated)
            {
                SyncCameraYawToSingleton(_clientWorld.EntityManager, useIsometric);
            }
            
            // Update third-person camera distance and orbit
            if (_tpFollow != null && !useFPS && !useIsometric)
            {
                _tpFollow.CameraDistance = Mathf.Clamp(_currentDistance, _minDistance, _maxDistance);
                _tpFollow.CameraSide = 0.5f; // Centered — all lateral offset via ShoulderOffset.x

                // Sync shoulder offset: inspector base + dynamic OTS offset from ECS CombatCameraOffset.x
                var shoulder = _shoulderOffset;
                if (_clientWorld != null && _clientWorld.IsCreated &&
                    _playerEntity != Entity.Null && _clientWorld.EntityManager.Exists(_playerEntity) &&
                    _clientWorld.EntityManager.HasComponent<CameraViewConfig>(_playerEntity))
                {
                    var viewConfig = _clientWorld.EntityManager.GetComponentData<CameraViewConfig>(_playerEntity);
                    shoulder.x += viewConfig.CombatCameraOffset.x;
                }
                _tpFollow.ShoulderOffset = shoulder;
                
                // For ThirdPersonFollow, we control orbit via the follow target rotation
                // When locked on, UpdateFromECS already set the rotation - don't overwrite it
                if (!_isLockedOn)
                {
                    var targetRot = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
                    _followTarget.rotation = targetRot;
                }
            }
            else if (!useFPS && !useIsometric)
            {
                // Fallback: still apply rotation to follow target for orbit
                if (!_isLockedOn)
                {
                    _followTarget.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
                }
            }
            
            // Update FPS PanTilt angles (CM3 uses PanTilt instead of POV)
            if (_fpsPanTilt != null && useFPS)
            {
                // Drive the PanTilt directly from ECS values
                _fpsPanTilt.PanAxis.Value = _currentYaw;
                _fpsPanTilt.TiltAxis.Value = -_currentPitch; // Inverted for natural feel
            }
            else if (useFPS && _fpsTarget != null)
            {
                // Fallback: drive FPS target rotation directly
                _fpsTarget.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            }
        }
        
        // ============================================================
        // PUBLIC API
        // ============================================================
        
        /// <summary>
        /// Force switch to a specific view type.
        /// </summary>
        public void SetViewType(CameraViewType viewType)
        {
            _currentViewType = viewType;
            
            // Write back to ECS if needed
            if (_clientWorld != null && _clientWorld.IsCreated && 
                _clientWorld.EntityManager.Exists(_playerEntity) &&
                _clientWorld.EntityManager.HasComponent<CameraViewConfig>(_playerEntity))
            {
                var config = _clientWorld.EntityManager.GetComponentData<CameraViewConfig>(_playerEntity);
                config.ActiveViewType = viewType;
                _clientWorld.EntityManager.SetComponentData(_playerEntity, config);
            }
        }
        
        /// <summary>
        /// Set camera distance (zoom level).
        /// </summary>
        public void SetDistance(float distance)
        {
            _currentDistance = Mathf.Clamp(distance, 0f, _maxDistance);
        }
        
        /// <summary>
        /// Set camera yaw and pitch angles.
        /// </summary>
        public void SetRotation(float yaw, float pitch)
        {
            _currentYaw = yaw;
            _currentPitch = Mathf.Clamp(pitch, -89f, 89f);
        }
        
        /// <summary>
        /// Trigger camera shake via Cinemachine Impulse.
        /// </summary>
        public void TriggerShake(float force, Vector3 velocity = default)
        {
            if (velocity == default)
            {
                velocity = UnityEngine.Random.insideUnitSphere;
            }
            
            // Find or create impulse source
            var impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(velocity * force);
            }
        }
        
        /// <summary>
        /// Get the currently active Cinemachine camera.
        /// </summary>
        public CinemachineCamera GetActiveCamera()
        {
            return _currentCameraMode switch
            {
                CinemachineCameraMode.FirstPerson => _firstPersonCamera,
                CinemachineCameraMode.Isometric => _isometricCamera,
                _ => _thirdPersonCamera
            };
        }
        
        // ============================================================
        // CAMERA MODE SWITCHING
        // ============================================================
        
        /// <summary>
        /// Switch to a different camera mode (ThirdPerson, FirstPerson, Isometric).
        /// Uses Cinemachine blending for smooth transitions.
        /// </summary>
        public void SetCameraMode(CinemachineCameraMode mode)
        {
            if (_currentCameraMode == mode) return;
            
            // Check if requested camera exists
            if (mode == CinemachineCameraMode.Isometric && _isometricCamera == null)
            {
                Debug.LogError("[CinemachineCameraController] Cannot switch to Isometric - no isometric camera assigned! " +
                    "Use menu DIG → Camera → Create Cinemachine Camera Rig to create one with isometric camera.");
                return;
            }
            if (mode == CinemachineCameraMode.FirstPerson && _firstPersonCamera == null)
            {
                Debug.LogError("[CinemachineCameraController] Cannot switch to FirstPerson - no first person camera assigned!");
                return;
            }
            if (mode == CinemachineCameraMode.ThirdPerson && _thirdPersonCamera == null)
            {
                Debug.LogError("[CinemachineCameraController] Cannot switch to ThirdPerson - no third person camera assigned!");
                return;
            }
            
            var previousMode = _currentCameraMode;
            _currentCameraMode = mode;
            
            // Update virtual camera priorities
            UpdateCameraPriorities();
            
            // Notify CameraModeProvider of the change
            NotifyCameraModeProvider();
            
            // Fire event for UI and other listeners
            OnCameraModeChanged?.Invoke(previousMode, mode);
            
            Debug.Log($"[CinemachineCameraController] Camera mode: {previousMode} → {mode}");
        }
        
        /// <summary>
        /// Switch to isometric mode for ARPG/MOBA/TwinStick paradigms.
        /// </summary>
        public void SetIsometricMode()
        {
            SetCameraMode(CinemachineCameraMode.Isometric);
        }
        
        /// <summary>
        /// Switch to third-person mode for Shooter/MMO paradigms.
        /// </summary>
        public void SetThirdPersonMode()
        {
            SetCameraMode(CinemachineCameraMode.ThirdPerson);
        }
        
        /// <summary>
        /// Switch to first-person mode.
        /// </summary>
        public void SetFirstPersonMode()
        {
            SetCameraMode(CinemachineCameraMode.FirstPerson);
        }
        
        private void UpdateCameraPriorities()
        {
            // Set priorities: active camera gets high priority, others get low
            if (_thirdPersonCamera != null)
            {
                _thirdPersonCamera.Priority = _currentCameraMode == CinemachineCameraMode.ThirdPerson 
                    ? _activePriority : _inactivePriority;
            }
            
            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.Priority = _currentCameraMode == CinemachineCameraMode.FirstPerson 
                    ? _activePriority : _inactivePriority;
            }
            
            if (_isometricCamera != null)
            {
                _isometricCamera.Priority = _currentCameraMode == CinemachineCameraMode.Isometric 
                    ? _activePriority : _inactivePriority;
            }
        }
        
        /// <summary>
        /// EPIC 15.20: Sync camera yaw to ParadigmSettings singleton.
        /// This allows screen-relative movement to work in ANY camera mode.
        /// </summary>
        private void SyncCameraYawToSingleton(EntityManager em, bool useIsometric)
        {
            // EPIC 15.20: Use cached query (created once, reused every frame)
            if (_paradigmSettingsQuery == default)
            {
                _paradigmSettingsQuery = em.CreateEntityQuery(ComponentType.ReadWrite<ParadigmSettings>());
            }
            if (_paradigmSettingsQuery.IsEmpty) return;
            
            var singletonEntity = _paradigmSettingsQuery.GetSingletonEntity();
            var settings = em.GetComponentData<ParadigmSettings>(singletonEntity);
            
            // Only sync if screen-relative movement is enabled
            if (!settings.UseScreenRelativeMovement) return;
            
            // Use the appropriate yaw based on camera mode:
            // - Isometric mode: read actual camera yaw so movement/facing matches the visual camera
            //   (the serialized _isometricYaw can mismatch the Cinemachine camera's actual orientation)
            // - Third-person mode: use _currentYaw (camera orbits with mouse)
            float yawToSync;
            if (useIsometric)
            {
                var cam = Camera.main;
                yawToSync = cam != null ? cam.transform.eulerAngles.y : _isometricYaw;
            }
            else
            {
                yawToSync = _currentYaw;
            }
            
            // Update camera yaw
            settings.CameraYaw = yawToSync;
            em.SetComponentData(singletonEntity, settings);
        }
        
        private void NotifyCameraModeProvider()
        {
            if (!CameraModeProvider.HasInstance) return;
            
            // Map CinemachineCameraMode to CameraMode enum
            CameraMode cameraMode = _currentCameraMode switch
            {
                CinemachineCameraMode.FirstPerson => CameraMode.FirstPerson,
                CinemachineCameraMode.Isometric => CameraMode.IsometricFixed,
                _ => CameraMode.ThirdPersonFollow
            };
            
            // Create or update the adapter with current mode settings
            if (_cameraModeAdapter == null)
            {
                _cameraModeAdapter = new CinemachineCameraModeAdapter(this);
            }
            _cameraModeAdapter.UpdateMode(cameraMode);
            
            // Register with CameraModeProvider so other systems can query camera capabilities
            CameraModeProvider.Instance.SetActiveCamera(_cameraModeAdapter);
            
            Debug.Log($"[CinemachineCameraController] Notified CameraModeProvider: mode={cameraMode}, UsesCursorAiming={_cameraModeAdapter.UsesCursorAiming}, SupportsOrbit={_cameraModeAdapter.SupportsOrbitRotation}");
        }
        
        /// <summary>
        /// Check if isometric camera is available.
        /// </summary>
        public bool HasIsometricCamera => _isometricCamera != null;

        // ============================================================
        // EPIC 18.13: Authority override helpers
        // ============================================================

        private bool _vcamsDisabledByOverride;

        /// <summary>
        /// Disables all virtual camera priorities so Cinemachine Brain
        /// doesn't fight the override camera driving Camera.main.
        /// </summary>
        private void DisableAllVirtualCameras()
        {
            if (_vcamsDisabledByOverride) return;
            _vcamsDisabledByOverride = true;

            if (_thirdPersonCamera != null) _thirdPersonCamera.Priority = -1;
            if (_firstPersonCamera != null) _firstPersonCamera.Priority = -1;
            if (_isometricCamera != null) _isometricCamera.Priority = -1;
        }

        /// <summary>
        /// Restores virtual camera priorities after override ends.
        /// </summary>
        private void RestoreVirtualCameraPriorities()
        {
            if (!_vcamsDisabledByOverride) return;
            _vcamsDisabledByOverride = false;

            // UpdateVirtualCameras (called right after) will set correct priorities
        }
    }
    
    /// <summary>
    /// Lightweight adapter that wraps CinemachineCameraController for ICameraMode compatibility.
    /// Provides mode-related properties to CameraModeProvider without full ICameraMode implementation.
    /// </summary>
    internal class CinemachineCameraModeAdapter : ICameraMode
    {
        private readonly CinemachineCameraController _controller;
        private CameraMode _currentMode;
        
        public CinemachineCameraModeAdapter(CinemachineCameraController controller)
        {
            _controller = controller;
            _currentMode = CameraMode.ThirdPersonFollow;
        }
        
        public void UpdateMode(CameraMode mode)
        {
            _currentMode = mode;
        }
        
        // ICameraMode implementation - mode properties are the important ones
        public CameraMode Mode => _currentMode;
        
        public bool SupportsOrbitRotation => _currentMode == CameraMode.ThirdPersonFollow || _currentMode == CameraMode.FirstPerson;
        
        public bool UsesCursorAiming => _currentMode == CameraMode.IsometricFixed || 
                                         _currentMode == CameraMode.IsometricRotatable || 
                                         _currentMode == CameraMode.TopDownFixed;
        
        // Delegate to controller where possible, or provide reasonable defaults
        public Transform GetCameraTransform() => Camera.main?.transform;
        
        public Plane GetAimPlane()
        {
            if (UsesCursorAiming)
            {
                // Ground plane for isometric
                return new Plane(Vector3.up, Vector3.zero);
            }
            else
            {
                // Camera-facing plane for third-person
                var cam = Camera.main;
                if (cam != null)
                    return new Plane(-cam.transform.forward, cam.transform.position + cam.transform.forward * 10f);
                return new Plane(Vector3.forward, Vector3.zero);
            }
        }
        
        public float3 TransformMovementInput(float2 input)
        {
            // Delegate movement transformation to controller or provide default
            var cam = Camera.main;
            if (cam == null) return new float3(input.x, 0, input.y);
            
            if (UsesCursorAiming)
            {
                // Screen-relative for isometric (based on camera rotation)
                var camForward = cam.transform.forward;
                var camRight = cam.transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();
                
                var movement = camRight * input.x + camForward * input.y;
                return new float3(movement.x, 0, movement.z);
            }
            else
            {
                // Camera-relative for third-person
                var forward = cam.transform.forward;
                var right = cam.transform.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();
                
                var movement = right * input.x + forward * input.y;
                return new float3(movement.x, 0, movement.z);
            }
        }
        
        public float3 TransformAimInput(float2 cursorScreenPos)
        {
            var cam = Camera.main;
            if (cam == null) return float3.zero;
            
            var ray = cam.ScreenPointToRay(new Vector3(cursorScreenPos.x, cursorScreenPos.y, 0));
            var plane = GetAimPlane();
            
            if (plane.Raycast(ray, out float distance))
            {
                return (float3)ray.GetPoint(distance);
            }
            
            return (float3)ray.GetPoint(10f);
        }
        
        // These methods are less critical - provide minimal implementations
        public void Initialize(CameraConfig config) { }
        public void UpdateCamera(float deltaTime) { }
        public void SetTarget(Entity entity, Transform visualTransform = null) { }
        public void SetZoom(float zoomLevel) { } // Zoom handled directly by controller
        public float GetZoom() => 0.5f; // Default mid-zoom
        public void Shake(float intensity, float duration) => _controller?.TriggerShake(intensity);
        public void HandleRotationInput(float2 rotationInput) { } // Rotation handled directly by controller
    }
}
