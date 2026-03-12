using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Ticks cooldowns, GCD, and charge regeneration each predicted tick.
    ///
    /// EPIC 18.19 - Phase 4
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
    [UpdateAfter(typeof(PlayerAbilityInputSystem))]
    public partial struct PlayerAbilityCooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerAbilityState>();
            state.RequireForUpdate<AbilityDatabaseRef>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var dbRef = SystemAPI.GetSingleton<AbilityDatabaseRef>();
            if (!dbRef.Value.IsCreated) return;

            ref var abilities = ref dbRef.Value.Value.Abilities;

            foreach (var (abilityState, entity) in
                SystemAPI.Query<RefRW<PlayerAbilityState>>()
                    .WithAll<Simulate, PlayerAbilitySlot>()
                    .WithEntityAccess())
            {
                // Tick GCD
                if (abilityState.ValueRO.GCDRemaining > 0f)
                {
                    abilityState.ValueRW.GCDRemaining -= dt;
                    if (abilityState.ValueRO.GCDRemaining < 0f)
                        abilityState.ValueRW.GCDRemaining = 0f;
                }

                // Get buffer via lookup (writable local, not foreach iteration variable)
                var slots = SystemAPI.GetBuffer<PlayerAbilitySlot>(entity);

                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.AbilityId < 0) continue;

                    bool modified = false;

                    if (slot.CooldownRemaining > 0f)
                    {
                        slot.CooldownRemaining -= dt;
                        if (slot.CooldownRemaining < 0f)
                            slot.CooldownRemaining = 0f;
                        modified = true;
                    }

                    int abilityId = slot.AbilityId;
                    if (abilityId >= 0 && abilityId < abilities.Length)
                    {
                        ref var def = ref abilities[abilityId];
                        if (def.MaxCharges > 0 && slot.ChargesRemaining < def.MaxCharges)
                        {
                            slot.ChargeRechargeElapsed += dt;
                            if (slot.ChargeRechargeElapsed >= def.ChargeRegenTime && def.ChargeRegenTime > 0f)
                            {
                                slot.ChargesRemaining++;
                                slot.ChargeRechargeElapsed -= def.ChargeRegenTime;
                            }
                            modified = true;
                        }
                    }

                    if (modified)
                        slots[i] = slot;
                }
            }
        }
    }
}
