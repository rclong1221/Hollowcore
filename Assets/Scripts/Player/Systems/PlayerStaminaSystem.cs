using Unity.Burst;
using Unity.Entities;
using Player.Components;
using Unity.Mathematics;
using Unity.NetCode;

/// <summary>
/// Manages player stamina - drains when sprinting or climbing, regenerates when idle.
/// Forces dismount from climbing when stamina depletes.
/// </summary>
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerMovementSystem))]
[BurstCompile]
public partial struct PlayerStaminaSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var currentTime = (float)SystemAPI.Time.ElapsedTime;
        var isServer = state.WorldUnmanaged.IsServer();

        foreach (var (stamina, playerInput, playerState, climbState, climbSettings) in
                 SystemAPI.Query<RefRW<PlayerStamina>, RefRO<PlayerInput>, RefRO<PlayerState>, 
                                 RefRW<FreeClimbState>, RefRO<FreeClimbSettings>>()
                 .WithAll<Simulate>())
        {
            ref var stam = ref stamina.ValueRW;
            var input = playerInput.ValueRO;
            var pState = playerState.ValueRO;
            ref var climb = ref climbState.ValueRW;
            var climbCfg = climbSettings.ValueRO;

            bool isDraining = false;

            // ========== Sprinting Drain ==========
            bool isSprinting = input.Sprint.IsSet &&
                              pState.Stance == PlayerStance.Standing &&
                              pState.MovementState == PlayerMovementState.Sprinting &&
                              stam.Current > 0;
            
            if (isSprinting)
            {
                stam.Current -= stam.DrainRate * deltaTime;
                stam.Current = math.max(0, stam.Current);
                stam.LastDrainTime = currentTime;
                isDraining = true;
            }

            // ========== Climbing Drain ==========
            if (climb.IsClimbing && !climb.IsTransitioning && climbCfg.StaminaCost > 0)
            {
                stam.Current -= climbCfg.StaminaCost * deltaTime;
                stam.Current = math.max(0, stam.Current);
                stam.LastDrainTime = currentTime;
                isDraining = true;

                // Force dismount when stamina depletes - SERVER ONLY
                if (isServer && stam.Current <= 0)
                {
                    // EPIC 13.20: Track dismount for cooldown
                    climb.LastDismountTime = currentTime;
                    climb.LastClimbedSurface = climb.SurfaceEntity;
                    
                    UnityEngine.Debug.Log($"[CLIMB_ABORT] Stamina Depleted! Current={stam.Current} Max={stam.Max}");
                    climb.IsClimbing = false;
                    climb.SurfaceEntity = Entity.Null;
                    // Note: Velocity will be handled by CharacterControllerSystem (gravity takes over)
                }
            }

            // ========== Regeneration ==========
            if (!isDraining)
            {
                // Use climb recovery delay if was climbing, otherwise normal delay
                float delay = climb.IsClimbing ? climbCfg.ClimbStaminaRecoveryDelay : stam.RegenDelay;
                float timeSinceLastDrain = currentTime - stam.LastDrainTime;
                if (timeSinceLastDrain >= delay)
                {
                    stam.Current += stam.RegenRate * deltaTime;
                    stam.Current = math.min(stam.Max, stam.Current);
                }
            }
        }
        
        // Handle players without climbing components (fallback query)
        foreach (var (stamina, playerInput, playerState) in
                 SystemAPI.Query<RefRW<PlayerStamina>, RefRO<PlayerInput>, RefRO<PlayerState>>()
                 .WithNone<FreeClimbState>()
                 .WithAll<Simulate>())
        {
            ref var stam = ref stamina.ValueRW;
            var input = playerInput.ValueRO;
            var pState = playerState.ValueRO;

            bool isSprinting = input.Sprint.IsSet &&
                              pState.Stance == PlayerStance.Standing &&
                              pState.MovementState == PlayerMovementState.Sprinting &&
                              stam.Current > 0;
            
            if (isSprinting)
            {
                stam.Current -= stam.DrainRate * deltaTime;
                stam.Current = math.max(0, stam.Current);
                stam.LastDrainTime = currentTime;
            }
            else
            {
                float timeSinceLastDrain = currentTime - stam.LastDrainTime;
                if (timeSinceLastDrain >= stam.RegenDelay)
                {
                    stam.Current += stam.RegenRate * deltaTime;
                    stam.Current = math.min(stam.Max, stam.Current);
                }
            }
        }
    }
}
