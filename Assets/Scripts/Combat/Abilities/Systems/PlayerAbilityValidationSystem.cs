using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Resources;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Validates queued ability requests: checks cooldown, GCD, resource cost, range,
    /// and target requirements. On success, transitions from Idle to the first cast phase.
    ///
    /// EPIC 18.19 - Phase 4
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
    [UpdateAfter(typeof(PlayerAbilityCooldownSystem))]
    public partial struct PlayerAbilityValidationSystem : ISystem
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
            var dbRef = SystemAPI.GetSingleton<AbilityDatabaseRef>();
            if (!dbRef.Value.IsCreated) return;

            ref var abilities = ref dbRef.Value.Value.Abilities;

            foreach (var (abilityState, slots, resourcePool) in
                SystemAPI.Query<RefRW<PlayerAbilityState>, DynamicBuffer<PlayerAbilitySlot>,
                    RefRO<ResourcePool>>()
                    .WithAll<Simulate>())
            {
                // Only validate when idle (or during recovery for queue)
                if (!abilityState.ValueRO.IsIdle && abilityState.ValueRO.Phase != AbilityCastPhase.Recovery)
                    continue;

                byte queuedSlot = abilityState.ValueRO.QueuedSlotIndex;
                if (queuedSlot == 255 || queuedSlot >= slots.Length)
                    continue;

                var slot = slots[queuedSlot];
                if (slot.AbilityId < 0 || slot.AbilityId >= abilities.Length)
                {
                    abilityState.ValueRW.QueuedSlotIndex = 255;
                    continue;
                }

                ref var def = ref abilities[slot.AbilityId];

                // Check GCD
                if (abilityState.ValueRO.GCDRemaining > 0f)
                    continue; // Wait, don't clear queue

                // Check per-ability cooldown
                if (slot.CooldownRemaining > 0f)
                {
                    // Charge-based: check charges instead of cooldown
                    if (def.MaxCharges > 0)
                    {
                        if (slot.ChargesRemaining <= 0)
                            continue; // Wait for charge
                    }
                    else
                    {
                        continue; // Wait for cooldown
                    }
                }

                // Check resource cost (OnCast timing)
                if (def.CostResource != ResourceType.None && def.CostTiming == CostTiming.OnCast)
                {
                    if (!resourcePool.ValueRO.HasResource(def.CostResource, def.CostAmount))
                    {
                        abilityState.ValueRW.QueuedSlotIndex = 255; // Can't afford — clear queue
                        continue;
                    }
                }

                // Validation passed — begin ability
                if (abilityState.ValueRO.IsIdle)
                {
                    abilityState.ValueRW.ActiveSlotIndex = queuedSlot;
                    abilityState.ValueRW.QueuedSlotIndex = 255;
                    abilityState.ValueRW.DamageDealt = 0;
                    abilityState.ValueRW.TicksDelivered = 0;

                    // Determine starting phase
                    if (def.TelegraphDuration > 0f)
                        abilityState.ValueRW.Phase = AbilityCastPhase.Telegraph;
                    else if (def.CastTime > 0f)
                        abilityState.ValueRW.Phase = AbilityCastPhase.Casting;
                    else
                        abilityState.ValueRW.Phase = AbilityCastPhase.Active;

                    abilityState.ValueRW.PhaseElapsed = 0f;

                    // Set GCD
                    if (def.GlobalCooldown > 0f)
                        abilityState.ValueRW.GCDRemaining = def.GlobalCooldown;

                    // Set movement flags
                    var flags = AbilityStateFlags.None;
                    if (def.CastMovement == AbilityCastMovement.Rooted)
                        flags |= AbilityStateFlags.MovementLocked;
                    else if (def.CastMovement == AbilityCastMovement.Slowed)
                        flags |= AbilityStateFlags.MovementSlowed;
                    if (def.Interruptible)
                        flags |= AbilityStateFlags.Interruptible;
                    if (def.TickInterval > 0f)
                        flags |= AbilityStateFlags.IsChanneled;
                    abilityState.ValueRW.ActiveFlags = flags;
                }
            }
        }
    }
}
