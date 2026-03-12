using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using DIG.Player.Abilities;

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// Determines which ability should be active based on:
    /// 1. Currently active ability
    /// 2. Abilities requesting to start (CanStart = true)
    /// 3. Priority comparison
    /// 4. Blocking masks
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    public partial struct AbilityPrioritySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AbilityState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ResolveAbilityPriorityJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AbilitySystemTag))]
        public partial struct ResolveAbilityPriorityJob : IJobEntity
        {
            public void Execute(
                ref AbilityState state,
                ref DynamicBuffer<AbilityDefinition> abilities)
            {
                int bestCandidateIndex = -1;
                int currentPriority = -1;
                
                // 1. Identify current active ability priority
                if (state.ActiveAbilityIndex >= 0 && state.ActiveAbilityIndex < abilities.Length)
                {
                    currentPriority = abilities[state.ActiveAbilityIndex].Priority;
                }

                // 2. Iterate all abilities to find the best candidate to START
                for (int i = 0; i < abilities.Length; i++)
                {
                    var ability = abilities[i];

                    // Skip if cannot start
                    if (!ability.CanStart) continue;

                    // Skip if already active (lifecycle handles continuation)
                    if (i == state.ActiveAbilityIndex) continue;

                    // Check if higher priority than current
                    if (ability.Priority > currentPriority)
                    {
                        // Check if this new candidate is better than previous best candidate
                        int bestCandidatePriority = bestCandidateIndex >= 0 ? abilities[bestCandidateIndex].Priority : -1;
                        
                        if (ability.Priority > bestCandidatePriority)
                        {
                            // Check for blocking
                            // (Simplified: A higher priority ability usually implies it internally blocks lower ones,
                            // but we can add explicit mask checks here if needed)
                            
                            bestCandidateIndex = i;
                        }
                    }
                }

                // 3. If a better candidate is found, set as pending
                if (bestCandidateIndex >= 0)
                {
                    state.PendingAbilityIndex = bestCandidateIndex;
                }
                // 4. Check if current ability should STOP
                else if (state.ActiveAbilityIndex >= 0 && state.ActiveAbilityIndex < abilities.Length)
                {
                    var currentAbility = abilities[state.ActiveAbilityIndex];
                    if (currentAbility.CanStop)
                    {
                        // Stop current, switch to none (or default idle if implemented)
                        // Setting Active to -1 will be handled by lifecycle if Pending is -1?
                        // Actually, Lifecycle logic needs an explicit signal to start "Nothing".
                        // For now, let's assume if CanStop is true, we just clear active index.
                        
                        // BUT, usually we want to fall back to a default state.
                        // For this simple implementation:
                        state.ActiveAbilityIndex = -1; 
                        state.PendingAbilityIndex = -1;
                    }
                }
            }
        }
    }
}
