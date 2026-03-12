using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Monitors Health changes and updates HealthChangedEvent (13.16.11).
    /// Run on both Server and Client to drive UI/Feedback.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageSystemGroup))] // Run after damage/healing applied
    public partial struct HealthEventSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Health>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var combatLookup = SystemAPI.GetComponentLookup<CombatState>(true);
            new CheckHealthChangeJob
            {
                CombatLookup = combatLookup
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct CheckHealthChangeJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<CombatState> CombatLookup;

            void Execute(
                Entity entity,
                ref HealthStateTracker tracker,
                ref HealthChangedEvent param,
                EnabledRefRW<HealthChangedEvent> enabledParams,
                in Health health)
            {
                if (System.Math.Abs(health.Current - tracker.PreviousHealth) > 0.001f)
                {
                    // Update event data
                    param.OldValue = tracker.PreviousHealth;
                    param.NewValue = health.Current;
                    param.Delta = health.Current - tracker.PreviousHealth;
                    
                    // Populate Source if possible
                    param.Source = Entity.Null;
                    if (param.Delta < 0 && CombatLookup.HasComponent(entity))
                    {
                        param.Source = CombatLookup[entity].LastAttacker;
                    }

                    // Enable event to signal change
                    enabledParams.ValueRW = true;

                    // Update tracker
                    tracker.PreviousHealth = health.Current;
                }
                else
                {
                    // Disable event if no change this frame
                    enabledParams.ValueRW = false;
                }
            }
        }
    }
}
