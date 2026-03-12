using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

/// <summary>
/// Server-side system that syncs authoritative PlayerState values into PlayerAnimationState.
/// This ensures animation flags (IsCrouching, IsProne, etc.) are properly replicated to clients.
/// Runs after CrouchSystem and SprintSystem so it captures the latest state.
/// </summary>
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(DIG.Player.Systems.Abilities.AbilitySystemGroup))]
public partial struct PlayerAnimationStateSyncSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (animState, playerState, entity) in 
                 SystemAPI.Query<RefRW<PlayerAnimationState>, RefRO<PlayerState>>()
                 .WithEntityAccess())
        {
            animState.ValueRW.IsCrouching = playerState.ValueRO.Stance == PlayerStance.Crouching;
            animState.ValueRW.IsProne = playerState.ValueRO.Stance == PlayerStance.Prone;
            animState.ValueRW.IsSprinting = playerState.ValueRO.MovementState == PlayerMovementState.Sprinting;
            animState.ValueRW.IsGrounded = playerState.ValueRO.IsGrounded;
        }
    }
}
