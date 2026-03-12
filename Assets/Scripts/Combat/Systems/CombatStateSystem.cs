using Unity.Entities;
using Unity.Burst;
using DIG.Combat.Components;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// System that manages combat state transitions.
    /// Updates timers and toggles IsInCombat based on combat activity.
    /// 
    /// Runs after CombatResolutionSystem so it can react to new combat events
    /// processed in the same frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatResolutionSystem))]
    public partial struct CombatStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatState>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
            
            // Update all combat states
            new UpdateCombatStateJob
            {
                DeltaTime = deltaTime,
                ElapsedTime = elapsedTime
            }.ScheduleParallel();
        }
        
        [BurstCompile]
        partial struct UpdateCombatStateJob : IJobEntity
        {
            public float DeltaTime;
            public float ElapsedTime;
            
            void Execute(
                ref CombatState combatState,
                EnabledRefRW<EnteredCombatTag> enteredCombat,
                EnabledRefRW<ExitedCombatTag> exitedCombat)
            {
                // Clear event tags from previous frame
                enteredCombat.ValueRW = false;
                exitedCombat.ValueRW = false;
                
                if (combatState.IsInCombat)
                {
                    // Increment time since last combat action
                    combatState.TimeSinceLastCombatAction += DeltaTime;
                    
                    // Check if we should exit combat
                    if (combatState.TimeSinceLastCombatAction >= combatState.CombatDropTime)
                    {
                        combatState.IsInCombat = false;
                        combatState.CombatExitTime = ElapsedTime;
                        exitedCombat.ValueRW = true;
                    }
                }
            }
        }
    }
}
