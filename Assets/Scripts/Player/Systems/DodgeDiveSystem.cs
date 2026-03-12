using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// DodgeDiveSystem: detects DodgeDive input and starts a DodgeDiveState.
    /// When the dive completes, transitions player to prone state.
    /// Similar to DodgeRollSystem but with prone transition at the end.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(DodgeRollSystem))] // Process dive before roll
    public partial class DodgeDiveSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<Player.Components.PlayerInputComponent>();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;

            // Process networked/predicted players (uses PlayerInput from NetCode)
            foreach (var (inputRO, stateRW, entity) in SystemAPI.Query<RefRO<PlayerInput>, RefRW<PlayerState>>()
                .WithAll<Simulate>()
                .WithEntityAccess())
            {
                var input = inputRO.ValueRO;
                if (!input.DodgeDive.IsSet) continue;
                
                UnityEngine.Debug.Log($"[DodgeDiveSystem] DodgeDive input detected for entity {entity.Index}");

                // Don't allow dive while actively rolling
                if (EntityManager.HasComponent<DodgeRollState>(entity))
                {
                    var rollState = EntityManager.GetComponentData<DodgeRollState>(entity);
                    if (rollState.IsActive == 1)
                        continue;
                }

                // Check if already diving or on cooldown
                if (EntityManager.HasComponent<DodgeDiveState>(entity))
                {
                    var existingState = EntityManager.GetComponentData<DodgeDiveState>(entity);
                    if (existingState.IsActive == 1 || existingState.CooldownRemaining > 0f)
                        continue;
                    // Prevent processing the same input frame multiple times (hybrid uses frameCount 0)
                    // For hybrid, we rely on IsActive check primarily
                }

                // Check stamina if present
                DodgeDiveComponent tuning = DodgeDiveComponent.Default;
                if (EntityManager.HasComponent<DodgeDiveComponent>(entity))
                {
                    tuning = EntityManager.GetComponentData<DodgeDiveComponent>(entity);
                }

                if (EntityManager.HasComponent<PlayerStamina>(entity))
                {
                    var st = EntityManager.GetComponentData<PlayerStamina>(entity);
                    if (st.Current < tuning.StaminaCost)
                        continue;

                    st.Current = math.max(0f, st.Current - tuning.StaminaCost);
                    ecb.SetComponent(entity, st);
                }

                // Start dive
                var diveState = new DodgeDiveState
                {
                    Elapsed = 0f,
                    Duration = tuning.Duration,
                    DistanceRemaining = tuning.Distance,
                    InvulnStart = tuning.InvulnWindowStart,
                    InvulnEnd = tuning.InvulnWindowEnd,
                    IsActive = 1,
                    StartFrame = input.DodgeDive.FrameCount,
                    CooldownRemaining = 0f,
                    WillEndInProne = tuning.EndInProne
                };
                ecb.SetComponent(entity, diveState);

                // Mark for prediction reconciliation
                if (!EntityManager.HasComponent<PredictedDodgeDive>(entity))
                    ecb.AddComponent(entity, new PredictedDodgeDive 
                    { 
                        FrameCount = input.DodgeDive.FrameCount, 
                        PredictedStartTime = (float)SystemAPI.Time.ElapsedTime 
                    });
                else
                    ecb.SetComponent(entity, new PredictedDodgeDive 
                    { 
                        FrameCount = input.DodgeDive.FrameCount, 
                        PredictedStartTime = (float)SystemAPI.Time.ElapsedTime 
                    });

                // Set movement state
                var ps = stateRW.ValueRW;
                ps.MovementState = PlayerMovementState.Diving;
                ecb.SetComponent(entity, ps);

                UnityEngine.Debug.Log($"[DodgeDiveSystem] Started dive for entity {entity.Index}, frame={input.DodgeDive.FrameCount}");
            }

            // Process hybrid/local-only players (PlayerInputComponent path)
            foreach (var (inputRO, stateRW, entity) in SystemAPI.Query<RefRO<Player.Components.PlayerInputComponent>, RefRW<PlayerState>>()
                .WithNone<PlayerInput>()
                .WithEntityAccess())
            {
                var input = inputRO.ValueRO;
                if (input.DodgeDive == 0) continue;

                // Don't allow dive while actively rolling
                if (EntityManager.HasComponent<DodgeRollState>(entity))
                {
                    var rollState = EntityManager.GetComponentData<DodgeRollState>(entity);
                    if (rollState.IsActive == 1)
                        continue;
                }

                // Check if already diving or on cooldown
                if (EntityManager.HasComponent<DodgeDiveState>(entity))
                {
                    var existingState = EntityManager.GetComponentData<DodgeDiveState>(entity);
                    if (existingState.IsActive == 1 || existingState.CooldownRemaining > 0f)
                        continue;
                }

                // Check stamina
                DodgeDiveComponent tuning = DodgeDiveComponent.Default;
                if (EntityManager.HasComponent<DodgeDiveComponent>(entity))
                {
                    tuning = EntityManager.GetComponentData<DodgeDiveComponent>(entity);
                }

                if (EntityManager.HasComponent<PlayerStamina>(entity))
                {
                    var st = EntityManager.GetComponentData<PlayerStamina>(entity);
                    if (st.Current < tuning.StaminaCost)
                        continue;

                    st.Current = math.max(0f, st.Current - tuning.StaminaCost);
                    ecb.SetComponent(entity, st);
                }

                // Start dive
                var diveState = new DodgeDiveState
                {
                    Elapsed = 0f,
                    Duration = tuning.Duration,
                    DistanceRemaining = tuning.Distance,
                    InvulnStart = tuning.InvulnWindowStart,
                    InvulnEnd = tuning.InvulnWindowEnd,
                    IsActive = 1,
                    StartFrame = 0,
                    CooldownRemaining = 0f,
                    WillEndInProne = tuning.EndInProne
                };
                ecb.SetComponent(entity, diveState);

                // Set movement state
                var ps = stateRW.ValueRW;
                ps.MovementState = PlayerMovementState.Diving;
                ecb.SetComponent(entity, ps);

                UnityEngine.Debug.Log($"[DodgeDiveSystem] Started dive for hybrid entity {entity.Index}");
            }

            // Update active dives
            foreach (var (diveRW, entity) in SystemAPI.Query<RefRW<DodgeDiveState>>().WithEntityAccess())
            {
                ref var dive = ref diveRW.ValueRW;
                
                if (dive.IsActive == 0)
                {
                    // Update cooldown
                    if (dive.CooldownRemaining > 0f)
                    {
                        dive.CooldownRemaining = math.max(0f, dive.CooldownRemaining - dt);
                    }
                    continue;
                }

                dive.Elapsed += dt;

                // Check if dive completed
                if (dive.Elapsed >= dive.Duration)
                {
                    dive.IsActive = 0;
                    
                    // Get tuning for cooldown
                    DodgeDiveComponent tuning = DodgeDiveComponent.Default;
                    if (EntityManager.HasComponent<DodgeDiveComponent>(entity))
                    {
                        tuning = EntityManager.GetComponentData<DodgeDiveComponent>(entity);
                    }
                    dive.CooldownRemaining = tuning.Cooldown;

                    // Disable invulnerability if present (do not remove, as it is now enableable)
                    if (EntityManager.HasComponent<DodgeRollInvuln>(entity) && SystemAPI.IsComponentEnabled<DodgeRollInvuln>(entity))
                    {
                        ecb.SetComponentEnabled<DodgeRollInvuln>(entity, false);
                    }

                    // Transition to prone if configured
                    if (dive.WillEndInProne == 1)
                    {
                        // Add or update prone state
                        if (EntityManager.HasComponent<ProneStateComponent>(entity))
                        {
                            var prone = EntityManager.GetComponentData<ProneStateComponent>(entity);
                            prone.IsProne = 1;
                            prone.IsCrawling = 0;
                            ecb.SetComponent(entity, prone);
                        }
                        else
                        {
                            ecb.AddComponent(entity, new ProneStateComponent
                            {
                                IsProne = 1,
                                IsCrawling = 0,
                                TransitionTimer = 0f,
                                TransitionDuration = 0.3f,
                                SpeedMultiplier = 0.5f
                            });
                        }

                        // Update player stance
                        if (EntityManager.HasComponent<PlayerState>(entity))
                        {
                            var ps = EntityManager.GetComponentData<PlayerState>(entity);
                            ps.Stance = PlayerStance.Prone;
                            ps.LastStanceChangeTime = (float)SystemAPI.Time.ElapsedTime;
                            
                            // Set target height
                            if (SystemAPI.HasComponent<PlayerStanceConfig>(entity))
                            {
                                var cfg = SystemAPI.GetComponent<PlayerStanceConfig>(entity);
                                ps.TargetHeight = cfg.ProneHeight;
                            }
                            else
                            {
                                ps.TargetHeight = PlayerStanceConfig.Default.ProneHeight;
                            }
                            
                            ps.MovementState = PlayerMovementState.Idle;
                            ecb.SetComponent(entity, ps);
                        }

                        UnityEngine.Debug.Log($"[DodgeDiveSystem] Dive completed for entity {entity.Index}, transitioning to prone");
                    }
                    else
                    {
                        // Just restore normal movement state
                        if (EntityManager.HasComponent<PlayerState>(entity))
                        {
                            var ps = EntityManager.GetComponentData<PlayerState>(entity);
                            ps.MovementState = PlayerMovementState.Idle;
                            ecb.SetComponent(entity, ps);
                        }
                    }

                    continue;
                }

                // Manage invulnerability window
                bool inInvulnWindow = dive.Elapsed >= dive.InvulnStart && dive.Elapsed <= dive.InvulnEnd;
                bool hasInvulnTag = EntityManager.HasComponent<DodgeRollInvuln>(entity) && SystemAPI.IsComponentEnabled<DodgeRollInvuln>(entity);

                if (inInvulnWindow && !hasInvulnTag)
                {
                    if (EntityManager.HasComponent<DodgeRollInvuln>(entity))
                        ecb.SetComponentEnabled<DodgeRollInvuln>(entity, true);
                    else
                        ecb.AddComponent<DodgeRollInvuln>(entity); // Structural, but Dive uses immediate ECB so it's less risky than predicted rollback loops
                }
                else if (!inInvulnWindow && hasInvulnTag)
                {
                    ecb.SetComponentEnabled<DodgeRollInvuln>(entity, false);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
