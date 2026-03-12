using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using DIG.Player.Abilities;

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// Evaluates start and stop conditions for abilities.
    /// 1. Checks Duration stoppers.
    /// 2. Sets CanStop flags.
    /// 3. Sets CanStart flags (simplified for now, full input separation later).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateBefore(typeof(AbilityPrioritySystem))]
    public partial struct AbilityTriggerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AbilityState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            new EvaluateTriggersJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AbilitySystemTag))]
        public partial struct EvaluateTriggersJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(
                ref DynamicBuffer<AbilityDefinition> abilities,
                in AbilityState state)
            {
                for (int i = 0; i < abilities.Length; i++)
                {
                    var ability = abilities[i];
                    
                    // --- STOP LOGIC ---
                    if (ability.IsActive)
                    {
                        // Duration Stopper Logic
                        if (ability.StopType == AbilityStopType.Duration)
                        {
                            // We need to know the duration. Ideally stored in definition or separate component?
                            // For this MVP, let's assume a hardcoded duration or future component lookups.
                            // Since we don't have per-ability component lookup here inside IJobEntity nicely without aliasing complexity,
                            // we'll defer complex stop logic.
                            
                            // Placeholder: immediate stop for duration types to prevent sticking
                            ability.CanStop = true; 
                        }
                        else
                        {
                            // Default: allow stop unless locked
                            ability.CanStop = true;
                        }
                    }
                    else
                    {
                        // Reset stop flag when inactive
                        ability.CanStop = false;
                    }

                    // --- START LOGIC ---
                    
                    // Simple auto-start for testing
                    if (ability.StartType == AbilityStartType.Automatic)
                    {
                        ability.CanStart = true;
                    }
                    // For now, Input systems will be separate and write to CanStart.
                    // This system aggregates generic logic.
                    
                    abilities[i] = ability;
                }
            }
        }
    }
}
