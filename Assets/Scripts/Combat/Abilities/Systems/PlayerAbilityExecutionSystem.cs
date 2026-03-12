using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// 5-phase state machine for player ability lifecycle:
    /// Idle -> Telegraph -> Casting -> Active -> Recovery -> Idle
    ///
    /// Ticks phase elapsed time and transitions between phases based on
    /// ability definition timing. Sets movement gating flags per phase.
    ///
    /// EPIC 18.19 - Phase 4
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
    [UpdateAfter(typeof(PlayerAbilityValidationSystem))]
    public partial struct PlayerAbilityExecutionSystem : ISystem
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
                if (abilityState.ValueRO.IsIdle) continue;

                // Get buffer via lookup (writable local, not foreach iteration variable)
                var slots = SystemAPI.GetBuffer<PlayerAbilitySlot>(entity);

                byte activeSlot = abilityState.ValueRO.ActiveSlotIndex;
                if (activeSlot == 255 || activeSlot >= slots.Length) continue;

                var slot = slots[activeSlot];
                if (slot.AbilityId < 0 || slot.AbilityId >= abilities.Length)
                {
                    ResetToIdle(ref abilityState.ValueRW);
                    continue;
                }

                ref var def = ref abilities[slot.AbilityId];

                abilityState.ValueRW.PhaseElapsed += dt;
                float elapsed = abilityState.ValueRO.PhaseElapsed;

                switch (abilityState.ValueRO.Phase)
                {
                    case AbilityCastPhase.Telegraph:
                        if (elapsed >= def.TelegraphDuration)
                        {
                            if (def.CastTime > 0f)
                                TransitionPhase(ref abilityState.ValueRW, AbilityCastPhase.Casting);
                            else
                                TransitionPhase(ref abilityState.ValueRW, AbilityCastPhase.Active);
                        }
                        break;

                    case AbilityCastPhase.Casting:
                        if (elapsed >= def.CastTime)
                        {
                            TransitionPhase(ref abilityState.ValueRW, AbilityCastPhase.Active);
                        }
                        break;

                    case AbilityCastPhase.Active:
                        if (elapsed >= def.ActiveDuration)
                        {
                            if (def.RecoveryTime > 0f)
                            {
                                TransitionPhase(ref abilityState.ValueRW, AbilityCastPhase.Recovery);
                            }
                            else
                            {
                                CompleteAbility(ref abilityState.ValueRW, slots, activeSlot, ref def);
                            }
                        }
                        break;

                    case AbilityCastPhase.Recovery:
                        if (elapsed >= def.RecoveryTime)
                        {
                            CompleteAbility(ref abilityState.ValueRW, slots, activeSlot, ref def);
                        }
                        break;
                }
            }
        }

        private static void TransitionPhase(ref PlayerAbilityState abilityState, AbilityCastPhase newPhase)
        {
            abilityState.Phase = newPhase;
            abilityState.PhaseElapsed = 0f;
        }

        private static void CompleteAbility(ref PlayerAbilityState abilityState,
            DynamicBuffer<PlayerAbilitySlot> slots, byte slotIndex, ref AbilityDef def)
        {
            var slot = slots[slotIndex];
            if (def.MaxCharges > 0 && slot.ChargesRemaining > 0)
            {
                slot.ChargesRemaining--;
                if (slot.ChargeRechargeElapsed <= 0f)
                    slot.ChargeRechargeElapsed = 0f;
            }

            slot.CooldownRemaining = def.Cooldown;
            slots[slotIndex] = slot;

            ResetToIdle(ref abilityState);
        }

        private static void ResetToIdle(ref PlayerAbilityState abilityState)
        {
            abilityState.ActiveSlotIndex = 255;
            abilityState.Phase = AbilityCastPhase.Idle;
            abilityState.PhaseElapsed = 0f;
            abilityState.ActiveFlags = AbilityStateFlags.None;
            abilityState.DamageDealt = 0;
            abilityState.TicksDelivered = 0;
        }
    }
}
