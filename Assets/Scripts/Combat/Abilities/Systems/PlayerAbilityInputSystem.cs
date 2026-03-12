using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Reads PlayerInput.AbilitySlotRequest and writes to PlayerAbilityState.QueuedSlotIndex.
    /// Runs first in the ability pipeline to capture input before validation.
    ///
    /// EPIC 18.19 - Phase 4
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PlayerAbilitySystemGroup), OrderFirst = true)]
    public partial struct PlayerAbilityInputSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerAbilityState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (abilityState, input) in
                SystemAPI.Query<RefRW<PlayerAbilityState>, RefRO<PlayerInput>>()
                    .WithAll<Simulate>())
            {
                byte slotRequest = input.ValueRO.AbilitySlotRequest;

                // 255 = no request
                if (slotRequest == 255) continue;

                abilityState.ValueRW.QueuedSlotIndex = slotRequest;
            }
        }
    }
}
