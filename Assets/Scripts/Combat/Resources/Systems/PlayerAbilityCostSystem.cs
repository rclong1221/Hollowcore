using Unity.Burst;
using Unity.Entities;
using DIG.Player.Abilities;
using DIG.Player.Systems.Abilities;

namespace DIG.Combat.Resources.Systems
{
    /// <summary>
    /// EPIC 16.8 Phase 2: Validates and deducts resource costs for player abilities.
    /// Runs after AbilityPrioritySystem (which sets PendingAbilityIndex)
    /// and before AbilityLifecycleSystem (which transitions Pending → Active).
    /// If the pending ability has a resource cost and the player can't afford it,
    /// clears PendingAbilityIndex to block the cast.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateAfter(typeof(AbilityPrioritySystem))]
    [UpdateBefore(typeof(AbilityLifecycleSystem))]
    public partial struct PlayerAbilityCostSystem : ISystem
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

            foreach (var (abilityState, abilities, pool) in
                SystemAPI.Query<RefRW<AbilityState>,
                                DynamicBuffer<AbilityDefinition>,
                                RefRW<ResourcePool>>()
                .WithAll<AbilitySystemTag, Simulate>())
            {
                int pending = abilityState.ValueRO.PendingAbilityIndex;
                if (pending < 0 || pending >= abilities.Length) continue;

                var ability = abilities[pending];
                if (ability.ResourceCostType == ResourceType.None) continue;

                if (!pool.ValueRO.HasResource(ability.ResourceCostType, ability.ResourceCostAmount))
                {
                    // Insufficient resource — block the ability
                    abilityState.ValueRW.PendingAbilityIndex = -1;
                }
                else
                {
                    // Deduct resource on cast start
                    pool.ValueRW.TryDeduct(ability.ResourceCostType, ability.ResourceCostAmount, currentTime);
                }
            }
        }
    }
}
