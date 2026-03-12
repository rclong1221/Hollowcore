using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Player.Abilities;

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// Handles the lifecycle of abilities:
    /// 1. Updates elapsed time for active ability
    /// 2. Handles transition from Pending -> Active
    /// 3. Updates IsActive flags in the buffer
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateAfter(typeof(AbilityPrioritySystem))]
    public partial struct AbilityLifecycleSystem : ISystem
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
            double currentTime = SystemAPI.Time.ElapsedTime;
            
            new UpdateAbilityLifecycleJob
            {
                DeltaTime = deltaTime,
                CurrentTime = (float)currentTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AbilitySystemTag))]
        public partial struct UpdateAbilityLifecycleJob : IJobEntity
        {
            public float DeltaTime;
            public float CurrentTime;

            public void Execute(
                ref AbilityState state,
                ref DynamicBuffer<AbilityDefinition> abilities)
            {
                // Handle Pending Ability Transition
                if (state.PendingAbilityIndex >= 0)
                {
                    // If switching abilities, stop the old one
                    if (state.ActiveAbilityIndex >= 0 && state.ActiveAbilityIndex < abilities.Length)
                    {
                        var oldAbility = abilities[state.ActiveAbilityIndex];
                        oldAbility.IsActive = false;
                        oldAbility.HasStarted = false;
                        abilities[state.ActiveAbilityIndex] = oldAbility;
                    }

                    // Start the new one
                    if (state.PendingAbilityIndex < abilities.Length)
                    {
                        var newAbility = abilities[state.PendingAbilityIndex];
                        newAbility.IsActive = true;
                        newAbility.HasStarted = true;
                        newAbility.StartTime = CurrentTime;
                        abilities[state.PendingAbilityIndex] = newAbility;

                        state.ActiveAbilityIndex = state.PendingAbilityIndex;
                        state.AbilityStartTime = CurrentTime;
                        state.AbilityElapsedTime = 0;
                    }
                    
                    // Clear pending
                    state.PendingAbilityIndex = -1;
                }

                // Update Active Ability Time
                if (state.ActiveAbilityIndex >= 0)
                {
                    state.AbilityElapsedTime += DeltaTime;
                    
                    // Validate active index is still within bounds
                    if (state.ActiveAbilityIndex >= abilities.Length)
                    {
                        state.ActiveAbilityIndex = -1;
                    }
                }
            }
        }
    }
}
