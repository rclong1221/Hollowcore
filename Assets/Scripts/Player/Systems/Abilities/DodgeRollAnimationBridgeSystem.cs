using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using Player.Animation;
using Unity.Mathematics;

namespace Player.Systems.Abilities
{
    /// <summary>
    /// Bridges the existing DodgeRollState (gameplay) to RollState (Opsive animation).
    /// This ensures the Opsive Roll animation plays when DodgeRollState is active.
    ///
    /// The existing DodgeRollSystem handles:
    /// - Input detection
    /// - Stamina cost
    /// - Invulnerability windows
    /// - Cooldowns
    /// - Movement state changes
    ///
    /// This bridge simply sets RollState.IsRolling = true when DodgeRollState.IsActive == 1,
    /// allowing PlayerAnimationStateSystem to set the correct Opsive animation parameters.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(DodgeRollSystem))]
    public partial struct DodgeRollAnimationBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Sync DodgeRollState -> RollState for animation
            // Write directly (no ECB) so PlayerAnimationStateSystem sees change this frame
            foreach (var (dodgeRoll, input, rollState) in
                SystemAPI.Query<RefRO<DodgeRollState>, RefRO<PlayerInput>, RefRW<RollState>>()
                    .WithAll<Simulate>())
            {
                var dr = dodgeRoll.ValueRO;
                var pi = input.ValueRO;
                ref var rs = ref rollState.ValueRW;

                bool isRolling = dr.IsActive == 1;

                // Sync rolling state
                if (rs.IsRolling != isRolling)
                {
                    rs.IsRolling = isRolling;
                    if (!isRolling)
                    {
                         rs.TimeRemaining = 0f;
                    }
                }

                if (isRolling)
                {
                    // Update direction continuously to handle input timing nuances
                    // Priority: Left/Right > Forward/Back
                    if (math.abs(pi.Horizontal) > 0.1f)
                    {
                        rs.RollType = pi.Horizontal > 0 
                            ? OpsiveAnimatorConstants.ROLL_RIGHT 
                            : OpsiveAnimatorConstants.ROLL_LEFT;
                    }
                    else
                    {
                        // Default to forward
                        rs.RollType = OpsiveAnimatorConstants.ROLL_FORWARD;
                    }
                    
                    rs.TimeRemaining = dr.Duration;
                }
            }
        }
    }

    /// <summary>
    /// Bridges DodgeDiveState to DodgeState for animation.
    /// The existing DodgeDiveSystem handles the dive gameplay; this syncs to Opsive animation.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(DodgeDiveSystem))]
    public partial struct DodgeDiveAnimationBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Sync DodgeDiveState -> DodgeState for animation
            // Write directly (no ECB) so PlayerAnimationStateSystem sees change this frame
            foreach (var (dodgeDive, input, dodgeState) in
                SystemAPI.Query<RefRO<DodgeDiveState>, RefRO<PlayerInput>, RefRW<DodgeState>>()
                    .WithAll<Simulate>())
            {
                var dd = dodgeDive.ValueRO;
                var pi = input.ValueRO;
                ref var ds = ref dodgeState.ValueRW;

                bool isDodging = dd.IsActive == 1;

                // Sync dodging state
                if (ds.IsDodging != isDodging)
                {
                    ds.IsDodging = isDodging;
                    if (!isDodging)
                    {
                        ds.TimeRemaining = 0f;
                    }
                }

                if (isDodging)
                {
                    // Update direction continuously
                    if (math.abs(pi.Horizontal) > math.abs(pi.Vertical) && math.abs(pi.Horizontal) > 0.1f)
                    {
                        ds.Direction = pi.Horizontal > 0 
                            ? OpsiveAnimatorConstants.DODGE_RIGHT 
                            : OpsiveAnimatorConstants.DODGE_LEFT;
                    }
                    else
                    {
                        // If Vertical is dominant
                        if (pi.Vertical < -0.1f)
                        {
                            ds.Direction = OpsiveAnimatorConstants.DODGE_BACKWARD;
                        }
                        else
                        {
                            ds.Direction = OpsiveAnimatorConstants.DODGE_FORWARD;
                        }
                    }
                    
                    ds.TimeRemaining = dd.Duration;
                }
            }
        }
    }

    /// <summary>
    /// Bridges ProneState to CrawlState for animation.
    /// When player is prone AND moving, triggers crawl animation.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial struct ProneCrawlAnimationBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Sync Prone + Movement -> CrawlState for animation
            foreach (var (playerState, playerInput, crawlState) in
                SystemAPI.Query<RefRO<PlayerState>, RefRO<PlayerInput>, RefRW<CrawlState>>()
                    .WithAll<Simulate>())
            {
                var pState = playerState.ValueRO;
                var input = playerInput.ValueRO;
                ref var cs = ref crawlState.ValueRW;

                // Crawl when prone AND has movement input
                bool isProne = pState.Stance == PlayerStance.Prone;
                bool hasMovement = input.Horizontal != 0 || input.Vertical != 0;
                bool shouldCrawl = isProne && hasMovement;

                if (cs.IsCrawling != shouldCrawl)
                {
                    cs.IsCrawling = shouldCrawl;
                    cs.CrawlSubState = shouldCrawl
                        ? OpsiveAnimatorConstants.CRAWL_ACTIVE
                        : OpsiveAnimatorConstants.CRAWL_STOPPING;
                }
            }
        }
    }
}
