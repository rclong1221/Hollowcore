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
/// Simple DodgeRollSystem: detects a DodgeRoll input and starts a DodgeRollState.
/// This is intentionally conservative: it only starts the state and tracks elapsed time.
/// Integration with CharacterController/MoveRequests and stamina is left as follow-up.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
public partial class DodgeRollSystem : SystemBase
{

    protected override void OnCreate()
    {
        RequireForUpdate<Player.Components.PlayerInputComponent>();
        RequireForUpdate<NetworkTime>();
    }

        protected override void OnUpdate()
        {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                float dt = SystemAPI.Time.DeltaTime;
                var networkTime = SystemAPI.GetSingleton<NetworkTime>();
                bool isFirstTime = networkTime.IsFirstTimeFullyPredictingTick;

                // Prediction support: mark predicted rolls started on local predicted ghosts so client can play immediately.
                // Define a small predicted marker component (frame-based) and reconcile when authoritative DodgeRollState arrives.

                // Start rolls for networked/predicted players (uses PlayerInput from NetCode)
                foreach (var (inputRO, stateRW, entity) in SystemAPI.Query<RefRO<PlayerInput>, RefRW<PlayerState>>().WithAll<Simulate>().WithEntityAccess())
                {
                    var input = inputRO.ValueRO;
                    if (!input.DodgeRoll.IsSet) continue;

                    // Check if already rolling or on cooldown
                    if (EntityManager.HasComponent<DodgeRollState>(entity))
                    {
                        var existingState = EntityManager.GetComponentData<DodgeRollState>(entity);
                        if (existingState.IsActive == 1 || existingState.CooldownRemaining > 0f)
                            continue;
                        // Prevent processing the same input frame multiple times
                        if (existingState.StartFrame == input.DodgeRoll.FrameCount)
                            continue;
                    }

                    // Don't allow roll while actively diving
                    if (EntityManager.HasComponent<Player.Components.DodgeDiveState>(entity))
                    {
                        var diveState = EntityManager.GetComponentData<Player.Components.DodgeDiveState>(entity);
                        if (diveState.IsActive == 1)
                            continue;
                    }

                    // Check stamina if present
                    if (EntityManager.HasComponent<PlayerStamina>(entity))
                    {
                        var st = EntityManager.GetComponentData<PlayerStamina>(entity);
                        var tuning = DodgeRollComponent.Default;
                        if (st.Current < tuning.StaminaCost)
                        {
                            // insufficient stamina, ignore input
                            continue;
                        }

                        st.Current = math.max(0f, st.Current - tuning.StaminaCost);
                        ecb.SetComponent(entity, st);
                    }

                    // Start a roll with default tuning if no tuning component present
                    DodgeRollComponent tuning2 = DodgeRollComponent.Default;
                    if (EntityManager.HasComponent<DodgeRollComponent>(entity))
                    {
                        tuning2 = EntityManager.GetComponentData<DodgeRollComponent>(entity);
                    }
                    var s = new DodgeRollState
                    {
                        Elapsed = 0f,
                        Duration = tuning2.Duration,
                        DistanceRemaining = tuning2.Distance,
                        InvulnStart = tuning2.InvulnWindowStart,
                        InvulnEnd = tuning2.InvulnWindowEnd,
                        IsActive = 1,
                        CooldownRemaining = 0f
                    };
                    s.StartFrame = input.DodgeRoll.FrameCount;
                    // Use AddComponent to be safe if it's missing, effectively Set if present via AddComponent logic in some contexts? 
                    // No, SetComponent is safer if it exists, AddComponent if not. 
                    // Since we checked HasComponent above (and continued if valid), if we are here it might exist (inactive) or not exist.
                    // To be safe and support both, we check.
                    if (EntityManager.HasComponent<DodgeRollState>(entity))
                        ecb.SetComponent(entity, s);
                    else
                        ecb.AddComponent(entity, s);

                    // Mark prediction so we can reconcile when server snapshot arrives
                    // Robustness: Handle case where PredictedDodgeRoll wasn't baked
                    if (!EntityManager.HasComponent<PredictedDodgeRoll>(entity))
                    {
                        if (isFirstTime)
                        {
                            ecb.AddComponent(entity, new PredictedDodgeRoll { FrameCount = input.DodgeRoll.FrameCount, PredictedStartTime = (float)SystemAPI.Time.ElapsedTime });
                        }
                    }
                    else
                    {
                        ecb.SetComponentEnabled<PredictedDodgeRoll>(entity, true);
                        ecb.SetComponent(entity, new PredictedDodgeRoll { FrameCount = input.DodgeRoll.FrameCount, PredictedStartTime = (float)SystemAPI.Time.ElapsedTime });
                    }

                    // Set movement state to Rolling
                    var ps = stateRW.ValueRW;
                    ps.MovementState = PlayerMovementState.Rolling;
                    ecb.SetComponent(entity, ps);
                }

                // Start rolls for hybrid/local-only players (PlayerInputComponent path)
                foreach (var (inputRO, stateRW, entity) in SystemAPI.Query<RefRO<Player.Components.PlayerInputComponent>, RefRW<PlayerState>>().WithNone<PlayerInput>().WithEntityAccess())
                {
                    var input = inputRO.ValueRO;
                    if (input.DodgeRoll == 0) continue;

                    // Check if already rolling or on cooldown
                    if (EntityManager.HasComponent<DodgeRollState>(entity))
                    {
                        var existingState = EntityManager.GetComponentData<DodgeRollState>(entity);
                        if (existingState.IsActive == 1 || existingState.CooldownRemaining > 0f)
                            continue;
                    }

                    // Don't allow roll while actively diving
                    if (EntityManager.HasComponent<Player.Components.DodgeDiveState>(entity))
                    {
                        var diveState = EntityManager.GetComponentData<Player.Components.DodgeDiveState>(entity);
                        if (diveState.IsActive == 1)
                            continue;
                    }

                    // Check stamina if present
                    if (EntityManager.HasComponent<PlayerStamina>(entity))
                    {
                        var st = EntityManager.GetComponentData<PlayerStamina>(entity);
                        var tuning = DodgeRollComponent.Default;
                        if (st.Current < tuning.StaminaCost)
                        {
                            // insufficient stamina, ignore input
                            continue;
                        }

                        st.Current = math.max(0f, st.Current - tuning.StaminaCost);
                        ecb.SetComponent(entity, st);
                    }

                    // Start a roll with default tuning if no tuning component present
                    DodgeRollComponent tuning2 = DodgeRollComponent.Default;
                    if (EntityManager.HasComponent<DodgeRollComponent>(entity))
                    {
                        tuning2 = EntityManager.GetComponentData<DodgeRollComponent>(entity);
                    }
                    var s = new DodgeRollState
                    {
                        Elapsed = 0f,
                        Duration = tuning2.Duration,
                        DistanceRemaining = tuning2.Distance,
                        InvulnStart = tuning2.InvulnWindowStart,
                        InvulnEnd = tuning2.InvulnWindowEnd,
                        IsActive = 1,
                        CooldownRemaining = 0f
                    };
                    s.StartFrame = 0;
                    
                    if (EntityManager.HasComponent<DodgeRollState>(entity))
                        ecb.SetComponent(entity, s);
                    else
                        ecb.AddComponent(entity, s);

                    // Local/hybrid players are not network-predicted, but we keep behavior consistent
                    var ps = stateRW.ValueRW;
                    ps.MovementState = PlayerMovementState.Rolling;
                    ecb.SetComponent(entity, ps);
                }

                // Update active rolls (advance elapsed and adjust distance)
                foreach (var (rollRW, entity) in SystemAPI.Query<RefRW<DodgeRollState>>().WithEntityAccess())
                {
                    ref var roll = ref rollRW.ValueRW;
                    
                    if (roll.IsActive == 0)
                    {
                        // Update cooldown
                        if (roll.CooldownRemaining > 0f)
                        {
                            roll.CooldownRemaining = math.max(0f, roll.CooldownRemaining - dt);
                        }
                        continue;
                    }
                    
                    roll.Elapsed += dt;
                    // Simple distance decay proportional to elapsed
                    float t = math.clamp(roll.Elapsed / math.max(0.0001f, roll.Duration), 0f, 1f);
                    roll.DistanceRemaining = math.lerp(roll.DistanceRemaining, 0f, t);
                    
                    if (roll.Elapsed >= roll.Duration)
                    {
                        roll.IsActive = 0;
                        
                        // Get tuning for cooldown
                        DodgeRollComponent tuning = DodgeRollComponent.Default;
                        if (EntityManager.HasComponent<DodgeRollComponent>(entity))
                        {
                            tuning = EntityManager.GetComponentData<DodgeRollComponent>(entity);
                        }
                        roll.CooldownRemaining = tuning.Cooldown;
                        
                        // Remove invulnerability if present
                        if (EntityManager.HasComponent<DodgeRollInvuln>(entity) && SystemAPI.IsComponentEnabled<DodgeRollInvuln>(entity))
                        {
                            ecb.SetComponentEnabled<DodgeRollInvuln>(entity, false);
                        }
                        
                        // Restore normal movement state
                        if (EntityManager.HasComponent<PlayerState>(entity))
                        {
                            var ps = EntityManager.GetComponentData<PlayerState>(entity);
                            ps.MovementState = PlayerMovementState.Idle;
                            ecb.SetComponent(entity, ps);
                        }
                        continue;
                    }
                    // Manage Invuln (while active)
                    bool within = roll.IsActive != 0 && roll.Elapsed >= roll.InvulnStart && roll.Elapsed < roll.InvulnEnd;
                    bool hasInvComponent = EntityManager.HasComponent<DodgeRollInvuln>(entity);
                    bool isInvEnabled = hasInvComponent && SystemAPI.IsComponentEnabled<DodgeRollInvuln>(entity);
                    
                    if (within && !isInvEnabled)
                    {
                        if (!hasInvComponent)
                        {
                            if (isFirstTime) ecb.AddComponent<DodgeRollInvuln>(entity); // Default enabled
                        }
                        else
                        {
                            ecb.SetComponentEnabled<DodgeRollInvuln>(entity, true);
                        }
                    }
                    else if (!within && isInvEnabled)
                    {
                        ecb.SetComponentEnabled<DodgeRollInvuln>(entity, false);
                    }
                }

                // Removed separate invuln loop to prevent double-removal errors
                /* 
                Original loop removed. Logic merged above.
                */

                // Reconcile predicted rolls: compare server's DodgeRollState elapsed to client's predicted elapsed
                // Use same ecb
                foreach (var (predRO, rollRW, entity) in SystemAPI.Query<RefRO<PredictedDodgeRoll>, RefRW<DodgeRollState>>().WithAll<DodgeRollState, PredictedDodgeRoll>().WithEntityAccess())
                {
                    var pred = predRO.ValueRO;
                    ref var roll = ref rollRW.ValueRW;
                    
                    // Calculate client's predicted elapsed based on local time when prediction started
                    float clientElapsedNow = (float)SystemAPI.Time.ElapsedTime - pred.PredictedStartTime;
                    float delta = roll.Elapsed - clientElapsedNow; // positive => server ahead
                    const float threshold = 0.05f;
                    const float smoothTime = 0.2f;
                    
                    if (math.abs(delta) > threshold)
                    {
                        // Store server elapsed and enable reconciliation smoothing
                        roll.ServerElapsed = roll.Elapsed;
                        roll.IsReconciling = 1;
                        roll.ReconcileSmoothing = dt / smoothTime; // Smooth factor based on frame time
                    }

                    // Remove prediction marker
                    if (EntityManager.HasComponent<PredictedDodgeRoll>(entity))
                        ecb.SetComponentEnabled<PredictedDodgeRoll>(entity, false);
                }
                
                // Update reconciliation progress and disable when complete
                foreach (var rollRW in SystemAPI.Query<RefRW<DodgeRollState>>())
                {
                    ref var roll = ref rollRW.ValueRW;
                    if (roll.IsReconciling == 1)
                    {
                        // Check if reconciliation is complete (elapsed caught up to server)
                        float diff = math.abs(roll.Elapsed - roll.ServerElapsed);
                        if (diff < 0.01f)
                        {
                            roll.IsReconciling = 0;
                            roll.ReconcileSmoothing = 0f;
                        }
                    }
                }

                ecb.Playback(EntityManager);
                ecb.Dispose();
        }
}

    /// <summary>
    /// Marker component added when a client predicts a dodge roll locally so we can reconcile
    /// when the server authoritative state arrives.
    /// </summary>
    public struct PredictedDodgeRoll : IComponentData, IEnableableComponent
    {
        public uint FrameCount;
        public float PredictedStartTime;
        // Optionally keep other debug fields here
    }

}
