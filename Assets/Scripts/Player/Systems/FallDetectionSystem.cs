using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;
using DIG.Player.Abilities;
using DIG.Surface;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 13.14: Fall Detection System with Opsive Feature Parity
    ///
    /// Features:
    /// - 13.14.1: Minimum fall height check (raycast to ground before starting fall)
    /// - 13.14.2: Land surface impact requests (VFX/audio on landing)
    /// - 13.14.3: Animation event wait for land complete
    /// - 13.14.4: Blend tree float data (FallVelocity sent to animator)
    /// - 13.14.5: Immediate transform change handling (teleport ends fall cleanly)
    /// - 13.14.6: State index for animation (0 = falling, 1 = landed)
    ///
    /// This system runs in the predicted simulation group for proper netcode prediction.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerGroundCheckSystem))]
    public partial struct FallDetectionSystem : ISystem
    {
        private ComponentLookup<FallDamageSettings> _fallDamageSettingsLookup;
        private ComponentLookup<ApplyDamage> _applyDamageLookup;
        private ComponentLookup<CameraShake> _cameraShakeLookup;
        private ComponentLookup<LandingEvent> _landingEventLookup;
        private ComponentLookup<LandingState> _landingStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _fallDamageSettingsLookup = state.GetComponentLookup<FallDamageSettings>(true);
            _applyDamageLookup = state.GetComponentLookup<ApplyDamage>(false);
            _cameraShakeLookup = state.GetComponentLookup<CameraShake>(false);
            _landingEventLookup = state.GetComponentLookup<LandingEvent>(false);
            _landingStateLookup = state.GetComponentLookup<LandingState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            // Update lookups
            _fallDamageSettingsLookup.Update(ref state);
            _applyDamageLookup.Update(ref state);
            _cameraShakeLookup.Update(ref state);
            _landingEventLookup.Update(ref state);
            _landingStateLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (playerStateRef, fallRef, fallSettingsRef, fallAbilityRef, animStateRef,
                         localTransformRef, velocityRef, entity) in
                     SystemAPI.Query<
                         RefRW<PlayerState>,
                         RefRW<FallState>,
                         RefRO<FallSettings>,
                         RefRW<FallAbility>,
                         RefRW<PlayerAnimationState>,
                         RefRO<LocalTransform>,
                         RefRO<PhysicsVelocity>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var playerState = ref playerStateRef.ValueRW;
                ref var fall = ref fallRef.ValueRW;
                var fallSettings = fallSettingsRef.ValueRO;
                ref var fallAbility = ref fallAbilityRef.ValueRW;
                ref var animState = ref animStateRef.ValueRW;
                var position = localTransformRef.ValueRO.Position;
                var velocity = velocityRef.ValueRO.Linear;
                var up = localTransformRef.ValueRO.Up();

                bool grounded = playerState.IsGrounded;
                float verticalVelocity = velocity.y;

                // --- 13.14.5: Handle immediate transform change (teleport) ---
                if (fallAbility.PendingImmediateTransformChange)
                {
                    fallAbility.PendingImmediateTransformChange = false;

                    if (grounded && fallAbility.IsFalling)
                    {
                        // Teleported to ground while falling - force end fall
                        fallAbility.Landed = true;
                        fallAbility.StateIndex = 1;
                        fallAbility.IsFalling = false;
                        fallAbility.WaitingForAnimationEvent = false;

                        fall.IsFalling = false;
                        fall.FallDistance = 0f;

                        animState.IsFalling = false;
                        animState.FallStateIndex = 1;
                        animState.FallVelocity = 0f;
                        playerState.MovementState = PlayerMovementState.Idle;
                    }
                }

                // --- 13.14.3: Handle animation event wait timeout ---
                if (fallAbility.WaitingForAnimationEvent)
                {
                    fallAbility.AnimationEventTimer += deltaTime;

                    // Check if animation complete event was received
                    if (SystemAPI.IsComponentEnabled<FallAnimationComplete>(entity))
                    {
                        // Event received - complete the landing
                        SystemAPI.SetComponentEnabled<FallAnimationComplete>(entity, false);
                        fallAbility.WaitingForAnimationEvent = false;
                        fallAbility.Landed = true;

                        // Can now transition to next movement state
                        if (grounded)
                        {
                            playerState.MovementState = PlayerMovementState.Idle;
                        }
                    }
                    else if (fallAbility.AnimationEventTimer >= fallAbility.AnimationEventTimeout)
                    {
                        // Timeout - force complete
                        fallAbility.WaitingForAnimationEvent = false;
                        fallAbility.Landed = true;

                        if (grounded)
                        {
                            playerState.MovementState = PlayerMovementState.Idle;
                        }
                    }
                }

                // --- 13.14.4: Update blend tree float data every frame ---
                // Send vertical velocity to animator regardless of fall state
                animState.FallVelocity = verticalVelocity;
                animState.FallStateIndex = fallAbility.StateIndex;
                animState.IsFalling = fallAbility.IsFalling;

                // --- Detect start of fall ---
                if (!fall.IsFalling && !grounded && verticalVelocity < -0.1f)
                {
                    // --- 13.14.1: Minimum fall height check ---
                    bool canStartFall = true;

                    if (fallSettings.MinFallHeight > 0f)
                    {
                        // Raycast down to check ground distance
                        var rayInput = new RaycastInput
                        {
                            Start = position,
                            End = position - up * fallSettings.MinFallHeight,
                            Filter = new CollisionFilter
                            {
                                BelongsTo = ~0u,
                                CollidesWith = fallSettings.SolidObjectLayerMask,
                                GroupIndex = 0
                            }
                        };

                        if (physicsWorld.CastRay(rayInput, out _))
                        {
                            // Ground is too close - don't start fall ability
                            canStartFall = false;
                        }
                    }

                    if (canStartFall)
                    {
                        // Start falling
                        fall.IsFalling = true;
                        fall.FallStartHeight = position.y;
                        fall.FallDistance = 0f;

                        fallAbility.IsFalling = true;
                        fallAbility.FallStartHeight = position.y;
                        fallAbility.FallDuration = 0f;
                        fallAbility.StateIndex = 0; // 13.14.6: State 0 = falling
                        fallAbility.Landed = false;
                        fallAbility.WaitingForAnimationEvent = false;
                        fallAbility.AnimationEventTimer = 0f;

                        animState.IsFalling = true;
                        animState.FallStateIndex = 0;

                        playerState.MovementState = PlayerMovementState.Falling;
                    }
                }

                // --- Update fall tracking while falling ---
                if (fall.IsFalling)
                {
                    fall.FallDistance = math.max(0f, fall.FallStartHeight - position.y);
                    fallAbility.FallDuration += deltaTime;

                    // --- Landing detection ---
                    if (grounded)
                    {
                        float impactVelocity = velocity.y;

                        // --- 13.14.6: State Index transition to landed ---
                        fallAbility.StateIndex = 1;
                        animState.FallStateIndex = 1;

                        // --- 13.14.2: Spawn surface impact if velocity exceeds threshold ---
                        if (impactVelocity < fallSettings.MinSurfaceImpactVelocity)
                        {
                            // EPIC 16.10: Read SurfaceMaterialId from GroundSurfaceState
                            int landingMaterialId = 0;
                            if (SystemAPI.HasComponent<GroundSurfaceState>(entity))
                            {
                                landingMaterialId = SystemAPI.GetComponent<GroundSurfaceState>(entity).SurfaceMaterialId;
                                if (landingMaterialId < 0) landingMaterialId = 0;
                            }

                            var impactRequest = new SurfaceImpactRequest
                            {
                                ContactPoint = new float3(position.x, playerState.GroundHeight, position.z),
                                ContactNormal = playerState.GroundNormal,
                                ImpactVelocity = impactVelocity,
                                SurfaceMaterialId = landingMaterialId,
                                SurfaceImpactId = fallSettings.LandSurfaceImpactId
                            };

                            ecb.SetComponent(entity, impactRequest);
                            ecb.SetComponentEnabled<SurfaceImpactRequest>(entity, true);
                        }

                        // --- Calculate fall damage ---
                        float damage = 0f;
                        if (_fallDamageSettingsLookup.HasComponent(entity))
                        {
                            var damageSettings = _fallDamageSettingsLookup[entity];
                            if (fall.FallDistance > damageSettings.SafeFallHeight)
                            {
                                damage = (fall.FallDistance - damageSettings.SafeFallHeight) * damageSettings.DamagePerMeter;
                            }

                            // Soft-landing: crouching halves damage
                            if (damage > 0f && playerState.Stance == PlayerStance.Crouching)
                            {
                                damage *= 0.5f;
                            }

                            // EPIC 16.10: Surface fall damage modifier
                            if (damage > 0f && SystemAPI.HasSingleton<SurfaceGameplayConfigSingleton>()
                                && SystemAPI.HasComponent<GroundSurfaceState>(entity))
                            {
                                var groundSurface = SystemAPI.GetComponent<GroundSurfaceState>(entity);
                                ref var blob = ref SystemAPI.GetSingleton<SurfaceGameplayConfigSingleton>()
                                    .Config.Value;
                                int idx = (int)groundSurface.SurfaceId;
                                if (idx >= 0 && idx < blob.Modifiers.Length)
                                    damage *= blob.Modifiers[idx].FallDamageMultiplier;
                            }

                            // Apply damage
                            if (damage > 0f)
                            {
                                if (_applyDamageLookup.HasComponent(entity))
                                {
                                    var existingDamage = _applyDamageLookup[entity];
                                    existingDamage.Amount += damage;
                                    ecb.SetComponent(entity, existingDamage);
                                }
                                else
                                {
                                    ecb.AddComponent(entity, new ApplyDamage { Amount = damage });
                                }

                                // Camera shake
                                var shake = new CameraShake
                                {
                                    Amplitude = damageSettings.ShakeAmplitude * math.clamp(damage / 10f, 0f, 1f),
                                    Frequency = damageSettings.ShakeFrequency,
                                    Decay = damageSettings.ShakeDecay,
                                    Timer = 0f
                                };

                                if (_cameraShakeLookup.HasComponent(entity))
                                {
                                    ecb.SetComponent(entity, shake);
                                }
                                else
                                {
                                    ecb.AddComponent(entity, shake);
                                }
                            }
                        }

                        // --- Emit landing event for adapters ---
                        if (fall.FallDistance > 0.5f)
                        {
                            float landingIntensity = math.clamp(
                                damage > 0f ? damage / 10f : fall.FallDistance / 3f,
                                0f, 1f);

                            var landingEvent = new LandingEvent { Intensity = landingIntensity };
                            if (_landingEventLookup.HasComponent(entity))
                            {
                                ecb.SetComponent(entity, landingEvent);
                            }
                            else
                            {
                                ecb.AddComponent(entity, landingEvent);
                            }
                        }

                        // --- Landing recovery (stun) ---
                        float recovery = 0.5f + math.clamp(damage * 0.1f, 0f, 4.5f);
                        var landingState = new LandingState
                        {
                            IsRecovering = true,
                            RecoveryDuration = recovery,
                            RecoveryTimer = recovery
                        };

                        if (_landingStateLookup.HasComponent(entity))
                        {
                            ecb.SetComponent(entity, landingState);
                        }
                        else
                        {
                            ecb.AddComponent(entity, landingState);
                        }

                        // --- 13.14.3: Wait for animation event or complete immediately ---
                        if (fallSettings.WaitForLandEvent)
                        {
                            fallAbility.WaitingForAnimationEvent = true;
                            fallAbility.AnimationEventTimer = 0f;
                            // Don't set Landed yet - wait for event
                        }
                        else
                        {
                            fallAbility.Landed = true;
                            fallAbility.WaitingForAnimationEvent = false;
                        }

                        // Clear fall state
                        fall.IsFalling = false;
                        fall.FallDistance = 0f;
                        fallAbility.IsFalling = false;
                        animState.IsFalling = false;

                        // Only set to idle if not waiting for animation
                        if (!fallSettings.WaitForLandEvent)
                        {
                            playerState.MovementState = PlayerMovementState.Idle;
                        }
                    }
                }
                else if (!grounded && !fallAbility.WaitingForAnimationEvent)
                {
                    // Went airborne again (e.g., jumped) - reset state
                    fallAbility.StateIndex = 0;
                    animState.FallStateIndex = 0;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
