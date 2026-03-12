using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using Unity.Transforms;
using UnityEngine;
using Player.Animation;
using Player.Components;

/// <summary>
/// Simple camera that directly follows the local player entity.
/// No ECS components needed - just reads the player's LocalTransform.
/// 
/// EPIC 14.18: This component now acts as a FALLBACK when Cinemachine is not active.
/// If CinemachineCameraController is present and initialized, this component defers to it.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Unity Camera to control. If null, uses Camera.main")]
    public Camera TargetCamera;
    
    [Header("Camera Settings")]
    [Tooltip("Camera offset from player (x, y, z)")]
    public Vector3 CameraOffset = new Vector3(0, 2, -5);
    
    [Tooltip("Position interpolation speed (0 = instant, higher = smoother)")]
    [Range(0f, 50f)]
    public float PositionSmoothing = 10f;
    
    [Tooltip("Rotation interpolation speed (0 = instant, higher = smoother)")]
    [Range(0f, 50f)]
    public float RotationSmoothing = 15f;

    [Header("Optional Cinemachine")]
    [Tooltip("Optional: assign a Cinemachine Virtual Camera GameObject. Integration is reflection-based so Cinemachine package is not required at compile time.")]
    public GameObject CinemachineVirtualCameraObject;
    
    [Tooltip("If true, this component will be disabled when CinemachineCameraController is detected.")]
    public bool DeferToCinemachine = true;

    [Header("Collision / Occlusion")]
    [Tooltip("Enable simple camera collision (spherecast between pivot and camera).")]
    public bool EnableCollision = true;

    [Tooltip("Radius used for collision spherecast (meters)")]
    public float CollisionRadius = 0.25f;

    [Tooltip("Layer mask for camera collision checks")] 
    public LayerMask CollisionMask = ~0;
    
    // Internal state
    private World _clientWorld;
    private Entity _playerEntity;

    // Cached queries (reuse instead of creating every frame)
    private EntityQuery _playerQueryWithOwner;
    private EntityQuery _playerQueryFallback;

    // Cinemachine detection
    private bool _cinemachineDetected;
    private int _cinemachineCheckFrames;

    // Ragdoll tracking for camera follow during death
    private RagdollPresentationBridge _cachedRagdollBridge;
    private Entity _cachedEntity;

    // EPIC 18.13: CinemachineBrain suppression during authority override
    private MonoBehaviour _cachedCinemachineBrain;
    private bool _brainWasEnabled;
    private bool _brainSuppressed;
    
    private void Awake()
    {
        // Get or find the camera
        if (TargetCamera == null)
        {
            TargetCamera = Camera.main;
            if (TargetCamera == null)
            {
                TargetCamera = GetComponent<Camera>();
            }
        }
        
        if (TargetCamera == null)
        {
            Debug.LogError("CameraManager: No Camera found!");
            enabled = false;
            return;
        }
        
        // Make sure we run in background so camera updates even when tabbed out
        Application.runInBackground = true;
    }
    
    private int _lateUpdateFrameCounter = 0;
    // Cinemachine reflection cache
    private object _cinemachineVcamInstance = null;
    private Transform _cinemachineTargetTransform = null;
    private bool _cinemachineWired = false;

    private void LateUpdate()
    {
        // EPIC 18.13: When authority is overridden (death cam, cutscene, etc.),
        // skip normal player-follow logic but still drive Camera.main from the
        // active ICameraMode so death/spectator cameras actually render.
        if (DIG.DeathCamera.CameraAuthorityGate.IsOverridden)
        {
            // Suppress CinemachineBrain so it doesn't overwrite our transform
            SuppressCinemachineBrain();

            bool hasProvider = DIG.CameraSystem.CameraModeProvider.HasInstance;
            DIG.CameraSystem.ICameraMode overrideCam = hasProvider ? DIG.CameraSystem.CameraModeProvider.Instance.ActiveCamera : null;
            Transform camTransform = overrideCam?.GetCameraTransform();

            if (overrideCam != null && TargetCamera != null && camTransform != null)
            {
                TargetCamera.transform.SetPositionAndRotation(
                    camTransform.position, camTransform.rotation);

                // Maintain FOV from the death camera (prevents FOV mismatch
                // when Cinemachine virtual camera had a different lens FOV)
                if (overrideCam is DIG.DeathCamera.DeathFollowCam followCam && followCam.TargetFOV > 0f)
                {
                    TargetCamera.fieldOfView = followCam.TargetFOV;
                }
            }

            _lateUpdateFrameCounter++;
            return;
        }

        // Re-enable CinemachineBrain if it was suppressed by authority override
        if (_brainSuppressed)
        {
            Debug.Log("[DCam] CameraManager: authority released — restoring CinemachineBrain");
        }
        RestoreCinemachineBrain();

        // EPIC 14.18: Check if Cinemachine controller is handling camera
        if (DeferToCinemachine && CheckCinemachineActive())
        {
            // Cinemachine is handling camera - do nothing
            return;
        }
        
        // Find the client world (cached after first find)
        if (_clientWorld == null || !_clientWorld.IsCreated)
        {
            _clientWorld = FindClientWorld();
            if (_clientWorld == null) return;

            // Reset player entity and queries when world changes
            _playerEntity = Entity.Null;
            _playerQueryWithOwner = default;
            _playerQueryFallback = default;
        }

        // Validate we have the correct world (not LocalWorld)
        if (_clientWorld.Name == "LocalWorld")
        {
            DebugLog.LogCameraWarning("Found LocalWorld instead of ClientWorld, retrying...");
            _clientWorld = null;
            return;
        }

        // Find the local player entity (cached after first find)
        if (_playerEntity == Entity.Null || !_clientWorld.EntityManager.Exists(_playerEntity))
        {
            _playerEntity = FindLocalPlayerEntity(_clientWorld);
            if (_playerEntity == Entity.Null) return;
        }

        // Update camera to follow player (runs every frame - this is the hot path)
        UpdateCameraPosition(_clientWorld, _playerEntity);

        // If we have a Cinemachine vcam assigned but the package wasn't present at compile-time,
        // we attempt to wire its Follow/LookAt properties via reflection the first time.
        TryWireCinemachine();

        _lateUpdateFrameCounter++;
    }
    
    /// <summary>
    /// EPIC 14.18: Check if CinemachineCameraController is active and handling camera.
    /// </summary>
    private bool CheckCinemachineActive()
    {
        // Only check periodically to avoid overhead
        if (_cinemachineCheckFrames++ < 60 && _cinemachineDetected)
        {
            return true;
        }
        _cinemachineCheckFrames = 0;
        
        // Check for CinemachineCameraController via reflection to avoid hard dependency
        var controllerType = System.Type.GetType("DIG.CameraSystem.Cinemachine.CinemachineCameraController, Assembly-CSharp");
        if (controllerType != null)
        {
            var hasInstanceProp = controllerType.GetProperty("HasInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (hasInstanceProp != null)
            {
                _cinemachineDetected = (bool)hasInstanceProp.GetValue(null);
                if (_cinemachineDetected && _lateUpdateFrameCounter == 0)
                {
                    Debug.Log("[CameraManager] CinemachineCameraController detected - deferring camera control.");
                }
                return _cinemachineDetected;
            }
        }
        
        // Also check for CinemachineBrain on the camera (Supports both Unity.Cinemachine and Legacy Cinemachine)
        if (TargetCamera != null)
        {
            // Check for CM 3.x (Unity.Cinemachine)
            var brain = TargetCamera.GetComponent("Unity.Cinemachine.CinemachineBrain");
            if (brain != null)
            {
                var enabledProp = brain.GetType().GetProperty("enabled");
                if (enabledProp != null && (bool)enabledProp.GetValue(brain))
                {
                    _cinemachineDetected = true;
                    return true;
                }
            }
            
            // Check for CM 2.x (Cinemachine)
            var legacyBrain = TargetCamera.GetComponent("Cinemachine.CinemachineBrain");
            if (legacyBrain != null)
            {
                var enabledProp = legacyBrain.GetType().GetProperty("enabled");
                if (enabledProp != null && (bool)enabledProp.GetValue(legacyBrain))
                {
                    _cinemachineDetected = true;
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Finds the client world
    /// </summary>
    private World FindClientWorld()
    {
        DebugLog.LogCamera("Searching for ClientWorld...");

        // List all worlds for debugging
        foreach (var world in World.All)
        {
            if (world.IsCreated)
            {
                DebugLog.LogCamera($"  - World: {world.Name}, Flags: {world.Flags}");
            }
        }

        // Look for the ClientWorld specifically by name (most reliable)
        foreach (var world in World.All)
        {
            if (world.IsCreated && world.Name == "ClientWorld")
            {
                DebugLog.LogCamera($"Found client world by name: {world.Name}");
                return world;
            }
        }

        // Fallback: any world with GameClient flag (but not LocalWorld)
        foreach (var world in World.All)
        {
            if (world.IsCreated &&
                world.Name != "LocalWorld" &&
                (world.Flags & WorldFlags.GameClient) != 0)
            {
                DebugLog.LogCamera($"Found client world (fallback): {world.Name}");
                return world;
            }
        }

        DebugLog.LogCameraWarning("No ClientWorld found!");
        return null;
    }
    
    /// <summary>
    /// Finds the local player entity (has Player tag + GhostOwnerIsLocal or just Player in single player)
    /// </summary>
    private Entity FindLocalPlayerEntity(World world)
    {
        var entityManager = world.EntityManager;

        // Create queries once and cache them (performance optimization)
        if (_playerQueryWithOwner == default)
        {
            _playerQueryWithOwner = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>()
            );
        }

        if (_playerQueryFallback == default)
        {
            _playerQueryFallback = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>()
            );
        }

        // Try to find player with GhostOwnerIsLocal first (proper multiplayer)
        if (!_playerQueryWithOwner.IsEmpty)
        {
            var entities = _playerQueryWithOwner.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length > 0)
            {
                var result = entities[0];
                entities.Dispose();
                DebugLog.LogCamera($"Found local player with GhostOwnerIsLocal: {result}");
                LogEntityComponents(entityManager, result);
                return result;
            }
            entities.Dispose();
        }

        // Fallback: Just find any Player (works before GhostOwnerIsLocal is assigned)
        if (!_playerQueryFallback.IsEmpty)
        {
            var entities = _playerQueryFallback.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length > 0)
            {
                var result = entities[0];
                entities.Dispose();
                DebugLog.LogCamera($"Found player (fallback): {result}");
                LogEntityComponents(entityManager, result);
                return result;
            }
            entities.Dispose();
        }

        return Entity.Null;
    }

    /// <summary>
    /// Logs all components on an entity for debugging
    /// </summary>
    private void LogEntityComponents(EntityManager entityManager, Entity entity)
    {
        var componentTypes = entityManager.GetComponentTypes(entity, Unity.Collections.Allocator.Temp);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Components on Entity {entity}:");
        foreach (var componentType in componentTypes)
        {
            sb.AppendLine($"  - {componentType.GetManagedType().Name}");
        }
        DebugLog.LogCamera(sb.ToString());
        componentTypes.Dispose();
    }
    
    /// <summary>
    /// Updates camera position to follow the player
    /// Reads from CameraTarget if available (set by PlayerCameraControlSystem),
    /// otherwise falls back to simple offset from player position.
    /// During ragdoll, follows the ragdoll's hips position instead.
    /// </summary>
    private void UpdateCameraPosition(World world, Entity playerEntity)
    {
        var entityManager = world.EntityManager;

        // === RAGDOLL CAMERA OVERRIDE ===
        // When ragdolled, follow the ragdoll hips instead of ECS entity
        float3 ragdollPositionOverride = float3.zero;
        bool useRagdollPosition = false;
        
        if (TryGetRagdollPosition(entityManager, playerEntity, out ragdollPositionOverride))
        {
            useRagdollPosition = true;
        }

        // Prefer camera target coming from PlayerCameraControlSystem (MMO orbit camera)
        if (entityManager.HasComponent<CameraTarget>(playerEntity))
        {
            var cameraTarget = entityManager.GetComponentData<CameraTarget>(playerEntity);

            float3 desiredPosition = cameraTarget.Position;
            quaternion desiredRotation = cameraTarget.Rotation;
            
            // Override camera position to follow ragdoll if active
            if (useRagdollPosition)
            {
                // Calculate camera position relative to ragdoll instead of entity
                float3 ragdollOffset = ragdollPositionOverride - (float3)entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
                desiredPosition += ragdollOffset;
            }

            float3 smoothedPosition = TargetCamera.transform.position;
            quaternion smoothedRotation = TargetCamera.transform.rotation;

            float posLerp = math.saturate(Time.deltaTime * PositionSmoothing);
            float rotLerp = math.saturate(Time.deltaTime * RotationSmoothing);

            smoothedPosition = PositionSmoothing > 0
                ? math.lerp(smoothedPosition, desiredPosition, posLerp)
                : desiredPosition;

            smoothedRotation = RotationSmoothing > 0
                ? math.slerp(smoothedRotation, desiredRotation, rotLerp)
                : desiredRotation;

            // Apply simple collision/occlusion handling (spherecast from pivot into desired camera position)
            if (EnableCollision)
            {
                // Use cameraTarget.Position as the pivot (design choice: cameraTarget.Position is often the pivot/effective focus)
                var pivot = (Vector3)cameraTarget.Position;
                var desiredWorld = (Vector3)desiredPosition;
                var dir = desiredWorld - pivot;
                var dist = dir.magnitude;
                if (dist > 0.001f)
                {
                    var rayDir = dir / dist;
                    RaycastHit hit;
                    if (Physics.SphereCast(pivot, CollisionRadius, rayDir, out hit, dist, CollisionMask.value))
                    {
                        // Move camera to the hit point but keep a small offset so it doesn't clip into the surface
                        smoothedPosition = Vector3.Lerp((Vector3)smoothedPosition, hit.point - rayDir * 0.1f, 0.9f);
                    }
                }
            }

            // Apply final transform (or route through Cinemachine target if wired)
            if (_cinemachineWired && _cinemachineTargetTransform != null)
            {
                _cinemachineTargetTransform.SetPositionAndRotation((Vector3)smoothedPosition, (Quaternion)smoothedRotation);
            }
            else
            {
                TargetCamera.transform.SetPositionAndRotation(smoothedPosition, smoothedRotation);
            }

            // Apply camera shake if requested on the player entity
            ApplyShakeIfPresent(world, playerEntity);

            if (cameraTarget.FOV > 0)
            {
                TargetCamera.fieldOfView = cameraTarget.FOV;
            }

            if (cameraTarget.NearClip > 0)
            {
                TargetCamera.nearClipPlane = cameraTarget.NearClip;
            }

            if (cameraTarget.FarClip > 0)
            {
                TargetCamera.farClipPlane = cameraTarget.FarClip;
            }

            if (_lateUpdateFrameCounter % 120 == 0)
            {
                DebugLog.LogCamera($"[CameraManager] Using CameraTarget | Player: {playerEntity} | Desired: {desiredPosition} | Applied: {smoothedPosition}");
            }

            return;
        }

        // Get player's transform for fallback behavior
        if (!entityManager.HasComponent<LocalTransform>(playerEntity))
        {
            DebugLog.LogCameraWarning("CameraManager: Player entity doesn't have LocalTransform!");
            return;
        }

        var playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
        float3 playerPos = playerTransform.Position;

        // Fallback: try PlayerCameraSettings to reconstruct target
        if (entityManager.HasComponent<PlayerCameraSettings>(playerEntity))
        {
            var cameraSettings = entityManager.GetComponentData<PlayerCameraSettings>(playerEntity);

            float3 pivotPosition = playerPos + cameraSettings.PivotOffset;
            float3 cameraPosition;
            quaternion cameraRotation;

            if (cameraSettings.CurrentDistance < 0.01f)
            {
                cameraPosition = playerPos + cameraSettings.FPSOffset;
                cameraRotation = quaternion.Euler(math.radians(cameraSettings.Pitch), math.radians(cameraSettings.Yaw), 0);
            }
            else
            {
                quaternion rotation = quaternion.Euler(math.radians(cameraSettings.Pitch), math.radians(cameraSettings.Yaw), 0);
                float3 direction = math.mul(rotation, new float3(0, 0, -1));
                cameraPosition = pivotPosition + (direction * cameraSettings.CurrentDistance);

                float3 forward = math.normalize(pivotPosition - cameraPosition);
                float3 up = new float3(0, 1, 0);
                if (math.lengthsq(forward) > 0.0001f)
                {
                    float3 right = math.normalize(math.cross(up, forward));
                    up = math.cross(forward, right);
                    cameraRotation = quaternion.LookRotation(forward, up);
                }
                else
                {
                    cameraRotation = quaternion.identity;
                }
            }

            float3 currentPosition = TargetCamera.transform.position;
            quaternion currentRotation = TargetCamera.transform.rotation;

            float posLerp = math.saturate(Time.deltaTime * PositionSmoothing);
            float rotLerp = math.saturate(Time.deltaTime * RotationSmoothing);

            currentPosition = PositionSmoothing > 0
                ? math.lerp(currentPosition, cameraPosition, posLerp)
                : cameraPosition;

            currentRotation = RotationSmoothing > 0
                ? math.slerp(currentRotation, cameraRotation, rotLerp)
                : cameraRotation;

            TargetCamera.transform.SetPositionAndRotation(currentPosition, currentRotation);

            if (_lateUpdateFrameCounter % 120 == 0)
            {
                DebugLog.LogCamera($"[CameraManager] Rebuilt from PlayerCameraSettings | Player: {playerEntity} | Camera: {cameraPosition} | Applied: {currentPosition}");
            }

            // Apply camera shake if requested on the player entity
            ApplyShakeIfPresent(world, playerEntity);
        }
        else
        {
            // Final fallback: simple offset follow camera
            float3 targetPosition = playerPos + (float3)CameraOffset;
            float3 currentPosition = TargetCamera.transform.position;

            float posLerp = math.saturate(Time.deltaTime * PositionSmoothing);

            currentPosition = PositionSmoothing > 0
                ? math.lerp(currentPosition, targetPosition, posLerp)
                : targetPosition;


            float3 lookDirection = math.normalize(playerPos - currentPosition);
            quaternion targetRotation = quaternion.LookRotation(lookDirection, math.up());
            quaternion currentRotation = TargetCamera.transform.rotation;

            float rotLerp = math.saturate(Time.deltaTime * RotationSmoothing);

            currentRotation = RotationSmoothing > 0
                ? math.slerp(currentRotation, targetRotation, rotLerp)
                : targetRotation;

            TargetCamera.transform.position = currentPosition;

            // Apply camera shake if requested on the player entity
            ApplyShakeIfPresent(world, playerEntity);

            TargetCamera.transform.rotation = currentRotation;

            if (_lateUpdateFrameCounter % 120 == 0)
            {
                DebugLog.LogCameraWarning($"[CameraManager] Using SIMPLE FALLBACK | Player: {playerPos} | Camera: {currentPosition}");
            }
        }
    }

    /// <summary>
    /// If the player entity has a CameraShake component, apply a short-lived shake to the Unity camera.
    /// This method mutates the CameraShake component to advance the timer and decay amplitude.
    /// </summary>
    private void ApplyShakeIfPresent(World world, Entity playerEntity)
    {
        var em = world.EntityManager;
        if (!em.HasComponent<CameraShake>(playerEntity)) return;

        var cs = em.GetComponentData<CameraShake>(playerEntity);
        if (cs.Amplitude <= 0f) return;

        // Advance timer
        cs.Timer += Time.deltaTime;

        // Deterministic multi-axis sine-based shake using Timer and Frequency.
        // This produces reproducible, smooth oscillations rather than random jitter.

        // Seed based on player entity index for per-entity deterministic variation
        float seed = (playerEntity.Index % 1000) * 0.001f; // in range [0,1)

        // Use Perlin noise for smooth, natural-looking shake. Map Timer*freq + offsets into Perlin.
        float t = cs.Timer * cs.Frequency;

        float px = Mathf.PerlinNoise(t + seed, seed + 0.1f) * 2f - 1f; // [-1,1]
        float py = Mathf.PerlinNoise(t + seed + 10f, seed + 0.2f) * 2f - 1f;
        float pz = Mathf.PerlinNoise(t + seed + 20f, seed + 0.3f) * 2f - 1f;

        var jitter = new UnityEngine.Vector3(px * cs.Amplitude, py * cs.Amplitude * 0.6f, pz * cs.Amplitude * 0.4f);

        // Apply jitter to camera position (post-smoothed)
        TargetCamera.transform.position = TargetCamera.transform.position + jitter;

        // Decay amplitude over time
        cs.Amplitude = Mathf.Max(0f, cs.Amplitude - cs.Decay * Time.deltaTime);

        em.SetComponentData(playerEntity, cs);
    }

    /// <summary>
    /// Try to wire a Cinemachine Virtual Camera (optional) using reflection so the project does
    /// not require Cinemachine at compile time. If a Cinemachine vcam GameObject is assigned,
    /// we create a small target transform that we update each frame and set it as the vcam's
    /// Follow/LookAt targets.
    /// </summary>
    private void TryWireCinemachine()
    {
        if (_cinemachineWired) return; // only attempt once
        if (CinemachineVirtualCameraObject == null)
        {
            _cinemachineWired = true;
            return;
        }

        // Create or find target transform used to drive Cinemachine
        if (_cinemachineTargetTransform == null)
        {
            var go = GameObject.Find("__CinemachineCameraTarget");
            if (go == null)
            {
                go = new GameObject("__CinemachineCameraTarget");
                DontDestroyOnLoad(go);
            }
            _cinemachineTargetTransform = go.transform;
        }

        // Search loaded assemblies for the Cinemachine virtual camera type
        System.Type vcamType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            vcamType = asm.GetType("Cinemachine.CinemachineVirtualCamera");
            if (vcamType != null) break;
        }

        if (vcamType == null)
        {
            // Cinemachine not present in loaded assemblies
            _cinemachineWired = true;
            return;
        }

        // Try to get component instance on assigned GameObject
        var comp = CinemachineVirtualCameraObject.GetComponent(vcamType);
        if (comp == null)
        {
            DebugLog.LogCameraWarning("CameraManager: Assigned Cinemachine object doesn't have a CinemachineVirtualCamera component.");
            _cinemachineWired = true;
            return;
        }

        _cinemachineVcamInstance = comp;

        // Set Follow and LookAt properties via reflection
        try
        {
            var followProp = vcamType.GetProperty("Follow");
            var lookAtProp = vcamType.GetProperty("LookAt");
            if (followProp != null) followProp.SetValue(_cinemachineVcamInstance, _cinemachineTargetTransform);
            if (lookAtProp != null) lookAtProp.SetValue(_cinemachineVcamInstance, _cinemachineTargetTransform);
            DebugLog.LogCamera("CameraManager: Wired Cinemachine VirtualCamera Follow/LookAt to internal target.");
        }
        catch (System.Exception ex)
        {
            DebugLog.LogCameraWarning($"CameraManager: Failed to wire Cinemachine vcam: {ex.Message}");
        }

        _cinemachineWired = true;
    }
    
    /// <summary>
    /// Checks if the player is ragdolled and returns the ragdoll position (hips).
    /// Uses cached reference to RagdollPresentationBridge for performance.
    /// </summary>
    private bool TryGetRagdollPosition(EntityManager entityManager, Entity playerEntity, out float3 ragdollPosition)
    {
        ragdollPosition = float3.zero;
        
        // Check if player has DeathState and is in a ragdoll-appropriate phase
        if (!entityManager.HasComponent<DeathState>(playerEntity))
            return false;
            
        var deathState = entityManager.GetComponentData<DeathState>(playerEntity);
        bool shouldBeRagdolled = deathState.Phase == DeathPhase.Downed || deathState.Phase == DeathPhase.Dead;
        
        if (!shouldBeRagdolled)
            return false;
        
        // Find the RagdollPresentationBridge - cache it for performance
        // FIX for Bug 2.8.3: Use GhostPresentationGameObjectSystem to find the specific bridge for THIS player
        if (_cachedRagdollBridge == null || _cachedEntity != playerEntity)
        {
            var world = entityManager.World;
            var presSystem = world.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            if (presSystem != null)
            {
                var go = presSystem.GetGameObjectForEntity(entityManager, playerEntity);
                if (go != null)
                {
                    _cachedRagdollBridge = go.GetComponent<RagdollPresentationBridge>();
                    _cachedEntity = playerEntity;
                }
            }
        }
        
        if (_cachedRagdollBridge != null && _cachedRagdollBridge.IsRagdolled)
        {
            ragdollPosition = _cachedRagdollBridge.RagdollPosition;
            return true;
        }
        
        return false;
    }

    // ============================================================
    // EPIC 18.13: CinemachineBrain suppression
    // ============================================================

    /// <summary>
    /// Disables the CinemachineBrain on TargetCamera so it doesn't
    /// overwrite Camera.main's transform during authority override.
    /// Uses reflection to avoid a hard compile-time dependency on Cinemachine.
    /// </summary>
    private void SuppressCinemachineBrain()
    {
        if (_brainSuppressed) return;

        if (_cachedCinemachineBrain == null && TargetCamera != null)
        {
            // Try CM 3.x first, then CM 2.x
            var brain = TargetCamera.GetComponent("Unity.Cinemachine.CinemachineBrain")
                     ?? TargetCamera.GetComponent("Cinemachine.CinemachineBrain");
            _cachedCinemachineBrain = brain as MonoBehaviour;
        }

        if (_cachedCinemachineBrain != null && _cachedCinemachineBrain.enabled)
        {
            _brainWasEnabled = true;
            _cachedCinemachineBrain.enabled = false;
            _brainSuppressed = true;
            Debug.Log($"[DCam] CinemachineBrain DISABLED ({_cachedCinemachineBrain.GetType().Name})");
        }
        else if (_cachedCinemachineBrain == null)
        {
            Debug.LogWarning("[DCam] SuppressCinemachineBrain: no brain found on TargetCamera");
        }
    }

    /// <summary>
    /// Re-enables the CinemachineBrain after authority override ends.
    /// </summary>
    private void RestoreCinemachineBrain()
    {
        if (!_brainSuppressed) return;

        if (_cachedCinemachineBrain != null && _brainWasEnabled)
        {
            _cachedCinemachineBrain.enabled = true;
        }

        _brainSuppressed = false;
        _brainWasEnabled = false;
    }
}
