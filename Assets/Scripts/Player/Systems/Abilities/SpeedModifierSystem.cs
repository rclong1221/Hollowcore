using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using DIG.Player.Abilities;

namespace DIG.Player.Systems.Abilities
{
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateBefore(typeof(QuickStartSystem))] // Calculate mods before using them (if we used them there)
    public partial struct SpeedModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (modState, modBuffer) in 
                     SystemAPI.Query<RefRW<SpeedModifierState>, DynamicBuffer<SpeedModifier>>())
            {
                var buffer = modBuffer;
                float baseMult = 1.0f;
                
                // Process buffer in reverse to allow removal
                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    var mod = buffer[i];
                    
                    // Apply multiplier
                    baseMult *= mod.Multiplier;
                    
                    // Update lifetime if not permanent
                    if (mod.Duration > 0)
                    {
                        mod.ElapsedTime += deltaTime;
                        if (mod.ElapsedTime >= mod.Duration)
                        {
                            buffer.RemoveAt(i);
                            continue;
                        }
                        buffer[i] = mod; // Write back updated time
                    }
                }
                
                // Write final combined multiplier to state
                modState.ValueRW.CombinedMultiplier = baseMult;
                
                // Clamp within global min/max limits
                // Logic for max speed cap would go here if we wanted to clamp the result
                // But usually we assume movement system does the clamping based on Min/Max fields
            }
        }
    }
}
