using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Collections;
using Unity.NetCode;
using UnityEngine;
using Player.Components;
using DIG.Player.Components;
using DIG.Targeting.Components;
using DIG.Targeting.Core;
using DIG.CameraSystem.Cinemachine;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 15.16: Camera lock-on system with multiple lock behavior modes.
    /// 
    /// Lock Modes:
    /// - HardLock: Camera forced to follow target, minimal player control
    /// - SoftLock: Camera biased toward target, player retains some control
    /// - OverTheShoulder: Shoulder swap, ADS zoom, target tracking
    /// - FirstPerson: Aim magnetism only, no forced rotation
    /// - TwinStick: Independent aim from movement
    /// - Isometric: Fixed camera, character rotation only
    /// 
    /// Uses CrosshairData for modular target selection (defaults to screen center).
    /// 
    /// IMPORTANT: Runs in SimulationSystemGroup (once per frame, after prediction settles)
    /// to avoid input double-processing during prediction re-simulation.
    /// Runs BEFORE PlayerCameraControlSystem so it can set the Yaw/Pitch
    /// that PlayerCameraControlSystem will use to build the final CameraTarget.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedFixedStepSimulationSystemGroup))]
    public partial struct CameraLockOnSystem : ISystem
    {
        private EntityQuery _targetQuery;
        
        // Cooldown duration after soft lock break to prevent flicker
        // Actual cooldown stored per-entity in CameraTargetLockState.SoftLockBreakCooldown
        private const float SOFT_LOCK_BREAK_COOLDOWN_DURATION = 0.5f;
        
        // Camera arrival threshold (degrees) - when camera yaw/pitch is within this of target, 
        // phase transitions from Locking -> Locked
        public const float CAMERA_ARRIVAL_THRESHOLD_DEGREES = 5f;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
            _targetQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LockOnTarget>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            
            // Get active lock behavior from ECS singleton or use defaults
            ActiveLockBehavior behavior;
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out behavior))
            {
                behavior = new ActiveLockBehavior
                {
                    BehaviorType = LockBehaviorType.HardLock,
                    InputMode = LockInputMode.Hold, // Default to Hold (hold Tab to lock)
                    CharacterRotationStrength = 0.15f,
                    CameraTrackingSpeed = 720f,
                    MaxLockRange = 30f,
                    MaxLockAngle = 30f,
                    DefaultHeightOffset = 1.5f
                };
            }
            
            // OVERRIDE: If TargetingModeTester has set a mode, use it directly (bypasses ECS world sync issues)
            LockBehaviorType behaviorType = TargetingModeTester.StaticModeSet 
                ? TargetingModeTester.StaticCurrentMode 
                : behavior.BehaviorType;
            
            // EPIC 15.16: Check if this system should handle lock input
            // Use static overrides to bypass ECS world sync issues
            LockInputHandler inputHandler = TargetingModeTester.StaticModeSet
                ? TargetingModeTester.StaticInputHandler
                : behavior.InputHandler;
            bool handleInput = (inputHandler == LockInputHandler.CameraLockOnSystem);
            
            LockInputMode inputMode = TargetingModeTester.StaticModeSet
                ? TargetingModeTester.StaticInputMode
                : behavior.InputMode;
            float rotationStrength = behavior.CharacterRotationStrength;
            float trackingSpeed = behavior.CameraTrackingSpeed > 0 ? behavior.CameraTrackingSpeed : 720f;
            
            // Data-driven config values (EPIC 15.16 optimization)
            float maxLockRange = behavior.MaxLockRange > 0 ? behavior.MaxLockRange : 30f;
            float maxLockAngle = behavior.MaxLockAngle > 0 ? behavior.MaxLockAngle : 30f;
            float defaultHeightOffset = behavior.DefaultHeightOffset > 0 ? behavior.DefaultHeightOffset : 1.5f;
            
            // Adjust rotation strength based on mode
            switch (behaviorType)
            {
                case LockBehaviorType.HardLock:
                    rotationStrength = 1f; // Full camera control
                    break;
                case LockBehaviorType.SoftLock:
                    rotationStrength = 0.3f; // Partial bias
                    break;
                case LockBehaviorType.FirstPerson:
                case LockBehaviorType.TwinStick:
                    rotationStrength = 0f; // No camera override, aim assist only
                    break;
                case LockBehaviorType.OverTheShoulder:
                    rotationStrength = 0.5f; // Medium tracking
                    break;
                case LockBehaviorType.IsometricLock:
                    rotationStrength = 0f; // Camera doesn't move
                    break;
            }
            
            // Check if target locking is allowed
            bool allowTargetLock = true;
            if (SystemAPI.TryGetSingleton<TargetLockSettings>(out var settings))
            {
                allowTargetLock = settings.AllowTargetLock;
            }
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (lockState, transform, camTarget, input, entity) in 
                SystemAPI.Query<RefRW<CameraTargetLockState>, RefRO<LocalTransform>, RefRO<CameraTarget>, RefRO<PlayerInput>>()
                .WithAll<GhostOwnerIsLocal>()
                .WithEntityAccess())
            {
                // If target lock disabled, immediately release any lock
                if (!allowTargetLock && lockState.ValueRO.IsLocked)
                {
                    lockState.ValueRW.IsLocked = false;
                    lockState.ValueRW.TargetEntity = Entity.Null;
                    lockState.ValueRW.Phase = LockPhase.Unlocked;
                    continue;
                }
                
                // Get crosshair data if available (for aim-based targeting)
                float3 aimOrigin = camTarget.ValueRO.Position;
                float3 aimDir = math.mul(camTarget.ValueRO.Rotation, new float3(0, 0, 1));
                
                if (em.HasComponent<CrosshairData>(entity))
                {
                    var crosshair = em.GetComponentData<CrosshairData>(entity);
                    aimOrigin = crosshair.RayOrigin;
                    aimDir = crosshair.RayDirection;
                }
                
                // Handle input based on input mode
                // Only process input if this system is the designated handler
                bool shouldLock = false;
                bool shouldUnlock = false;
                
                // Simple edge detection - system now runs once per frame (SimulationSystemGroup)
                // so no need for frame tracking to avoid prediction re-simulation issues
                bool isGrabDown = input.ValueRO.Grab.IsSet;
                bool wasGrabDown = lockState.ValueRO.WasGrabPressed;
                bool pressed = isGrabDown && !wasGrabDown;
                bool released = !isGrabDown && wasGrabDown;
                lockState.ValueRW.WasGrabPressed = isGrabDown;
                
                // Debug: Log input state changes (filter: LOCKDEBUG)
                #if UNITY_EDITOR
                if (pressed || released)
                {
                    UnityEngine.Debug.Log($"[LOCKDEBUG] InputMode={inputMode} pressed={pressed} released={released} IsLocked={lockState.ValueRO.IsLocked} handleInput={handleInput}");
                }
                #endif
                
                // Only handle lock/unlock input if we're the designated handler
                if (handleInput)
                {
                    switch (inputMode)
                    {
                        case LockInputMode.Toggle:
                            if (pressed)
                            {
                                #if UNITY_EDITOR
                                UnityEngine.Debug.Log($"[LOCKDEBUG] Toggle: IsLocked={lockState.ValueRO.IsLocked} -> shouldLock={!lockState.ValueRO.IsLocked}");
                                #endif
                                if (lockState.ValueRO.IsLocked)
                                    shouldUnlock = true;
                                else
                                    shouldLock = true;
                            }
                            break;
                            
                        case LockInputMode.Hold:
                            if (pressed) shouldLock = true;
                            if (released) shouldUnlock = true;
                            #if UNITY_EDITOR
                            if (pressed || released)
                                UnityEngine.Debug.Log($"[LOCKDEBUG] Hold: pressed={pressed} released={released} shouldLock={shouldLock} shouldUnlock={shouldUnlock}");
                            #endif
                            break;
                            
                        case LockInputMode.AutoNearest:
                            // Always try to lock nearest
                            if (!lockState.ValueRO.IsLocked)
                                shouldLock = true;
                            break;
                            
                        case LockInputMode.ClickTarget:
                        case LockInputMode.HoverTarget:
                            // These require crosshair hit detection
                            if (pressed) shouldLock = true;
                            if (pressed && lockState.ValueRO.IsLocked) shouldUnlock = true;
                            break;
                    }
                }
                
                // EPIC 15.16: Soft Lock - any mouse/camera movement breaks the lock immediately
                // This gives the player initial camera tracking, but moving the mouse breaks it
                // Hard Lock is unaffected - camera stays locked regardless of mouse input
                #if UNITY_EDITOR
                // Debug: Log every 60 frames while locked (filter: SLDEBUG)
                if (lockState.ValueRO.IsLocked && UnityEngine.Time.frameCount % 60 == 0)
                {
                    float2 ld = input.ValueRO.LookDelta;
                    UnityEngine.Debug.Log($"[SLDEBUG] Mode={behaviorType} Look=({ld.x:F2},{ld.y:F2})");
                }
                #endif
                
                // Clear JustUnlocked flag from previous frame
                if (lockState.ValueRO.JustUnlocked)
                {
                    lockState.ValueRW.JustUnlocked = false;
                }
                
                // Decrement soft lock break cooldown (use visual frame time, not simulation time)
                if (lockState.ValueRO.SoftLockBreakCooldown > 0)
                {
                    lockState.ValueRW.SoftLockBreakCooldown -= UnityEngine.Time.deltaTime;
                }
                
                // EPIC 15.16: Camera Arrival Detection - query CinemachineCameraController
                // Transition from Locking -> Locked when camera has arrived at target
                // This is now done HERE (single update loop) instead of in CinemachineCameraController
                if (lockState.ValueRO.Phase == LockPhase.Locking)
                {
                    var cameraController = DIG.CameraSystem.Cinemachine.CinemachineCameraController.Instance;
                    if (cameraController != null && cameraController.HasCameraArrivedAtTarget)
                    {
                        lockState.ValueRW.Phase = LockPhase.Locked;
                        #if UNITY_EDITOR
                        UnityEngine.Debug.Log($"[LOCKDEBUG] CAMERA ARRIVED - Phase: Locking -> Locked (yawErr={cameraController.CurrentYawError:F1}°, pitchErr={cameraController.CurrentPitchError:F1}°)");
                        #endif
                    }
                }
                
                // SOFT LOCK BREAK: Only check if in LOCKED phase (camera has arrived)
                // Phase is now set above when CinemachineCameraController signals arrival
                if (behaviorType == LockBehaviorType.SoftLock && lockState.ValueRO.Phase == LockPhase.Locked)
                {
                    float2 lookDelta = input.ValueRO.LookDelta;
                    // Use raw magnitude (not squared) for intuitive threshold
                    float lookMagnitude = math.length(lookDelta);
                    
                    // Threshold: ~1.0 means intentional mouse movement, not micro-jitter
                    const float mouseThreshold = 1.0f;
                    if (lookMagnitude > mouseThreshold)
                    {
                        shouldUnlock = true;
                        lockState.ValueRW.SoftLockBreakCooldown = SOFT_LOCK_BREAK_COOLDOWN_DURATION; // Start cooldown
                        #if UNITY_EDITOR
                        UnityEngine.Debug.Log($"[SLDEBUG] BREAK mag={lookMagnitude:F3} Phase=Locked (camera arrived, break detection enabled)");
                        #endif
                    }
                }
                else if (behaviorType == LockBehaviorType.SoftLock && lockState.ValueRO.IsLocked && lockState.ValueRO.Phase == LockPhase.Locking)
                {
                    // Log that we're ignoring mouse input while camera is en route
                    #if UNITY_EDITOR
                    if (UnityEngine.Time.frameCount % 30 == 0)
                    {
                        float2 lookDelta = input.ValueRO.LookDelta;
                        float lookMagnitude = math.length(lookDelta);
                        if (lookMagnitude > 0.5f)
                        {
                            UnityEngine.Debug.Log($"[SLDEBUG] Ignoring mouse (mag={lookMagnitude:F2}) - Phase=Locking (waiting for camera arrival)");
                        }
                    }
                    #endif
                }
                
                // Block re-lock during soft lock cooldown (use per-entity cooldown)
                if (behaviorType == LockBehaviorType.SoftLock && lockState.ValueRO.SoftLockBreakCooldown > 0)
                {
                    shouldLock = false;
                }
                
                // Process lock/unlock
                bool justLocked = false;
                if (shouldUnlock && lockState.ValueRO.IsLocked)
                {
                    lockState.ValueRW.IsLocked = false;
                    lockState.ValueRW.TargetEntity = Entity.Null;
                    lockState.ValueRW.Phase = LockPhase.Unlocked;
                    lockState.ValueRW.JustUnlocked = true; // Signal to other systems
                    #if UNITY_EDITOR
                    UnityEngine.Debug.Log($"[LOCKDEBUG] UNLOCK success, Phase=Unlocked");
                    #endif
                }
                else if (shouldLock && !lockState.ValueRO.IsLocked && allowTargetLock)
                {
                    // Find target using crosshair direction (not just nearest)
                    Entity target = FindTargetInCrosshair(ref state, transform.ValueRO.Position, aimOrigin, aimDir, entity, maxLockRange, maxLockAngle);
                    if (target != Entity.Null)
                    {
                        lockState.ValueRW.IsLocked = true;
                        lockState.ValueRW.TargetEntity = target;
                        justLocked = true;
                        
                        // Start in LOCKING phase - camera is en route to target
                        // CinemachineCameraController will transition to LOCKED when camera arrives
                        lockState.ValueRW.Phase = LockPhase.Locking;
                        
                        // Set initial target position immediately
                        if (em.HasComponent<LocalTransform>(target))
                        {
                            float3 pos = em.GetComponentData<LocalTransform>(target).Position;
                            float heightOffset = defaultHeightOffset;
                            if (em.HasComponent<LockOnTarget>(target))
                                heightOffset = em.GetComponentData<LockOnTarget>(target).IndicatorHeightOffset;
                            pos.y += heightOffset;
                            lockState.ValueRW.LastTargetPosition = pos;
                        }
                        
                        #if UNITY_EDITOR
                        UnityEngine.Debug.Log($"[LOCKDEBUG] LOCK success target={target.Index}, Phase=Locking (waiting for camera arrival)");
                        #endif
                    }
                    #if UNITY_EDITOR
                    else
                    {
                        UnityEngine.Debug.Log($"[LOCKDEBUG] LOCK FAILED - no target found (range={maxLockRange}, angle={maxLockAngle})");
                    }
                    #endif
                }
                
                // Update lock state while locked - just update target position
                // Camera rotation is handled by PlayerCameraControlSystem to avoid data race
                // Skip validation on first frame of lock since we just validated the target
                if (lockState.ValueRO.IsLocked && !justLocked)
                {
                    Entity target = lockState.ValueRO.TargetEntity;
                    
                    // Validate target still exists and is targetable
                    bool exists = em.Exists(target);
                    bool hasComp = exists && em.HasComponent<LockOnTarget>(target);
                    bool isEnabled = hasComp && em.IsComponentEnabled<LockOnTarget>(target);
                    
                    if (!exists || !hasComp || !isEnabled)
                    {
                        lockState.ValueRW.IsLocked = false;
                        lockState.ValueRW.TargetEntity = Entity.Null;
                        lockState.ValueRW.Phase = LockPhase.Unlocked;
                        lockState.ValueRW.JustUnlocked = true;
                        // Target lost
                        continue;
                    }
                    
                    // Get target position
                    if (!em.HasComponent<LocalTransform>(target)) continue;
                    float3 targetPos = em.GetComponentData<LocalTransform>(target).Position;
                    
                    // Get lock-on height offset (use per-entity if available, else config default)
                    float heightOffset = defaultHeightOffset;
                    if (em.HasComponent<LockOnTarget>(target))
                    {
                        heightOffset = em.GetComponentData<LockOnTarget>(target).IndicatorHeightOffset;
                    }
                    targetPos.y += heightOffset;
                    
                    // Update last target position for PlayerCameraControlSystem to use
                    lockState.ValueRW.LastTargetPosition = targetPos;
                }
            }
        }
        
        /// <summary>
        /// Find best target in crosshair direction.
        /// Prioritizes targets closer to aim direction, then by priority, then by distance.
        /// </summary>
        private Entity FindTargetInCrosshair(ref SystemState state, float3 playerPos, float3 aimOrigin, float3 aimDir, Entity self, float maxRange, float maxAngle)
        {
            Entity bestTarget = Entity.Null;
            float bestScore = float.MinValue;
            
            foreach (var (lockOnTarget, transform, targetEntity) in 
                SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (targetEntity == self) continue;
                
                float3 targetPos = transform.ValueRO.Position;
                targetPos.y += lockOnTarget.ValueRO.IndicatorHeightOffset;
                
                // Distance check
                float3 toTarget = targetPos - playerPos;
                float dist = math.length(toTarget);
                if (dist > maxRange || dist < 0.5f) continue;
                
                // Angle from crosshair check
                float3 toTargetFromAim = targetPos - aimOrigin;
                float3 toTargetNorm = math.normalize(toTargetFromAim);
                float dot = math.dot(aimDir, toTargetNorm);
                float angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                
                if (angle > maxAngle) continue;
                
                // Calculate score: prioritize targets closer to crosshair center
                // Score = priority * 10 - angle - distance * 0.1
                int priority = lockOnTarget.ValueRO.Priority;
                float score = priority * 10f - angle - dist * 0.1f;
                
                if (score > bestScore)
                {
                    bestTarget = targetEntity;
                    bestScore = score;
                }
            }
            
            return bestTarget;
        }
    }
}
