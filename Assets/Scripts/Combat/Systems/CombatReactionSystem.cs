using Unity.Entities;
using Unity.Burst;
using DIG.Combat.Components;
using DIG.Combat.Systems; // For CombatResultEvent

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Reactive system that puts entities into combat when they participate in combat events.
    /// Runs after CombatResolutionSystem to process new CombatResultEvents.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatResolutionSystem))]
    [UpdateBefore(typeof(CombatStateSystem))]
    public partial struct CombatReactionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatResultEvent>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var combatEvent in SystemAPI.Query<RefRO<CombatResultEvent>>())
            {
                var result = combatEvent.ValueRO;

                if (!result.DidHit)
                    continue;

                // Put attacker into combat
                if (SystemAPI.HasComponent<CombatState>(result.AttackerEntity))
                {
                    var attackerState = SystemAPI.GetComponentRW<CombatState>(result.AttackerEntity);
                    EnterCombat(ref attackerState.ValueRW, ref state, result.AttackerEntity, elapsedTime);
                }

                // Put target into combat
                if (SystemAPI.HasComponent<CombatState>(result.TargetEntity))
                {
                    var targetState = SystemAPI.GetComponentRW<CombatState>(result.TargetEntity);
                    EnterCombat(ref targetState.ValueRW, ref state, result.TargetEntity, elapsedTime);
                }
            }
        }
        
        private void EnterCombat(ref CombatState combatState, ref SystemState state, Entity entity, float elapsedTime)
        {
            bool wasInCombat = combatState.IsInCombat;
            
            // Reset timer and enter combat
            combatState.TimeSinceLastCombatAction = 0f;
            combatState.IsInCombat = true;
            combatState.CombatExitTime = float.NegativeInfinity;
            
            // Fire entered combat event if this is a new combat engagement
            if (!wasInCombat && SystemAPI.HasComponent<EnteredCombatTag>(entity))
            {
                SystemAPI.SetComponentEnabled<EnteredCombatTag>(entity, true);
            }
        }
    }
}
