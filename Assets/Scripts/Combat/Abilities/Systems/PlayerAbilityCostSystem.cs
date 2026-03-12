using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Resources;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Deducts resource costs at the correct timing (OnCast, PerTick, OnComplete, OnHit).
    /// Runs after execution so phase transitions are known.
    ///
    /// EPIC 18.19 - Phase 4
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
    [UpdateAfter(typeof(PlayerAbilityExecutionSystem))]
    public partial struct PlayerAbilityCostSystem : ISystem
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
            float time = (float)SystemAPI.Time.ElapsedTime;
            float dt = SystemAPI.Time.DeltaTime;
            var dbRef = SystemAPI.GetSingleton<AbilityDatabaseRef>();
            if (!dbRef.Value.IsCreated) return;

            ref var abilities = ref dbRef.Value.Value.Abilities;

            foreach (var (abilityState, slots, resourcePool) in
                SystemAPI.Query<RefRO<PlayerAbilityState>, DynamicBuffer<PlayerAbilitySlot>,
                    RefRW<ResourcePool>>()
                    .WithAll<Simulate>())
            {
                if (abilityState.ValueRO.IsIdle) continue;

                byte activeSlot = abilityState.ValueRO.ActiveSlotIndex;
                if (activeSlot == 255 || activeSlot >= slots.Length) continue;

                var slot = slots[activeSlot];
                if (slot.AbilityId < 0 || slot.AbilityId >= abilities.Length) continue;

                ref var def = ref abilities[slot.AbilityId];
                if (def.CostResource == ResourceType.None) continue;

                switch (def.CostTiming)
                {
                    case CostTiming.OnCast:
                        // Deduct at the start of Active phase (first frame only)
                        if (abilityState.ValueRO.Phase == AbilityCastPhase.Active &&
                            abilityState.ValueRO.PhaseElapsed <= dt * 1.5f)
                        {
                            resourcePool.ValueRW.TryDeduct(def.CostResource, def.CostAmount, time);
                        }
                        break;

                    case CostTiming.PerTick:
                        // Deduct each tick during Active phase
                        if (abilityState.ValueRO.Phase == AbilityCastPhase.Active && def.TickInterval > 0f)
                        {
                            // Check if a new tick just occurred based on ticks delivered
                            int expectedTicks = (int)(abilityState.ValueRO.PhaseElapsed / def.TickInterval);
                            if (expectedTicks > abilityState.ValueRO.TicksDelivered)
                            {
                                resourcePool.ValueRW.TryDeduct(def.CostResource, def.CostAmount, time);
                            }
                        }
                        break;

                    case CostTiming.OnComplete:
                        // Deduct when transitioning to Recovery or Idle from Active
                        if (abilityState.ValueRO.Phase == AbilityCastPhase.Recovery &&
                            abilityState.ValueRO.PhaseElapsed <= dt * 1.5f)
                        {
                            resourcePool.ValueRW.TryDeduct(def.CostResource, def.CostAmount, time);
                        }
                        break;

                    case CostTiming.OnHit:
                        // Handled by PlayerAbilityEffectSystem when damage lands
                        break;
                }
            }
        }
    }
}
