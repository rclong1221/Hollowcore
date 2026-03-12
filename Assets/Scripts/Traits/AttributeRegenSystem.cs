using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

namespace Traits
{
    /// <summary>
    /// Processes auto-regeneration or decay for attributes.
    /// Supports RegenDelay (wait after modification) and RegenRate (amount/sec).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AttributeRegenSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            double currentTime = SystemAPI.Time.ElapsedTime;
            
            // Iterate over all entities with AttributeData buffer
            foreach (var b in SystemAPI.Query<DynamicBuffer<AttributeData>>())
            {
                // Create local mutable reference to buffer (it's a struct pointer)
                var buffer = b;
                for (int i = 0; i < buffer.Length; i++)
                {
                    var attr = buffer[i];

                    // Check if Regen is active
                    if (math.abs(attr.RegenRate) < 0.0001f) continue;
                    
                    // Check Regen Delay
                    if ((currentTime - attr.LastChangeTime) < attr.RegenDelay) continue;
                    
                    // Apply Regen
                    float delta = attr.RegenRate * deltaTime;
                    float newValue = math.clamp(attr.CurrentValue + delta, attr.MinValue, attr.MaxValue);
                    
                    // Update if changed
                    if (math.abs(newValue - attr.CurrentValue) > 0.0001f)
                    {
                        attr.CurrentValue = newValue;
                        // Don't update LastChangeTime for regen itself, otherwise delay would never finish? 
                        // Actually Opsive logic typically resets delay on EXTERNAL change, not internal regen.
                        // So we assume LastChangeTime is only touched by external damage/healing events.
                        
                        buffer[i] = attr; 
                    }
                }
            }
        }
    }
}
