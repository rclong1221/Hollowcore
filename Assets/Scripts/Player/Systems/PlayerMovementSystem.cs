using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Physics;
using Player.Components;
using Player.Systems;
using DIG.Player.Components;
using DIG.Player.Abilities;
using DIG.Swimming;
using DIG.Targeting.Core;
using DIG.Targeting;
using DIG.Core.Input;
using DIG.Surface;
using System.Collections.Generic;

[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerStateSystem))]
[UpdateBefore(typeof(CharacterControllerSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[BurstCompile]
public partial struct PlayerMovementSystem : ISystem
{   
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        state.RequireForUpdate<PlayerTag>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var currentTime = (float)SystemAPI.Time.ElapsedTime;

        int entityCount = 0;
        var entityManager = state.EntityManager;
        
        // EPIC 15.16: Get lock behavior settings for Soft Lock vs Hard Lock rotation
        // Note: This system is Burst-compiled so we read from ECS singleton only (not static fields)
        LockBehaviorType lockBehaviorType = LockBehaviorType.HardLock;
        float characterRotationStrength = 1f; // 1 = hard lock (instant), 0 = no rotation toward target
        if (SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var lockBehavior))
        {
            lockBehaviorType = lockBehavior.BehaviorType;
            characterRotationStrength = lockBehavior.CharacterRotationStrength;
        }

        // EPIC 15.20: Get paradigm settings from ECS singleton (Burst-compatible)
        var paradigmSettings = new ParadigmSettings();
        bool hasParadigmSettings = SystemAPI.TryGetSingleton<ParadigmSettings>(out var ps);
        if (hasParadigmSettings)
        {
            paradigmSettings = ps;
        }

        // EPIC 15.20: Cache CameraData before entity loop (avoids repeated singleton lookup)
        var cameraData = new DIG.Voxel.Components.CameraData();
        bool hasCameraData = SystemAPI.TryGetSingleton<DIG.Voxel.Components.CameraData>(out cameraData);

        // EPIC 15.20: Get CursorHoverResult lookup for attack-toward-cursor in isometric modes
        var cursorHoverLookup = SystemAPI.GetComponentLookup<CursorHoverResult>(true);
        var facingLockLookup = SystemAPI.GetComponentLookup<IsometricFacingLock>(false); // Read-write

        foreach (var (transform, velocity, playerInput, playerState, playerStamina, movementSettingsRef, lockState, entity) in
             SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<PlayerInput>,
                     RefRO<PlayerState>, RefRO<PlayerStamina>,
                     RefRO<PlayerMovementSettings>, RefRO<CameraTargetLockState>>()
             .WithAll<Simulate>()
             .WithAll<PlayerCollisionState>()
             .WithAll<PlayerCameraSettings>()
             .WithAll<CameraViewConfig>()
             .WithEntityAccess())
        {
            // Safety: Guard against NaN inputs (Heal invalid state)
            if (!math.all(math.isfinite(transform.ValueRO.Position)))
            {
               transform.ValueRW.Position = new float3(0, 10, 0); // Reset to safe height
               velocity.ValueRW.Linear = float3.zero; // Stop velocity
               continue; 
            }

            // Skip movement if climbing (check FreeClimbState)
            if (SystemAPI.HasComponent<FreeClimbState>(entity))
            {
                var climbState = SystemAPI.GetComponent<FreeClimbState>(entity);
                if (climbState.IsClimbing)
                {
                    // Zero velocity to prevent accumulation during climb
                    velocity.ValueRW.Linear = Unity.Mathematics.float3.zero;
                    continue;
                }
            }
            
            // Skip movement if riding (RideControlSystem handles position)
            if (SystemAPI.HasComponent<RideState>(entity))
            {
                var rideState = SystemAPI.GetComponent<RideState>(entity);
                if (rideState.RidePhase != RidePhaseConstants.None)
                {
                    // Zero velocity to prevent physics fighting with ride attachment
                    velocity.ValueRW.Linear = float3.zero;
                    continue;
                }
            }
            
            entityCount++;
            var input = playerInput.ValueRO;
            var pState = playerState.ValueRO;
            var stamina = playerStamina.ValueRO;
            var movementSettings = movementSettingsRef.ValueRO;
            var camSettings = SystemAPI.GetComponent<PlayerCameraSettings>(entity);
            var viewType = SystemAPI.GetComponent<CameraViewConfig>(entity).ActiveViewType;
            ref var vel = ref velocity.ValueRW;
            
            
            // BUGFIX: Skip movement for dead/downed players
            // Prevents velocity accumulation (stagger/gravity) that causes ragdoll launch
            if (SystemAPI.HasComponent<DeathState>(entity))
            {
                var deathState = SystemAPI.GetComponent<DeathState>(entity);
                if (deathState.Phase == DeathPhase.Dead || deathState.Phase == DeathPhase.Downed)
                {
                    // Zero velocity to prevent ragdoll launch impulse
                    vel.Linear = float3.zero;
                    continue;
                }
            }
            
            // Skip movement when operating a station (Piloting mode)
            // WASD is routed to ship controls via StationInputRoutingSystem
            if (pState.Mode == PlayerMode.Piloting)
            {
                // Keep the player stationary while piloting
                vel.Linear = float3.zero;
                continue;
            }
            
            // Also zero velocity for anyone operating a station (not just Piloting)
            // This covers cases where other station types might not change mode to Piloting
            if (SystemAPI.HasComponent<DIG.Ship.Stations.OperatingStation>(entity))
            {
                vel.Linear = float3.zero;
                continue;
            }

            // Skip movement if swimming (SwimmingMovementSystem handles physics)
            if (SystemAPI.HasComponent<SwimmingState>(entity))
            {
                if (SystemAPI.GetComponent<SwimmingState>(entity).IsSwimming)
                {
                    continue;
                }
            }
            
            // Get collision state separately (Query limited to 7 type params)
            var collision = SystemAPI.GetComponentRW<PlayerCollisionState>(entity);

            // Epic 7.4.1: Handle knockdown state
            // isKnockedDown = active knockdown phase (no control)
            // isRecovering = recovery phase (limited control)
            bool isActiveKnockdown = collision.ValueRO.KnockdownTimeRemaining > 0;
            bool isRecovering = collision.ValueRO.IsRecoveringFromKnockdown;
            
            if (isActiveKnockdown)
            {
                // During active knockdown: Player has no control, only knockback velocity applies
                // Apply friction to StaggerVelocity
                float3 staggerVel = collision.ValueRO.StaggerVelocity;
                float staggerSpeed = math.length(staggerVel);
                
                if (staggerSpeed > 0.01f)
                {
                    float frictionAmount = movementSettings.Friction * deltaTime;
                    float newSpeed = math.max(0, staggerSpeed - frictionAmount);
                    staggerVel = math.normalize(staggerVel) * newSpeed;
                    collision.ValueRW.StaggerVelocity = staggerVel;
                }
                else
                {
                    collision.ValueRW.StaggerVelocity = float3.zero;
                }
                
                // Set velocity to stagger velocity (knockback)
                vel.Linear.x = staggerVel.x;
                vel.Linear.z = staggerVel.z;
                
                // Apply gravity normally, or override
                if (!pState.IsGrounded)
                {
                    float3 gravityForce = new float3(0, movementSettings.Gravity, 0);
                    
                    if (SystemAPI.HasComponent<DIG.Environment.Gravity.GravityOverride>(entity))
                    {
                        var gOverride = SystemAPI.GetComponent<DIG.Environment.Gravity.GravityOverride>(entity);
                        if (gOverride.IsActive)
                        {
                            gravityForce = gOverride.GravityVector;
                        }
                    }

                    vel.Linear += gravityForce * deltaTime;
                    
                    // Only clamp terminal velocity for Y-down gravity? 
                    // For spherical gravity, clamping magnitude seems safer.
                    // keeping legacy clamp for Y only if standard gravity
                    if (gravityForce.x == 0 && gravityForce.z == 0 && gravityForce.y < 0)
                    {
                         vel.Linear.y = math.max(vel.Linear.y, movementSettings.MaxFallSpeed);
                    }
                }
                
                // Skip normal movement processing
                continue;
            }

            // Debug logging (compile-time conditional, throttled to 1 log per 120 frames)
            bool hasInput = input.Horizontal != 0 || input.Vertical != 0 || input.IsPathFollowing != 0;

            // 0. GRAVITY ALIGNMENT (Epic 14.31)
            // Determine "Up" vector based on Gravity Override or Default
            float3 characterUp = math.up();

            if (SystemAPI.HasComponent<DIG.Environment.Gravity.GravityOverride>(entity))
            {
                var gOverride = SystemAPI.GetComponent<DIG.Environment.Gravity.GravityOverride>(entity);
                if (gOverride.IsActive && math.lengthsq(gOverride.GravityVector) > 0.001f)
                {
                    // Gravity pulls DOWN, so Up is Opposite
                    characterUp = -math.normalizesafe(gOverride.GravityVector);
                }
            }

            // Align Character to new Up Vector
            float3 currentUp = math.mul(transform.ValueRO.Rotation, math.up());
            // Check alignment (dot product close to 1 means aligned)
            float dotUp = math.dot(currentUp, characterUp);
            if (dotUp < 0.999f)
            {
                // Calculate alignment rotation using pure math (Burst-compatible)
                quaternion alignRot = FromToRotation(currentUp, characterUp);
                transform.ValueRW.Rotation = math.mul(alignRot, transform.ValueRO.Rotation);
            }

            // 1. INPUT ANALYSIS
            bool isRightClickHeld = input.AltUse.IsSet; // Steering trigger
            bool isFreeLook = input.FreeLook.IsSet;     // Free Look trigger
            bool isShooterMode = (viewType == CameraViewType.FirstPerson || viewType == CameraViewType.Combat);
            
            // Epic 15.16: Z-Targeting - use lock data from PlayerInput (synced client → server)
            // This ensures both client and server apply the same rotation
            float3 lockTargetPos = input.LockTargetPosition;
            bool isTargetLocked = input.IsLockedOn != 0 && math.lengthsq(lockTargetPos) > 1f;
            
            // LOGIC SPLIT:
            // SHOOTER MODE (FPS/Combat): Always Steer (Character locked to Camera), unless Free Look.
            // ADVENTURE MODE (MMO): Tank Controls, unless Right Click is held (Steer).
            // LOCK-ON: Always Steer (Face Target/Camera).
            // SCREEN-RELATIVE (ARPG/TwinStick): Rotation handled by PlayerFacingSystem - skip here!
            
            // EPIC 15.20: Check for screen-relative movement early (used for both rotation and movement)
            bool useScreenRelativeMovement = paradigmSettings.IsValid && paradigmSettings.UseScreenRelativeMovement;
            
            bool shouldSteer = false;
            
            // EPIC 15.20: ADTurnsCharacter from paradigm settings overrides camera-based isShooterMode
            // If paradigm says A/D turns character (MMO mode), use MMO logic regardless of camera type
            bool useMMOLogic = paradigmSettings.IsValid && paradigmSettings.ADTurnsCharacter;
            
            // EPIC 15.21: MMO Auto-run (Both mouse buttons = forward movement)
            // When AutoRun input is active (from composite binding LMB+RMB), override forward input
            bool isAutoRunning = input.AutoRun.IsSet;
            // EPIC 18.15: Gate WASD input based on paradigm settings (defense-in-depth).
            // When wasdEnabled=false (MOBA/ARPG), only path-following input moves the character.
            bool wasdGated = hasParadigmSettings && !paradigmSettings.IsWASDEnabled;
            int effectiveVertical = wasdGated ? 0 : (isAutoRunning ? 1 : input.Vertical);
            int effectiveHorizontal = wasdGated ? 0 : input.Horizontal;
            
            if (isTargetLocked)
            {
                shouldSteer = true; // Always face target when locked
            }
            else if (useMMOLogic)
            {
                shouldSteer = isRightClickHeld;
            }
            else if (isShooterMode)
            {
                shouldSteer = !isFreeLook; // FPS/Combat defaults to steering, Alt unlocks it.
            }
            else
            {
                shouldSteer = isRightClickHeld; // Adventure defaults to Tank, Right Click enables steering.
            }
            
            // EPIC 15.21: Separation of Turn vs Strafe Input
            // In Shooter: A/D is always Strafe.
            // In MMO: A/D is Turn (unless RMB held), Q/E is always Strafe.
            
            float strafeInput = effectiveHorizontal; 
            float turnInputFromKeys = 0f;

            if (useMMOLogic)
            {
                // MMO Logic
                if (shouldSteer) // RMB held (Orbit)
                {
                    // A/D = Strafe
                    strafeInput = effectiveHorizontal;
                    turnInputFromKeys = 0f;
                }
                else
                {
                    // A/D = Turn
                    strafeInput = 0f;
                    turnInputFromKeys = effectiveHorizontal;
                }
                
                // Explicit Strafe Keys (Q/E) - Always add to strafe
                // This allows Q/E to strafe even when A/D are turning
                if (input.MMOStrafeLeft.IsSet) strafeInput -= 1f;
                if (input.MMOStrafeRight.IsSet) strafeInput += 1f;
                
                strafeInput = math.clamp(strafeInput, -1f, 1f);
            }

            // 2. ROTATION LOGIC
            if (useScreenRelativeMovement)
            {
                // EPIC 15.20: ATTACK-TOWARD-CURSOR with persistent facing
                // When attacking: face cursor and LOCK that direction
                // When moving: face movement direction, clear lock
                // When idle: maintain locked direction if set
                
                bool isAttacking = input.Use.IsSet;
                bool hasCursorTarget = false;
                float3 cursorDirection = float3.zero;
                bool hasFacingLock = facingLockLookup.HasComponent(entity);
                
                // Check for attack toward cursor
                if (isAttacking && cursorHoverLookup.HasComponent(entity))
                {
                    var cursorHover = cursorHoverLookup[entity];
                    if (cursorHover.IsValid)
                    {
                        float3 toCursor = cursorHover.HitPoint - transform.ValueRO.Position;
                        toCursor.y = 0; // Flatten to XZ plane
                        if (math.lengthsq(toCursor) > 0.25f) // Only if cursor is not too close
                        {
                            cursorDirection = math.normalizesafe(toCursor);
                            hasCursorTarget = true;
                        }
                    }
                }
                
                // EPIC 15.20 Phase 3: Use continuous path direction for facing during path following
                float2 moveInput;
                if (input.IsPathFollowing != 0)
                    moveInput = new float2(input.PathMoveX, input.PathMoveY);
                else
                    moveInput = new float2(effectiveHorizontal, effectiveVertical);
                bool isMoving = math.lengthsq(moveInput) > 0.1f;
                
                // Priority: Attack direction > Move direction > Locked direction > Keep current
                if (hasCursorTarget)
                {
                    // Face cursor - instant rotation for responsive attacks
                    quaternion targetRot = quaternion.LookRotationSafe(cursorDirection, math.up());
                    transform.ValueRW.Rotation = targetRot;
                    
                    // Lock this direction so it persists after attack ends
                    if (hasFacingLock)
                    {
                        var facingLock = facingLockLookup[entity];
                        facingLock.LockedDirection = cursorDirection;
                        facingLock.IsLocked = true;
                        facingLockLookup[entity] = facingLock;
                    }
                }
                else if (isMoving)
                {
                    // Clear facing lock when moving
                    if (hasFacingLock)
                    {
                        var facingLock = facingLockLookup[entity];
                        facingLock.IsLocked = false;
                        facingLockLookup[entity] = facingLock;
                    }
                    
                    // SIMPLE ISOMETRIC FACING: Face movement direction instantly
                    // EPIC 15.20: Use cached CameraData from before entity loop
                    // Must match movement code (section 3) exactly to avoid facing/movement divergence
                    float3 camForward = float3.zero;
                    float3 camRight = float3.zero;

                    if (hasCameraData && cameraData.IsValid)
                    {
                        camForward = cameraData.Forward;
                        camRight = cameraData.Right;
                    }

                    // Flatten to XZ plane BEFORE checking length (steep cameras have near-zero XZ)
                    camForward.y = 0;
                    camRight.y = 0;

                    // Fallback to ParadigmSettings.CameraYaw if flattened forward is near-zero
                    if (math.lengthsq(camForward) < 0.01f)
                    {
                        float cameraYaw = paradigmSettings.CameraYaw;
                        var yawRotation = quaternion.Euler(0f, math.radians(cameraYaw), 0f);
                        camForward = math.mul(yawRotation, new float3(0, 0, 1));
                        camRight = math.mul(yawRotation, new float3(1, 0, 0));
                    }

                    camForward = math.normalizesafe(camForward, new float3(0, 0, 1));
                    camRight = math.normalizesafe(camRight, new float3(1, 0, 0));
                    
                    // Movement direction in world space
                    // EPIC 15.20 Phase 3: Use continuous path direction for smooth facing during path following
                    float3 moveDir3;
                    if (input.IsPathFollowing != 0)
                        moveDir3 = (camForward * input.PathMoveY) + (camRight * input.PathMoveX);
                    else
                        moveDir3 = (camForward * effectiveVertical) + (camRight * effectiveHorizontal);
                    moveDir3.y = 0;
                    moveDir3 = math.normalizesafe(moveDir3);
                    
                    // Face movement direction - INSTANT, no slerp
                    quaternion targetRot = quaternion.LookRotationSafe(moveDir3, math.up());
                    transform.ValueRW.Rotation = targetRot;
                }
                else if (hasFacingLock)
                {
                    // Idle: maintain locked direction from last attack
                    var facingLock = facingLockLookup[entity];
                    if (facingLock.IsLocked && math.lengthsq(facingLock.LockedDirection) > 0.01f)
                    {
                        quaternion targetRot = quaternion.LookRotationSafe(facingLock.LockedDirection, math.up());
                        transform.ValueRW.Rotation = targetRot;
                    }
                }
                // If no input, no lock, keep current rotation (don't modify)
            }
            else if (isTargetLocked)
            {
                // LOCK-ON MODE: Character faces TARGET
                // Behavior depends on lock mode:
                // - HardLock: Instantly snap to face target (Dark Souls style)
                // - SoftLock: Blend toward target, player retains some rotation control (God of War style)
                float3 targetPos = lockTargetPos;
                float3 playerPos = transform.ValueRO.Position;
                float3 toTarget = targetPos - playerPos;
                
                // Zero out vertical to get horizontal direction only
                toTarget.y = 0;
                float distSq = math.lengthsq(toTarget);
                
                if (distSq > 0.25f) // Only rotate if target is not too close
                {
                    toTarget = math.normalize(toTarget);
                    float targetYaw = math.atan2(toTarget.x, toTarget.z);
                    quaternion targetRot = quaternion.Euler(0, targetYaw, 0);
                    
                    // EPIC 15.16: Mode-specific rotation behavior
                    // HardLock AND SoftLock: Instantly face target while locked
                    // (SoftLock breaks when mouse moves, but while locked it behaves like HardLock)
                    if (lockBehaviorType == LockBehaviorType.HardLock || 
                        lockBehaviorType == LockBehaviorType.SoftLock)
                    {
                        // Instantly face target (no slerp delay)
                        // This ensures the player always faces the target, even while strafing
                        transform.ValueRW.Rotation = targetRot;
                    }
                    else
                    {
                        // Other modes (Isometric, OTS, etc.) - use CharacterRotationStrength as blend
                        float blendFactor = math.saturate(characterRotationStrength * deltaTime * 10f);
                        if (blendFactor > 0.001f)
                        {
                            transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, blendFactor);
                        }
                        // If blend is 0, don't modify rotation (player/camera controlled)
                    }
                }
            }
            else if (shouldSteer)
            {
                // STEERING MODE (Character hard-locked to Camera Yaw)
                float targetYaw = camSettings.Yaw; // From PlayerCameraSettings
                if (input.CameraYawValid != 0) targetYaw = input.CameraYaw; // Prefer Input if valid
                
                quaternion targetRot = quaternion.Euler(0, math.radians(targetYaw), 0);
                transform.ValueRW.Rotation = targetRot;
            }
            else if (!isFreeLook)
            {
                // STANDARD/TANK MODE
                // Mouse turns character normally (if not steering, mouse orbits camera, character turns with it?)
                // In Shooter mode with Free Look -> Camera orbits, character stays put.
                // In Adventure mode -> Mouse turns character + camera.
                
                float turnAmount = input.LookDelta.x * camSettings.LookSensitivity;
                if (math.abs(turnAmount) > 0.001f)
                {
                    var rotationDelta = quaternion.AxisAngle(characterUp, math.radians(turnAmount));
                    transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, rotationDelta);
                }
                
                // A/D keyboard turn (using separate turn input calculated earlier)
                // This applies when in Tank mode (MMO default)
                if (math.abs(turnInputFromKeys) > 0.001f)
                {
                    float keyTurnAmount = turnInputFromKeys * movementSettings.TurnSpeed * deltaTime;
                    var rotationDelta = quaternion.AxisAngle(characterUp, math.radians(keyTurnAmount));
                    transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, rotationDelta);
                }
            }
            
            // EPIC 15.20 Phase 3: When path-following in non-screen-relative mode,
            // face the movement direction (camera-relative reconstruction)
            if (!useScreenRelativeMovement && input.IsPathFollowing != 0)
            {
                float3 pfCamFwd = float3.zero;
                float3 pfCamRt = float3.zero;
                if (hasCameraData && cameraData.IsValid)
                {
                    pfCamFwd = cameraData.Forward;
                    pfCamRt = cameraData.Right;
                }
                pfCamFwd.y = 0;
                pfCamRt.y = 0;
                if (math.lengthsq(pfCamFwd) < 0.01f)
                {
                    float cameraYaw = paradigmSettings.CameraYaw;
                    var yawRot = quaternion.Euler(0f, math.radians(cameraYaw), 0f);
                    pfCamFwd = math.mul(yawRot, new float3(0, 0, 1));
                    pfCamRt = math.mul(yawRot, new float3(1, 0, 0));
                }
                pfCamFwd = math.normalizesafe(pfCamFwd, new float3(0, 0, 1));
                pfCamRt = math.normalizesafe(pfCamRt, new float3(1, 0, 0));
                float3 pathWorldDir = (pfCamFwd * input.PathMoveY) + (pfCamRt * input.PathMoveX);
                pathWorldDir.y = 0;
                if (math.lengthsq(pathWorldDir) > 0.01f)
                {
                    pathWorldDir = math.normalize(pathWorldDir);
                    transform.ValueRW.Rotation = quaternion.LookRotationSafe(pathWorldDir, math.up());
                }
            }

            // 3. MOVEMENT LOGIC
            float3 moveDir = float3.zero;

            // EPIC 15.20: Screen-relative movement check already done above
            if (useScreenRelativeMovement)
            {
                // SCREEN-RELATIVE MOVEMENT: WASD maps to fixed screen directions
                // We use the camera's horizontal rotation to determine what "up/right" means on screen
                float3 screenUp = float3.zero;
                float3 screenRight = float3.zero;

                // Use cached CameraData from before entity loop
                if (hasCameraData && cameraData.IsValid)
                {
                    screenUp = cameraData.Forward;
                    screenRight = cameraData.Right;
                }

                // Flatten to XZ plane BEFORE checking length (steep cameras have near-zero XZ)
                screenUp.y = 0;
                screenRight.y = 0;

                // Fallback to ParadigmSettings.CameraYaw if flattened forward is near-zero
                if (math.lengthsq(screenUp) < 0.01f)
                {
                    float cameraYaw = paradigmSettings.CameraYaw;
                    var yawRotation = quaternion.Euler(0f, math.radians(cameraYaw), 0f);
                    screenUp = math.mul(yawRotation, new float3(0, 0, 1));
                    screenRight = math.mul(yawRotation, new float3(1, 0, 0));
                }

                screenUp = math.normalizesafe(screenUp, new float3(0, 0, 1));
                screenRight = math.normalizesafe(screenRight, new float3(1, 0, 0));
                
                // W/S = screen up/down, A/D = screen left/right
                // EPIC 15.20 Phase 3: Use continuous path direction for smooth diagonal movement
                if (input.IsPathFollowing != 0)
                    moveDir = (screenUp * input.PathMoveY) + (screenRight * input.PathMoveX);
                else
                    moveDir = (screenUp * input.Vertical) + (screenRight * input.Horizontal);
            }
            // Both HardLock and SoftLock use circle-strafe movement while locked
            // (SoftLock breaks when mouse moves, but while locked it behaves like HardLock)
            else if (isTargetLocked && (lockBehaviorType == LockBehaviorType.HardLock || 
                                   lockBehaviorType == LockBehaviorType.SoftLock))
            {
                // LOCK-ON MOVEMENT: Circle strafe around target (Dark Souls style)
                // Calculate directions from CURRENT position to target (same as rotation logic)
                float3 playerPos = transform.ValueRO.Position;
                float3 toTarget = lockTargetPos - playerPos;
                toTarget.y = 0; // Horizontal only
                
                float distSq = math.lengthsq(toTarget);
                if (distSq > 0.25f)
                {
                    // Forward = toward target, Right = tangent to circle (for strafing)
                    float3 lockForward = math.normalize(toTarget);
                    float3 lockRight = new float3(lockForward.z, 0, -lockForward.x); // 90° rotation in XZ plane
                    
                    // W = toward target, S = away, A/D = perfect circle strafe
                    moveDir = (lockForward * effectiveVertical) + (lockRight * effectiveHorizontal);
                }
            }
            else 
            {
                // UNIFIED MOVEMENT (Steering or Tank)
                // Both modes use the same math for applying movement vector relative to character:
                // Forward = Character Forward
                // Right = Character Right (Strafe)
                // In Tank mode, strafeInput is usually 0 unless Q/E is pressed (or RMB held).
                // In Steering mode, strafeInput is A/D (plus Q/E).
                
                float3 modelForward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
                float3 modelRight = math.mul(transform.ValueRO.Rotation, new float3(1, 0, 0));
                
                // Flatten against Character Up
                modelForward = modelForward - (math.dot(modelForward, characterUp) * characterUp);
                modelRight = modelRight - (math.dot(modelRight, characterUp) * characterUp);

                modelForward = math.normalizesafe(modelForward);
                modelRight = math.normalizesafe(modelRight);
                
                // Unified Movement:
                // - Forward/Back applies to modelForward
                // - Strafe applies to modelRight using calculated strafeInput
                // EPIC 15.20 Phase 3: Path direction was computed in camera-relative space,
                // so reconstruct using camera vectors even in unified mode
                if (input.IsPathFollowing != 0)
                {
                    float3 camFwd = float3.zero;
                    float3 camRt = float3.zero;
                    if (hasCameraData && cameraData.IsValid)
                    {
                        camFwd = cameraData.Forward;
                        camRt = cameraData.Right;
                    }
                    camFwd.y = 0;
                    camRt.y = 0;
                    if (math.lengthsq(camFwd) < 0.01f)
                    {
                        float cameraYaw = paradigmSettings.CameraYaw;
                        var yawRot = quaternion.Euler(0f, math.radians(cameraYaw), 0f);
                        camFwd = math.mul(yawRot, new float3(0, 0, 1));
                        camRt = math.mul(yawRot, new float3(1, 0, 0));
                    }
                    camFwd = math.normalizesafe(camFwd, new float3(0, 0, 1));
                    camRt = math.normalizesafe(camRt, new float3(1, 0, 0));
                    moveDir = (camFwd * input.PathMoveY) + (camRt * input.PathMoveX);
                }
                else
                    moveDir = (modelForward * effectiveVertical) + (modelRight * strafeInput);
            }
            
            if (math.lengthsq(moveDir) > 1f) moveDir = math.normalize(moveDir);

            // Normal Movement Logic 
            // Determine target speed based on stance and sprint
            float targetSpeed = GetTargetSpeed(pState, input, stamina, movementSettings);
            
            // Apply Speed Modifiers (Epic 13.3 + 13.5)
            if (SystemAPI.HasComponent<SpeedModifierState>(entity))
            {
                targetSpeed *= SystemAPI.GetComponent<SpeedModifierState>(entity).CombinedMultiplier;
            }

            // EPIC 16.10: Apply surface movement modifiers (speed + friction)
            float surfaceFrictionMultiplier = 1.0f;
            if (SystemAPI.HasComponent<SurfaceMovementModifier>(entity))
            {
                var surfaceMod = SystemAPI.GetComponent<SurfaceMovementModifier>(entity);
                targetSpeed *= surfaceMod.SpeedMultiplier;
                surfaceFrictionMultiplier = surfaceMod.FrictionMultiplier;
            }

            // Epic 7.4.1: During recovery, allow limited movement
            if (isRecovering)
            {
                targetSpeed *= SystemAPI.GetSingleton<PlayerCollisionSettings>().KnockdownRecoverySpeedMultiplier;
            }
            
            // Epic 13.1: Apply motor polish multipliers if available (inlined for Burst)
            if (SystemAPI.HasComponent<MotorPolishSettings>(entity))
            {
                var polishSettings = SystemAPI.GetComponent<MotorPolishSettings>(entity);
                
                // Backwards movement multiplier (inlined)
                // EPIC 15.20: In screen-relative mode, behavior depends on attack state
                // - Not attacking: Character faces movement direction, no backward penalty
                // - Attacking: Character faces cursor, apply backward penalty based on actual facing
                if (!useScreenRelativeMovement)
                {
                    // Standard: backward penalty based on input
                    float backwardsMult = input.Vertical >= 0 
                        ? 1f 
                        : math.lerp(1f, polishSettings.MotorBackwardsMultiplier, math.abs(input.Vertical));
                    targetSpeed *= backwardsMult;
                }
                else if (input.Use.IsSet)
                {
                    // EPIC 15.20: Attacking in isometric - calculate backward penalty based on facing vs movement
                    // Character faces cursor, movement is camera-relative
                    // Get character's forward direction
                    float3 charForward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
                    charForward.y = 0;
                    charForward = math.normalizesafe(charForward);
                    
                    // Get world-space movement direction
                    float3 worldMoveDir = float3.zero;
                    float2 moveInput = new float2(input.Horizontal, input.Vertical);
                    if (math.lengthsq(moveInput) > 0.01f)
                    {
                        float3 camForward = float3.zero;
                        float3 camRight = float3.zero;

                        if (hasCameraData && cameraData.IsValid)
                        {
                            camForward = cameraData.Forward;
                            camRight = cameraData.Right;
                        }

                        // Flatten BEFORE checking length (steep cameras have near-zero XZ)
                        camForward.y = 0;
                        camRight.y = 0;

                        if (math.lengthsq(camForward) < 0.01f)
                        {
                            var yawRotation = quaternion.Euler(0f, math.radians(paradigmSettings.CameraYaw), 0f);
                            camForward = math.mul(yawRotation, new float3(0, 0, 1));
                            camRight = math.mul(yawRotation, new float3(1, 0, 0));
                        }

                        camForward = math.normalizesafe(camForward, new float3(0, 0, 1));
                        camRight = math.normalizesafe(camRight, new float3(1, 0, 0));
                        
                        worldMoveDir = (camForward * input.Vertical) + (camRight * input.Horizontal);
                        worldMoveDir.y = 0;
                        worldMoveDir = math.normalizesafe(worldMoveDir);
                    }
                    
                    // Dot product: 1 = forward, -1 = backward
                    float forwardDot = math.dot(worldMoveDir, charForward);
                    if (forwardDot < 0f)
                    {
                        // Moving backward relative to facing - apply penalty
                        float backwardsMult = math.lerp(1f, polishSettings.MotorBackwardsMultiplier, math.abs(forwardDot));
                        targetSpeed *= backwardsMult;
                    }
                }
                // Else: screen-relative mode but not attacking - no backward penalty (always facing movement)
                
                // Slope force adjustment (inlined)
                if (SystemAPI.HasComponent<MotorPolishState>(entity))
                {
                    var polishState = SystemAPI.GetComponent<MotorPolishState>(entity);
                    
                    if (polishSettings.AdjustMotorForceOnSlope == 1 && polishState.CurrentSlopeAngle >= 5f)
                    {
                        float slopeFactor = math.saturate(polishState.CurrentSlopeAngle / 45f);
                        float baseMultiplier = polishState.IsMovingUphill == 1 
                            ? polishSettings.MotorSlopeForceUp 
                            : polishSettings.MotorSlopeForceDown;
                        float slopeMult = math.lerp(1f, baseMultiplier, slopeFactor);
                        targetSpeed *= slopeMult;
                    }
                }
            }
            
            // Epic 13.1: Apply external forces to velocity
            if (SystemAPI.HasComponent<ExternalForceState>(entity))
            {
                var externalForce = SystemAPI.GetComponent<ExternalForceState>(entity);
                if (math.lengthsq(externalForce.AccumulatedForce) > 0.001f)
                {
                    vel.Linear += externalForce.AccumulatedForce * deltaTime;
                }
            }
            
            
            // Apply movement based on grounded state
            if (pState.IsGrounded)
            {
                ApplyGroundMovement(ref vel, moveDir, targetSpeed, movementSettings, deltaTime, surfaceFrictionMultiplier);

                // Refined: Only kill vertical velocity if we aren't moving upward (e.g. at peak of jump or descending)
                // This prevents the "grounding" frame from eating the jump impulse.
                if (vel.Linear.y <= 0.01f)
                {
                    vel.Linear.y = 0f;
                }


            }
            else
            {
                // In air
                ApplyAirMovement(ref vel, moveDir, targetSpeed, movementSettings, deltaTime);

                // Apply gravity
                vel.Linear.y += movementSettings.Gravity * deltaTime;
                vel.Linear.y = math.max(vel.Linear.y, movementSettings.MaxFallSpeed);


            }
            
            // NOTE: Position update is handled by CharacterControllerSystem which reads PhysicsVelocity
            // and applies it with collision detection. We only set velocity here.

        } // END OF ENTITY LOOP
    }
    
    /// <summary>
    /// Determines target speed based on stance, sprint, and stamina
    /// </summary>
    private static float GetTargetSpeed(in PlayerState state, in PlayerInput input, in PlayerStamina stamina,
                                       in PlayerMovementSettings settings)
    {
        // Legacy Prone logic (preserved until ProneAbility is implemented)
        if (state.Stance == PlayerStance.Prone)
        {
            return settings.ProneSpeed;
        }

        // Crouch and Sprint are handled by SpeedModifiers now (Ability System)
        // Return base RunSpeed
        return settings.RunSpeed;
    }
    
    /// <summary>
    /// Applies ground movement with acceleration and friction
    /// </summary>
    private static void ApplyGroundMovement(ref PhysicsVelocity velocity, in float3 moveDir, float targetSpeed,
                                           in PlayerMovementSettings settings, float deltaTime,
                                           float surfaceFrictionMultiplier = 1.0f)
    {
        // Get current horizontal velocity
        float3 currentVel = new float3(velocity.Linear.x, 0, velocity.Linear.z);

        // Apply friction (EPIC 16.10: scaled by surface friction modifier)
        ApplyFriction(ref currentVel, settings.Friction, deltaTime, targetSpeed, surfaceFrictionMultiplier);
        
        // Calculate acceleration
        float3 wishDir = math.normalizesafe(moveDir);
        float wishSpeed = targetSpeed;
        
        // Accelerate
        float currentSpeedInWishDir = math.dot(currentVel, wishDir);
        float addSpeed = wishSpeed - currentSpeedInWishDir;
        
        if (addSpeed > 0)
        {
            float accelSpeed = settings.GroundAcceleration * deltaTime * wishSpeed;
            accelSpeed = math.min(accelSpeed, addSpeed);
            currentVel += wishDir * accelSpeed;
        }
        
        // Update velocity (preserve vertical component)
        velocity.Linear.x = currentVel.x;
        velocity.Linear.z = currentVel.z;
    }
    
    /// <summary>
    /// Applies air movement using Source Engine-style air strafing (Epic 15.3.2)
    /// Allows players to curve mid-air by strafing perpendicular to velocity.
    /// Key: Acceleration is applied based on how much speed we ALREADY have in the wish direction,
    /// not by dragging velocity toward a target. This preserves jump momentum.
    /// </summary>
    private static void ApplyAirMovement(ref PhysicsVelocity velocity, in float3 moveDir, float targetSpeed,
                                        in PlayerMovementSettings settings, float deltaTime)
    {
        // No input = no air control, but PRESERVE existing momentum (critical for jumps)
        if (math.lengthsq(moveDir) < 0.01f)
        {
            return; // Do nothing - velocity.Linear.x/z are untouched
        }

        // Get current horizontal velocity (preserve original for reference)
        float3 currentVel = new float3(velocity.Linear.x, 0, velocity.Linear.z);

        // Source Engine Air Strafe Formula
        
        float3 wishDir = math.normalize(moveDir);
        float wishSpeed = targetSpeed;

        // Cap "wish speed" for air strafing? Usually it's limited to avoid infinite accel, 
        // but Source caps only the *gain*, not the speed.
        // For Quake/Source: if (wishSpeed > 30) wishSpeed = 30; // Usually hard cap on accel impulse
        // Our settings.AirAcceleration controls this gain.
        
        // Project current horizontal velocity onto wish direction
        float currentSpeedInWishDir = math.dot(currentVel, wishDir);
        
        // The core Source/Quake trick:
        // Identify how much speed we WANT to add (Target - CurrentProj)
        float addSpeed = wishSpeed - currentSpeedInWishDir;
        
        // If we serve to add speed (e.g. we aren't already moving faster than target in that dir)
        if (addSpeed > 0)
        {
            // Calculate how much to add this frame
            // Using AirAcceleration setting
            float accelSpeed = settings.AirAcceleration * wishSpeed * deltaTime;
            
            // Clamp to ensure we don't overshoot the target speed *in this direction*
            accelSpeed = math.min(accelSpeed, addSpeed);
            
            // Apply the addition
            currentVel += wishDir * accelSpeed;
        }

        // Update velocity.Linear X/Z
        velocity.Linear.x = currentVel.x;
        velocity.Linear.z = currentVel.z;
    }


    /// <summary>
    /// Builds a normalized move direction that is aligned to the active camera's forward/right vectors.
    /// Falls back to the entity's forward vector when no camera data is available (e.g., on the server).
    /// </summary>
    private static float3 ComputeCameraRelativeDirection(Entity entity, EntityManager entityManager, in quaternion fallbackRotation,
                                                         in PlayerInput input, out float3 flattenedCameraForward)
    {
        float3 forward;

        if (input.CameraYawValid != 0)
        {
            var yawRotation = quaternion.Euler(0f, math.radians(input.CameraYaw), 0f);
            forward = math.mul(yawRotation, new float3(0, 0, 1));
        }
        else if (entityManager.HasComponent<CameraTarget>(entity))
        {
            var cameraTarget = entityManager.GetComponentData<CameraTarget>(entity);
            forward = math.forward(cameraTarget.Rotation);
        }
        else if (entityManager.HasComponent<PlayerCameraSettings>(entity))
        {
            var cameraSettings = entityManager.GetComponentData<PlayerCameraSettings>(entity);
            var yawRotation = quaternion.Euler(0f, math.radians(cameraSettings.Yaw), 0f);
            forward = math.mul(yawRotation, new float3(0, 0, 1));
        }
        else
        {
            forward = math.forward(fallbackRotation);
        }

        forward.y = 0f;
        forward = math.normalizesafe(forward, new float3(0, 0, 1));
        flattenedCameraForward = forward;

        var moveInput = new float3(input.Horizontal, 0f, input.Vertical);
        if (math.lengthsq(moveInput) < 0.0001f)
        {
            return float3.zero;
        }

        float3 right = math.normalizesafe(new float3(forward.z, 0f, -forward.x), new float3(1, 0, 0));
        float3 desired = forward * moveInput.z + right * moveInput.x;
        return math.normalizesafe(desired, desired);
    }
    
    /// <summary>
    /// Burst-compatible FromToRotation - calculates quaternion that rotates from one direction to another.
    /// </summary>
    private static quaternion FromToRotation(float3 from, float3 to)
    {
        from = math.normalize(from);
        to = math.normalize(to);
        
        float dot = math.dot(from, to);
        
        // If vectors are nearly identical
        if (dot > 0.99999f)
            return quaternion.identity;
        
        // If vectors are nearly opposite
        if (dot < -0.99999f)
        {
            // Find orthogonal axis
            float3 axis = math.cross(new float3(1, 0, 0), from);
            if (math.lengthsq(axis) < 0.0001f)
                axis = math.cross(new float3(0, 1, 0), from);
            axis = math.normalize(axis);
            return quaternion.AxisAngle(axis, math.PI);
        }
        
        float3 cross = math.cross(from, to);
        float w = math.sqrt(math.lengthsq(from) * math.lengthsq(to)) + dot;
        
        return math.normalize(new quaternion(cross.x, cross.y, cross.z, w));
    }
    

}
