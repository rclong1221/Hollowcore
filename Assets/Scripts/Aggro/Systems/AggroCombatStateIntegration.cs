using Unity.Burst;
using Unity.Entities;
using DIG.Aggro.Components;
using DIG.Combat.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19: Bridges aggro state to combat state system.
    /// When AI becomes aggroed, sets IsInCombat = true.
    /// Combat exit is handled by CombatStateSystem timeout.
    /// 
    /// Runs after AggroTargetSelectorSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(AggroTargetSelectorSystem))]
    [BurstCompile]
    public partial struct AggroCombatStateIntegration : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // No specific requirements
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (aggroState, combatState, entity) in
                SystemAPI.Query<RefRO<AggroState>, RefRW<CombatState>>()
                .WithEntityAccess())
            {
                // If aggroed and not in combat, enter combat
                if (aggroState.ValueRO.IsAggroed && !combatState.ValueRO.IsInCombat)
                {
                    combatState.ValueRW.IsInCombat = true;
                    combatState.ValueRW.TimeSinceLastCombatAction = 0f;
                    
                    // Enable EnteredCombatTag if present
                    if (SystemAPI.HasComponent<EnteredCombatTag>(entity))
                    {
                        SystemAPI.SetComponentEnabled<EnteredCombatTag>(entity, true);
                    }
                }
                
                // While aggroed, keep resetting the combat timer so we don't timeout
                if (aggroState.ValueRO.IsAggroed)
                {
                    combatState.ValueRW.TimeSinceLastCombatAction = 0f;
                }
                
                // Note: Combat exit is handled by CombatStateSystem timeout
                // We don't force exit here because the player might still be 
                // engaging even if AI lost sight temporarily
            }
        }
    }
}
