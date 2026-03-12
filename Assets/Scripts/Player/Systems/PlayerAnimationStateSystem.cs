using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;
using Player.Animation;
using DIG.Swimming;
using static Player.Animation.OpsiveAnimatorConstants;

/// <summary>
/// Derives lightweight animation parameters from authoritative gameplay state each prediction tick.
/// Runs on both server and predicting clients so the values stay deterministic and replicate correctly.
/// </summary>
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerStateSystem))]
public partial struct PlayerAnimationStateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
    }


    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var climbingLookup = SystemAPI.GetComponentLookup<FreeClimbState>(true);
        climbingLookup.Update(ref state);

        var swimmingLookup = SystemAPI.GetComponentLookup<SwimmingState>(true);
        swimmingLookup.Update(ref state);
        
        var rideLookup = SystemAPI.GetComponentLookup<RideState>(true);
        rideLookup.Update(ref state);

        // Agility ability lookups
        var dodgeLookup = SystemAPI.GetComponentLookup<DodgeState>(true);
        dodgeLookup.Update(ref state);

        var rollLookup = SystemAPI.GetComponentLookup<RollState>(true);
        rollLookup.Update(ref state);

        var vaultLookup = SystemAPI.GetComponentLookup<VaultState>(true);
        vaultLookup.Update(ref state);

        var crawlLookup = SystemAPI.GetComponentLookup<CrawlState>(true);
        crawlLookup.Update(ref state);

        // EPIC 15.20: Get paradigm settings for screen-relative movement mode
        bool useScreenRelativeMovement = false;
        float cameraYaw = 0f;
        if (SystemAPI.HasSingleton<DIG.Core.Input.ParadigmSettings>())
        {
            var paradigmSettings = SystemAPI.GetSingleton<DIG.Core.Input.ParadigmSettings>();
            useScreenRelativeMovement = paradigmSettings.IsValid && paradigmSettings.UseScreenRelativeMovement;
            cameraYaw = paradigmSettings.CameraYaw;
        }
        
        // EPIC 15.20: Cache CameraData for screen-relative animation calculations
        var cameraData = new DIG.Voxel.Components.CameraData();
        bool hasCameraData = SystemAPI.TryGetSingleton<DIG.Voxel.Components.CameraData>(out cameraData);

        // Only update animation state for locally simulated entities (owner + server)
        // Remote ghosts receive replicated PlayerAnimationState values via GhostField

        // Process entities
        foreach (var (animationState, playerState, playerInput, velocity, transform, entity) in
                 SystemAPI.Query<RefRW<PlayerAnimationState>, RefRO<PlayerState>, RefRO<PlayerInput>, RefRO<PhysicsVelocity>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
        {
            ref var anim = ref animationState.ValueRW;
            var pState = playerState.ValueRO;
            var input = playerInput.ValueRO;
            var vel = velocity.ValueRO;

            // Check FreeClimbState for climbing status
            bool hasClimbing = climbingLookup.HasComponent(entity);
            int prevAbilityIndex = anim.AbilityIndex;

            if (hasClimbing)
            {
                var cs = climbingLookup[entity];
                
                // Legacy params (keep for backward compatibility)
                anim.IsClimbing = cs.IsClimbing;
                anim.ClimbProgress = cs.IsClimbing ? 0.5f : 0f;
                
                // Debug: Log climb state detection (commented out to reduce spam)
                // if (cs.IsClimbing || cs.IsTransitioning || cs.IsClimbingUp)
                // {
                //     if (cs.IsClimbingUp)
                //     {
                //         UnityEngine.Debug.Log($"[AnimState] CLIMB_UP DETECTED: IsClimbing={cs.IsClimbing}, IsTransitioning={cs.IsTransitioning}, IsClimbingUp={cs.IsClimbingUp}, prev={prevAbilityIndex}, setting AbilityIntData=6");
                //     }
                // }
                
                // 13.26: Opsive ability state mapping
                if (cs.IsClimbing || cs.IsTransitioning || cs.IsClimbingUp)
                {
                    // EPIC 14.24: Check for Free Hang (Ability 104)
                    // Trigger on IsHangTransitioning OR IsFreeHanging so animation plays during entry
                    // Also STAY in hang if already in hang ability (prevents flicker from detection)
                    // Skip if doing regular transition (mount) - but KEEP for IsClimbingUp (Pull Up is Hang ability!)
                    bool alreadyInHang = anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_HANG;
                    bool wantHang = cs.IsHangTransitioning || cs.IsFreeHanging;
                    
                    // EPIC 14.24: Pull Up (vault from hang) uses Hang ability with different IntData
                    bool isPullUpFromHang = cs.IsClimbingUp && (alreadyInHang || cs.IsFreeHanging || cs.IsHangTransitioning);
                    
                    bool shouldPlayHangAnim = ((wantHang || alreadyInHang) && !cs.IsTransitioning) || isPullUpFromHang;
                    
                    if (shouldPlayHangAnim)
                    {
                         // Use Hang ability (104) for proper hang animation
                         anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_HANG; // 104
                         
                         if (isPullUpFromHang)
                         {
                             // Pull Up animation - IntData 7 = Pull Up in Hang ability (from MemuAnim.controller)
                             anim.AbilityIntData = 7;
                             // EPIC 15.20: Debug removed for Burst compatibility
                             // [VAULT_DBG] PULL UP ANIM: AbilityIndex=104, IntData=7
                         }
                         else if (anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_HANG)
                         {
                             // Just entering hang - use transition IntData
                             anim.AbilityIntData = 10; // FreeClimbToHangVertical transition animation
                         }
                         else if (!cs.IsHangTransitioning)
                         {
                             // Already in Hang and not transitioning - use shimmy idle
                             if (anim.AbilityIntData != 2)
                             {
                                 anim.AbilityIntData = 2;
                             }
                         }
                         // During IsHangTransitioning, keep IntData=10 for entry animation
                    }
                    else
                    {
                        // Active climbing - set AbilityIndex to FreeClimb (503)
                        bool wasInHang = anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_HANG;
                        bool wasNotClimbing = anim.AbilityIndex != 503 && anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_HANG;
                        
                        anim.AbilityIndex = 503; // OpsiveAnimatorConstants.ABILITY_FREE_CLIMB
                        
                        // Determine sub-state (AbilityIntData) - prioritize stability
                        if (cs.IsClimbingUp)
                        {
                            // Vaulting over ledge - always set this
                            anim.AbilityIntData = 6; // CLIMB_TOP_DISMOUNT
                        }
                        else if (cs.IsTransitioning)
                        {
                            // During mount transition - only set if not already in a mount/transition state
                            // This prevents resetting mid-transition
                            if (anim.AbilityIntData != 0 && anim.AbilityIntData != 1 && anim.AbilityIntData != 8)
                            {
                                if (wasInHang)
                                {
                                    anim.AbilityIntData = 8; // CLIMB_VERTICAL_HANG_START
                                }
                                else if (wasNotClimbing)
                                {
                                    anim.AbilityIntData = 0; // CLIMB_BOTTOM_MOUNT
                                }
                                // else: keep current IntData during transition
                            }
                        }
                        else
                        {
                            // Normal climbing (not transitioning)
                            // Only switch to climbing state if we're past the mount/transition phases
                            // This is the key fix: don't reset IntData if already in a valid climbing state
                            if (anim.AbilityIntData == 6)
                            {
                                // Was vaulting, now done - go to climbing
                                anim.AbilityIntData = 2; // CLIMB_CLIMBING
                            }
                            else if (anim.AbilityIntData != 2)
                            {
                                // Mount animation should complete before switching to climbing
                                // Give mount animations time (0, 1, 8 are mount states)
                                // Once we're in steady climbing, set to 2
                                anim.AbilityIntData = 2; // CLIMB_CLIMBING
                            }
                            // else: already at 2, don't change
                        }
                    }
                    
                    // Movement direction for blend trees
                    anim.AbilityFloatData = input.Horizontal;
                }
                else if (prevAbilityIndex == 503 && !cs.IsClimbing)
                {
                    // Was climbing, now stopped - trigger dismount
                    anim.AbilityIntData = 5; // CLIMB_BOTTOM_DISMOUNT
                    // NOTE: AbilityChange is now tracked by ClimbAnimatorBridge locally
                    anim.AbilityIndex = 0; // Clear after one frame
                }
                else
                {
                    // Not climbing - reset ability state BUT preserve abilities handled by UpdateAnimationState
                    // Jump=1, Fall=2, HeightChange=3
                    if (anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_HEIGHT_CHANGE &&
                        anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_JUMP &&
                        anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_FALL &&
                        anim.AbilityIndex != ABILITY_DODGE &&
                        anim.AbilityIndex != ABILITY_ROLL &&
                        anim.AbilityIndex != ABILITY_VAULT)
                    {
                        anim.AbilityIndex = 0;
                        anim.AbilityIntData = 0; // Critical: Reset to 0 so next mount uses CLIMB_BOTTOM_MOUNT
                        anim.AbilityFloatData = 0f;
                        // NOTE: AbilityChange tracking moved to ClimbAnimatorBridge
                    }
                }
                
                // NOTE: AbilityChange trigger is now handled by ClimbAnimatorBridge
                // using local state tracking to avoid race conditions with prediction ticks
                if (prevAbilityIndex != anim.AbilityIndex && anim.AbilityIndex != 0)
                {
                    // Log removed
                }
                
                // FALLBACK GUARANTEE: If ECS says NOT climbing, force AbilityIndex to 0
                // This catches ANY edge case where the above logic didn't clear climb state
                // EPIC 14.24: Also preserve if in hang transition or active hang
                if (!cs.IsClimbing && !cs.IsTransitioning && !cs.IsClimbingUp && 
                    !cs.IsHangTransitioning && !cs.IsFreeHanging)
                {
                    if (anim.AbilityIndex == 503 || anim.AbilityIndex == 104)
                    {
                        // Log removed
                        anim.AbilityIndex = 0;
                        anim.AbilityIntData = 0;
                    }
                }
            }
            else
            {
                anim.IsClimbing = false;
                anim.ClimbProgress = 0f;
                // Only reset ability state if not in abilities handled by UpdateAnimationState or climbing
                // Jump=1, Fall=2, HeightChange=3, FreeClimb=503
                if (anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_HEIGHT_CHANGE &&
                    anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_JUMP &&
                    anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_FALL &&
                    anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_FREE_CLIMB &&
                    anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_SWIM &&
                    anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_DIVE &&
                    anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_CLIMB_FROM_WATER &&
                    anim.AbilityIndex != OpsiveAnimatorConstants.ABILITY_DROWN &&
                    anim.AbilityIndex != ABILITY_DODGE &&
                    anim.AbilityIndex != ABILITY_ROLL &&
                    anim.AbilityIndex != ABILITY_VAULT)
                {
                    anim.AbilityIndex = 0;
                    anim.AbilityIntData = 0;
                    anim.AbilityFloatData = 0f;
                }
            }

            // Check SwimmingState for swimming animation
            bool hasSwimming = swimmingLookup.HasComponent(entity);

            if (hasSwimming)
            {
                var ss = swimmingLookup[entity];
                anim.IsSwimming = ss.IsSwimming;
                anim.IsUnderwater = ss.IsSubmerged;

                if (ss.IsSwimming)
                {
                    int prevIdx = anim.AbilityIndex;
                    anim.AbilityIndex = ABILITY_SWIM; // 301
                    
                    // Determine AbilityIntData (Swim State)
                    // Priority: Enter > Exit > Underwater/Surface
                    
                    // Note: We need to access SwimmingEvents for OnEnterWater, but it's not in the query yet.
                    // Ideally we should add RefRO<SwimmingEvents> to the query.
                    // For now, we'll rely on state derived from SwimmingState.
                    
                    if (ss.IsSubmerged)
                    {
                        anim.AbilityIntData = SWIM_UNDERWATER; // 2
                    }
                    else
                    {
                        anim.AbilityIntData = SWIM_SURFACE; // 1
                    }
                    
                    // Map Vertical input to Pitch (AbilityFloatData)
                    // Opsive uses AbilityFloatData for pitch-based movement blending underwater
                    float pitch = 0f;
                    if (input.Vertical > 0.1f) pitch = 1f;
                    else if (input.Vertical < -0.1f) pitch = -1f;
                    
                    // Invert pitch if looking down? Opsive usually maps +1 to Up, -1 to Down
                    anim.AbilityFloatData = pitch; 

                    if (prevIdx != ABILITY_SWIM)
                    {
                        anim.AbilityChange = true;
                        // Use Enter from Air if we just started swimming and were falling/airborne
                        // but ideally we'd use the OnEnterWater event. 
                        // Defaulting to Surface/Underwater is safer for state syncing.
                        // [FIX] Don't overwrite what we just set above!
                        // anim.AbilityIntData = ss.IsSubmerged ? SWIM_UNDERWATER : SWIM_ENTER_FROM_AIR;
                        
                        // EPIC 15.20: Debug removed for Burst compatibility
                    }
                }
                else
                {
                    // Clear swimming ability if we were swimming
                    if (anim.AbilityIndex == ABILITY_SWIM) 
                    {
                        // EPIC 15.20: Debug removed for Burst compatibility
                        
                        anim.AbilityIndex = 0;
                        anim.AbilityIntData = 0;
                        anim.AbilityChange = true;
                    }
                }
            }
            else
            {
                anim.IsSwimming = false;
                anim.IsUnderwater = false;
            }
            
            // Check RideState for mount/ride animations (Priority over other abilities when active)
            bool hasRiding = rideLookup.HasComponent(entity);
            if (hasRiding)
            {
                var rideState = rideLookup[entity];
                
                // If in any ride phase (mounting, riding, or dismounting), override animation state
                if (rideState.RidePhase != RidePhaseConstants.None)
                {
                    int prevIdx = anim.AbilityIndex;
                    int prevIntData = anim.AbilityIntData;
                    
                    anim.AbilityIndex = ABILITY_RIDE; // 12
                    
                    // Set AbilityIntData based on phase and side
                    anim.AbilityIntData = rideState.RidePhase switch
                    {
                        RidePhaseConstants.Mounting => rideState.FromLeftSide 
                            ? RIDE_MOUNT_LEFT   // 1
                            : RIDE_MOUNT_RIGHT, // 2
                        RidePhaseConstants.Riding => RIDE_RIDING, // 3
                        RidePhaseConstants.Dismounting => rideState.FromLeftSide 
                            ? RIDE_DISMOUNT_LEFT   // 4
                            : RIDE_DISMOUNT_RIGHT, // 5
                        RidePhaseConstants.DismountComplete => RIDE_COMPLETE, // 6 - animator exits Ride layer
                        _ => RIDE_RIDING // Default to riding
                    };
                    
                    // AbilityFloatData can be used for blend direction if needed
                    anim.AbilityFloatData = 0f;
                }
                else if (anim.AbilityIndex == ABILITY_RIDE)
                {
                    // CRITICAL: Reset animator when ride ends (RidePhase == None but was riding)
                    // This allows the animator to transition back to locomotion
                    anim.AbilityIndex = 0;
                    anim.AbilityIntData = 0;
                    anim.AbilityFloatData = 0f;
                }
            }

            // === AGILITY ABILITIES (101-107) ===
            // Check agility states and set animation parameters
            // Priority: Dodge > Roll > Vault (all higher than crouch, lower than fall)

            // Dodge (101) - highest agility priority
            bool hasDodge = dodgeLookup.HasComponent(entity);
            if (hasDodge)
            {
                var ds = dodgeLookup[entity];
                if (ds.IsDodging)
                {
                    int prevIdx = anim.AbilityIndex;
                    anim.AbilityIndex = ABILITY_DODGE; // 101
                    anim.AbilityIntData = ds.Direction; // 0=Left, 1=Right, 2=Forward, 3=Backward
                    anim.AbilityFloatData = 0f;

                    if (prevIdx != ABILITY_DODGE)
                    {
                        anim.AbilityChange = true;
                    }
                    // Skip UpdateAnimationState - dodge takes priority
                    continue;
                }
            }

            // Roll (102)
            bool hasRoll = rollLookup.HasComponent(entity);
            if (hasRoll)
            {
                var rs = rollLookup[entity];
                if (rs.IsRolling)
                {
                    int prevIdx = anim.AbilityIndex;
                    anim.AbilityIndex = ABILITY_ROLL; // 102
                    anim.AbilityIntData = rs.RollType; // 0=Left, 1=Right, 2=Forward, 3=Land
                    anim.AbilityFloatData = 0f;

                    if (prevIdx != ABILITY_ROLL)
                    {
                        anim.AbilityChange = true;
                    }
                    // Skip UpdateAnimationState - roll takes priority
                    continue;
                }
            }

            // Vault (105)
            bool hasVault = vaultLookup.HasComponent(entity);
            if (hasVault)
            {
                var vs = vaultLookup[entity];
                if (vs.IsVaulting)
                {
                    int prevIdx = anim.AbilityIndex;
                    anim.AbilityIndex = ABILITY_VAULT; // 105
                    // IntData = height * 1000 (Opsive convention)
                    anim.AbilityIntData = (int)(vs.VaultHeight * 1000f);
                    // FloatData = starting velocity for animation speed
                    anim.AbilityFloatData = vs.StartVelocity;

                    if (prevIdx != ABILITY_VAULT)
                    {
                        anim.AbilityChange = true;
                    }
                    // Skip UpdateAnimationState - vault takes priority
                    continue;
                }
            }

            // Crawl (103) - active when prone and moving
            bool hasCrawl = crawlLookup.HasComponent(entity);
            if (hasCrawl)
            {
                var cs = crawlLookup[entity];
                if (cs.IsCrawling)
                {
                    int prevIdx = anim.AbilityIndex;
                    anim.AbilityIndex = ABILITY_CRAWL; // 103
                    anim.AbilityIntData = cs.CrawlSubState; // 0=Active, 1=Stopping
                    anim.AbilityFloatData = 0f;

                    if (prevIdx != ABILITY_CRAWL)
                    {
                        anim.AbilityChange = true;
                    }
                    // Skip UpdateAnimationState - crawl takes priority
                    continue;
                }
            }

            // MMO Tank Turn: Zero out horizontal input for animation when tank turning
            // This prevents strafe animation from playing when A/D rotates the character
            // Use paradigmSettings singleton (reliable) and direct RMB input check
            var animInput = input;
            float turnYaw = 0f; // EPIC 15.20: Yaw for turn animation blend tree
            if (SystemAPI.HasSingleton<DIG.Core.Input.ParadigmSettings>())
            {
                var paradigmSettings = SystemAPI.GetSingleton<DIG.Core.Input.ParadigmSettings>();
                bool adTurnsCharacter = paradigmSettings.IsValid && paradigmSettings.ADTurnsCharacter;
                bool isRmbHeld = input.AltUse.IsSet;
                
                // Tank turn = ADTurnsCharacter enabled AND not strafing (RMB = strafing)
                bool isTankTurning = adTurnsCharacter && !isRmbHeld;
                
                if (isTankTurning)
                {
                    // Zero horizontal so animator doesn't play strafe, just idle/turn-in-place
                    animInput.Horizontal = 0;
                    // Set Yaw for turn animation blend tree (A = left = -1, D = right = +1)
                    turnYaw = input.Horizontal;
                }
                
                // EPIC 15.21: Inject Explicit Strafe (Q/E) into animation input
                // This ensures Q/E plays strafe animations even if A/D are used for turning (or idle)
                if (adTurnsCharacter)
                {
                    int mmoStrafe = 0;
                    if (input.MMOStrafeLeft.IsSet) mmoStrafe -= 1;
                    if (input.MMOStrafeRight.IsSet) mmoStrafe += 1;
                    
                    if (mmoStrafe != 0)
                    {
                        animInput.Horizontal += mmoStrafe;
                        animInput.Horizontal = math.clamp(animInput.Horizontal, -1, 1);
                    }
                }
            }
            
            // Set Yaw on animation state for turn-in-place animations
            anim.Yaw = turnYaw;

            UpdateAnimationState(ref anim, pState, animInput, vel, transform.ValueRO, useScreenRelativeMovement, hasCameraData, cameraData, cameraYaw);
        }
    }
    
    private static void UpdateAnimationState(
        ref PlayerAnimationState anim, 
        PlayerState pState, 
        PlayerInput input, 
        PhysicsVelocity vel,
        LocalTransform transform,
        bool useScreenRelativeMovement,
        bool hasCameraData,
        DIG.Voxel.Components.CameraData cameraData,
        float cameraYaw)
    {
        // --- Basic movement parameters (always set) ---
        // EPIC 15.20: When path-following (click-to-move), use continuous PathMove direction
        // instead of Horizontal/Vertical which are zero (WASD not pressed)
        var move = (input.IsPathFollowing != 0)
            ? new float2(input.PathMoveX, input.PathMoveY)
            : new float2(input.Horizontal, input.Vertical);
        float moveLen = math.length(move);
        if (moveLen > 1f)
        {
            move /= moveLen;
            moveLen = 1f;
        }
        
        // EPIC 15.20: In screen-relative (isometric/ARPG) mode, animation depends on attack state
        if (useScreenRelativeMovement && moveLen > 0.1f)
        {
            bool isAttacking = input.Use.IsSet;
            
            if (isAttacking)
            {
                // ATTACKING: Use full movement animations (forward, strafe, backpedal with 0.7 speed)
                // Character faces cursor, so we need character-relative movement
                // Calculate movement direction in world space
                float3 camForward = float3.zero;
                float3 camRight = float3.zero;
                
                if (hasCameraData && cameraData.IsValid)
                {
                    camForward = cameraData.Forward;
                    camRight = cameraData.Right;
                }
                
                // Fallback to CameraYaw if CameraData not available
                if (math.lengthsq(camForward) < 0.01f)
                {
                    var yawRotation = quaternion.Euler(0f, math.radians(cameraYaw), 0f);
                    camForward = math.mul(yawRotation, new float3(0, 0, 1));
                    camRight = math.mul(yawRotation, new float3(1, 0, 0));
                }
                
                // Flatten camera vectors to XZ plane
                camForward.y = 0;
                camRight.y = 0;
                camForward = math.normalizesafe(camForward);
                camRight = math.normalizesafe(camRight);
                
                // World-space movement direction from screen input
                // Use 'move' (which already handles path-following) instead of raw input
                float3 worldMoveDir = (camForward * move.y) + (camRight * move.x);
                worldMoveDir.y = 0;
                worldMoveDir = math.normalizesafe(worldMoveDir);
                
                // Get character's facing direction from transform
                float3 charForward = math.mul(transform.Rotation, new float3(0, 0, 1));
                float3 charRight = math.mul(transform.Rotation, new float3(1, 0, 0));
                charForward.y = 0;
                charRight.y = 0;
                charForward = math.normalizesafe(charForward);
                charRight = math.normalizesafe(charRight);
                
                // Project movement onto character's local axes
                // Dot with forward = forward/backward component
                // Dot with right = strafe component
                float forwardComponent = math.dot(worldMoveDir, charForward) * moveLen;
                float rightComponent = math.dot(worldMoveDir, charRight) * moveLen;
                
                // Use character-relative input for blend tree
                move = new float2(rightComponent, forwardComponent);
            }
            else
            {
                // NOT ATTACKING: Character faces movement direction
                // From their perspective, they're always moving forward
                // Use forward-only animation
                move = new float2(0f, moveLen);
            }
        }
        // Else: Standard mode (Shooter, MMO, etc.) - pass through raw input for strafe/forward/back
        // MMO A/D turning is handled by the animator's blend tree using the input values directly
        
        // NOTE: MMO tank turn horizontal zeroing is handled in the main loop before calling this method
        
        anim.MoveInput = move;

        var planarVel = new float2(vel.Linear.x, vel.Linear.z);
        anim.MoveSpeed = math.length(planarVel);
        anim.VerticalSpeed = vel.Linear.y;

        float lean = 0f;
        if (input.LeanLeft.IsSet)
            lean = -1f;
        if (input.LeanRight.IsSet)
            lean = 1f;
        anim.Lean = lean;

        // Copy replicated state values
        anim.MovementState = pState.MovementState;
        anim.IsGrounded = pState.IsGrounded;
        anim.IsJumping = pState.MovementState == PlayerMovementState.Jumping;
        anim.IsCrouching = pState.Stance == PlayerStance.Crouching || pState.Stance == PlayerStance.Prone;
        anim.IsProne = pState.Stance == PlayerStance.Prone;
        anim.IsSprinting = pState.MovementState == PlayerMovementState.Sprinting;
        anim.IsSliding = false;
        
        // 13.15.3: Height animator parameter (0 = Standing, 1 = Crouching, 2 = Prone)
        if (pState.Stance == PlayerStance.Prone)
            anim.Height = 2;
        else if (pState.Stance == PlayerStance.Crouching)
            anim.Height = 1;
        else
            anim.Height = 0;
        
        // --- Opsive Ability State Machine ---
        // Track previous ability for change detection
        int prevAbilityIndex = anim.AbilityIndex;
        
        // Skip if climbing (handled elsewhere, AbilityIndex = 503)
        if (anim.IsClimbing || anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_FREE_CLIMB)
        {
            return;
        }

        // [FIX] Skip if swimming (AbilityIndex = 301-304) to prevent overwrite
        if (anim.IsSwimming || 
            anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_SWIM ||
            anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_DIVE ||
            anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_CLIMB_FROM_WATER ||
            anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_DROWN)
        {
            return;
        }
        
        // Skip if riding (handled elsewhere, AbilityIndex = 401)
        if (anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_RIDE)
        {
            return;
        }
        
        // Priority 1: Jumping (AbilityIndex = 1)
        // Active when jumping with upward velocity
        // NOTE: Controller requires AbilityIntData=1 for Jump Start animations
        bool isJumpingUp = anim.IsJumping && anim.VerticalSpeed > 0.1f;
        if (isJumpingUp)
        {
            anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_JUMP;
            anim.AbilityIntData = OpsiveAnimatorConstants.JUMP_INT_LANDED; // 1 = Jump Start (controller requires this)
            if (prevAbilityIndex != OpsiveAnimatorConstants.ABILITY_JUMP)
            {
                anim.AbilityChange = true;
                // UnityEngine.Debug.Log("[AnimState] JUMP START");
            }
            else if (anim.AbilityChange)
            {
                anim.AbilityChange = false;
            }
            return;
        }
        
        // Priority 2: Falling (AbilityIndex = 2)  
        // When airborne with significant downward velocity
        // Two-tier thresholds to prevent slope flickering while allowing fast downhill:
        // - To START falling: require significant velocity (-2.0f)
        // - To CONTINUE falling: use lower threshold (-0.5f) so we don't flicker out
        bool wasAlreadyFalling = prevAbilityIndex == OpsiveAnimatorConstants.ABILITY_FALL;
        float fallThreshold = wasAlreadyFalling ? -0.5f : -2.0f;
        bool isFalling = !pState.IsGrounded && anim.VerticalSpeed < fallThreshold;
        if (isFalling)
        {
            anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_FALL;
            anim.AbilityIntData = OpsiveAnimatorConstants.FALL_INT_FALLING; // 0 = falling
            anim.AbilityFloatData = anim.VerticalSpeed; // Vertical velocity for fall blend tree
            if (!wasAlreadyFalling)
            {
                // Debug log removed - Burst doesn't support F2 format
            }
            return;
        }
        
        // Priority 3: Just landed (transition from Jump/Fall to Locomotion)
        // Only trigger landing on the FIRST frame we touch ground from a jump/fall
        // After that, AbilityIntData will be FALL_INT_LANDED (1), so we skip to locomotion
        bool wasInAir = prevAbilityIndex == OpsiveAnimatorConstants.ABILITY_JUMP || 
                        (prevAbilityIndex == OpsiveAnimatorConstants.ABILITY_FALL && 
                         anim.AbilityIntData == OpsiveAnimatorConstants.FALL_INT_FALLING);
        bool justLanded = pState.IsGrounded && wasInAir;
        if (justLanded)
        {
            // Set Fall with Landed IntData for landing animation
            anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_FALL;
            anim.AbilityIntData = OpsiveAnimatorConstants.FALL_INT_LANDED; // 1 = landed
            anim.AbilityFloatData = 0f; // Reset on landing
            anim.AbilityChange = true;
                // UnityEngine.Debug.Log("[AnimState] LANDED - transitioning to locomotion");
            return;
        }
        
        // Priority 4: Crouching (AbilityIndex = 3)
        bool isCrouching = pState.Stance == PlayerStance.Crouching;
        if (isCrouching)
        {
            anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_HEIGHT_CHANGE;
            anim.AbilityIntData = OpsiveAnimatorConstants.CROUCH_INT_DATA; // 1
            if (prevAbilityIndex != OpsiveAnimatorConstants.ABILITY_HEIGHT_CHANGE)
            {
                anim.AbilityChange = true;
            }
            else if (anim.AbilityChange)
            {
                anim.AbilityChange = false;
            }
            return;
        }
        
        // Priority 5: Normal locomotion (AbilityIndex = 0)
        // This is the default state - allows Adventure Movement to play
        
        // Trigger AbilityChange when returning from any ability to locomotion
        if (prevAbilityIndex != OpsiveAnimatorConstants.ABILITY_NONE)
        {
            anim.AbilityChange = true;
            // UnityEngine.Debug.Log($"[AnimState] Returning to locomotion from {prevAbilityIndex}");
        }
        else if (anim.AbilityChange)
        {
            anim.AbilityChange = false;
        }
        
        anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_NONE;
        anim.AbilityIntData = 0;
    }
}
