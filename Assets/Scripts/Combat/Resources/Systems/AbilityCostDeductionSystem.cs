using Unity.Entities;
using Unity.Burst;
using DIG.AI.Components;
using DIG.AI.Systems;

namespace DIG.Combat.Resources.Systems
{
    /// <summary>
    /// EPIC 16.8 Phase 2: Deducts resources at the appropriate timing during AI ability execution.
    /// OnCast: when entering Casting phase. PerTick: each TickInterval during Active.
    /// OnComplete: when entering Recovery. OnHit: when DamageDealt is set.
    /// Interrupts ability if resource runs out mid-cast (PerTick).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AbilitySelectionSystem))]
    [UpdateBefore(typeof(AbilityExecutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AbilityCostDeductionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourcePool>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (execState, pool, entity) in
                SystemAPI.Query<RefRW<AbilityExecutionState>, RefRW<ResourcePool>>()
                .WithEntityAccess())
            {
                int abilityIdx = execState.ValueRO.SelectedAbilityIndex;
                if (abilityIdx < 0) continue;

                if (!SystemAPI.HasBuffer<AbilityDefinition>(entity)) continue;
                var abilities = SystemAPI.GetBuffer<AbilityDefinition>(entity);
                if (abilityIdx >= abilities.Length) continue;

                var ability = abilities[abilityIdx];
                if (ability.ResourceCostType == ResourceType.None) continue;

                var phase = execState.ValueRO.Phase;

                switch (ability.ResourceCostTiming)
                {
                    case CostTiming.OnCast:
                        // Deduct when first entering Casting phase (PhaseTimer near 0)
                        if (phase == AbilityCastPhase.Casting && execState.ValueRO.PhaseTimer < deltaTime * 1.5f)
                        {
                            pool.ValueRW.TryDeduct(ability.ResourceCostType, ability.ResourceCostAmount, currentTime);
                        }
                        break;

                    case CostTiming.PerTick:
                        if (phase == AbilityCastPhase.Active && ability.TickInterval > 0f)
                        {
                            // Deduct each tick — check if new tick occurred this frame
                            int expectedTicks = (int)(execState.ValueRO.PhaseTimer / ability.TickInterval);
                            if (expectedTicks > execState.ValueRO.TicksDelivered)
                            {
                                if (!pool.ValueRW.TryDeduct(ability.ResourceCostType, ability.ResourceCostAmount, currentTime))
                                {
                                    // Resource depleted — interrupt to Recovery
                                    execState.ValueRW.Phase = AbilityCastPhase.Recovery;
                                    execState.ValueRW.PhaseTimer = 0f;
                                }
                            }
                        }
                        break;

                    case CostTiming.OnComplete:
                        if (phase == AbilityCastPhase.Recovery && execState.ValueRO.PhaseTimer < deltaTime * 1.5f)
                        {
                            pool.ValueRW.TryDeduct(ability.ResourceCostType, ability.ResourceCostAmount, currentTime);
                        }
                        break;

                    case CostTiming.OnHit:
                        if (execState.ValueRO.DamageDealt)
                        {
                            pool.ValueRW.TryDeduct(ability.ResourceCostType, ability.ResourceCostAmount, currentTime);
                        }
                        break;
                }
            }
        }
    }
}
