using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Abilities;
using Player.Components; // PlayerInput, PlayerState, PlayerInputPreferences

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// Crouch system with 13.15 parity features and optimizations:
    /// - 13.15.1: Standup obstruction check (prevents clipping through ceilings)
    /// - 13.15.4: Block sprint while crouched (if configured)
    /// - 13.15.O1: Edge-triggered raycast (only check on input state change)
    /// - 13.15.O2: Parallelized via IJobEntity
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    public partial struct CrouchSystem : ISystem
    {
        private const int CROUCH_MODIFIER_ID = 101;

        private ComponentLookup<PlayerInputPreferences> _prefsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _prefsLookup = state.GetComponentLookup<PlayerInputPreferences>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _prefsLookup.Update(ref state);
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            new CrouchJob
            {
                PhysicsWorld = physicsWorld,
                PrefsLookup = _prefsLookup,
                CrouchModifierId = CROUCH_MODIFIER_ID
            }.ScheduleParallel();
        }

        /// <summary>
        /// Parallelized crouch job with edge-triggered obstruction check.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct CrouchJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public ComponentLookup<PlayerInputPreferences> PrefsLookup;
            public int CrouchModifierId;

            void Execute(
                Entity entity,
                ref CrouchAbility crouchAbility,
                in CrouchSettings crouchSettings,
                in PlayerInput input,
                ref PlayerState playerState,
                in LocalTransform transform,
                ref DynamicBuffer<SpeedModifier> modifiers)
            {
                // Read Preferences
                var prefs = PrefsLookup.HasComponent(entity)
                    ? PrefsLookup[entity]
                    : PlayerInputPreferences.Default;

                bool isPressed = input.Crouch.IsSet;
                bool wasPressed = crouchAbility.CrouchPressed;
                bool wantsToCrouch;

                // Sync internal pressed state
                crouchAbility.CrouchPressed = isPressed;

                if (prefs.CrouchToggle)
                {
                    // Toggle on press
                    if (isPressed && !wasPressed)
                    {
                        playerState.CrouchToggled = !playerState.CrouchToggled;
                    }
                    wantsToCrouch = playerState.CrouchToggled;
                }
                else
                {
                    // Hold
                    wantsToCrouch = isPressed;
                    playerState.CrouchToggled = false;
                }

                bool currentlyIsCrouching = crouchAbility.IsCrouching;
                bool currentlyIsProne = playerState.Stance == PlayerStance.Prone;

                // ========================================================
                // 13.15.1 + 13.15.O1 + 13.15.P4: STANDUP/PRONE OBSTRUCTION CHECK
                // Check on EDGE TRIGGER:
                // - Crouch → Stand: was crouching AND just released
                // - Prone → Crouch: was prone AND crouch key pressed
                // ========================================================
                bool justReleasedCrouch = currentlyIsCrouching && !wantsToCrouch;
                
                // Also re-check if previously blocked and still trying to stand
                bool retryStandup = currentlyIsCrouching && crouchAbility.StandupBlocked && !wantsToCrouch;
                
                // 13.15.P4: Check obstruction when rising from prone to crouch
                bool proneToTransition = currentlyIsProne && (isPressed && !wasPressed);
                
                if (justReleasedCrouch || retryStandup)
                {
                    // Player wants to stand up from crouch - check for ceiling obstruction
                    float3 position = transform.Position;
                    float3 up = math.up();

                    float standingHeight = crouchSettings.StandingHeight;
                    float radius = crouchAbility.OriginalRadius > 0
                        ? crouchAbility.OriginalRadius
                        : crouchSettings.CrouchRadius;
                    float spacing = crouchSettings.ColliderSpacing;

                    // Check if standing capsule would overlap world geometry
                    bool standupBlocked = CheckStandupObstruction(
                        in PhysicsWorld,
                        entity,
                        position,
                        up,
                        standingHeight,
                        radius,
                        spacing);

                    crouchAbility.StandupBlocked = standupBlocked;

                    if (standupBlocked)
                    {
                        // Cannot stand - force stay crouched
                        wantsToCrouch = true;
                        // Keep toggle state consistent
                        if (prefs.CrouchToggle)
                        {
                            playerState.CrouchToggled = true;
                        }
                    }
                }
                else if (proneToTransition)
                {
                    // 13.15.P4: Player wants to rise from prone - check for crouch height clearance
                    float3 position = transform.Position;
                    float3 up = math.up();

                    float crouchHeight = crouchSettings.CrouchHeight;
                    float radius = crouchSettings.CrouchRadius;
                    float spacing = crouchSettings.ColliderSpacing;

                    // Check if crouch capsule would overlap world geometry
                    bool crouchBlocked = CheckStandupObstruction(
                        in PhysicsWorld,
                        entity,
                        position,
                        up,
                        crouchHeight,
                        radius,
                        spacing);

                    crouchAbility.StandupBlocked = crouchBlocked;
                    
                    if (crouchBlocked)
                    {
                        // Cannot rise to crouch - signal blocked (ProneSystem handles keeping prone)
                        // Note: ProneSystem has its own SafeStandCheck, but we set this flag
                        // so the UI can indicate the obstruction
                    }
                }
                else if (!currentlyIsCrouching && !currentlyIsProne)
                {
                    crouchAbility.StandupBlocked = false;
                }

                // Apply crouch state
                bool wasAlreadyCrouching = crouchAbility.IsCrouching;
                crouchAbility.IsCrouching = wantsToCrouch;

                // Apply to PlayerState
                // Only override if not Prone (Prone takes precedence)
                if (playerState.Stance != PlayerStance.Prone)
                {
                    PlayerStance oldStance = playerState.Stance;
                    
                    if (wantsToCrouch)
                    {
                        playerState.Stance = PlayerStance.Crouching;
                    }
                    else if (playerState.Stance == PlayerStance.Crouching)
                    {
                        // Only reset to standing if we were crouching
                        playerState.Stance = PlayerStance.Standing;
                    }
                    
                    // 13.15.O5: Set dirty flag if stance changed
                    if (oldStance != playerState.Stance)
                    {
                        crouchAbility.StanceDirty = true;
                    }
                }

                // Apply Speed Modifier
                int foundIndex = -1;
                for (int i = 0; i < modifiers.Length; i++)
                {
                    if (modifiers[i].SourceId == CrouchModifierId)
                    {
                        foundIndex = i;
                        break;
                    }
                }

                if (wantsToCrouch)
                {
                    var mod = new SpeedModifier
                    {
                        SourceId = CrouchModifierId,
                        Multiplier = crouchSettings.MovementSpeedMultiplier,
                        Duration = -1f,
                        ElapsedTime = 0f
                    };

                    if (foundIndex >= 0) modifiers[foundIndex] = mod;
                    else modifiers.Add(mod);
                }
                else
                {
                    if (foundIndex >= 0) modifiers.RemoveAt(foundIndex);
                }
            }

            /// <summary>
            /// 13.15.1: Check if standing up would cause collision with world geometry.
            /// Uses a raycast from current position to standing height.
            /// </summary>
            private static bool CheckStandupObstruction(
                in PhysicsWorld physicsWorld,
                Entity selfEntity,
                float3 position,
                float3 up,
                float standingHeight,
                float radius,
                float spacing)
            {
                // Raycast upward from current crouched position to standing height
                float currentCrouchTop = position.y + 1.0f; // Approximate crouched head height
                float standingTop = position.y + standingHeight;
                float checkDistance = standingTop - currentCrouchTop;

                if (checkDistance <= 0) return false;

                var rayStart = position + up * currentCrouchTop;
                var rayEnd = position + up * standingTop;

                var rayInput = new RaycastInput
                {
                    Start = rayStart,
                    End = rayEnd,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u, // Check against everything
                        GroupIndex = 0
                    }
                };

                var hits = new NativeList<RaycastHit>(4, Allocator.Temp);
                bool hasHit = physicsWorld.CastRay(rayInput, ref hits);

                bool blocked = false;
                if (hasHit)
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        if (hit.RigidBodyIndex >= 0 && hit.RigidBodyIndex < physicsWorld.Bodies.Length)
                        {
                            var hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;
                            if (hitEntity != selfEntity)
                            {
                                // Check if hit surface is mostly horizontal (ceiling-like)
                                float dot = math.dot(-up, hit.SurfaceNormal);
                                if (dot > 0.5f) // ~60 degree threshold for "ceiling"
                                {
                                    blocked = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                hits.Dispose();

                return blocked;
            }
        }
    }
}


