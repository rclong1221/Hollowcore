using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;
using DIG.Player.Abilities;
using Player.Components; 

namespace DIG.Player.Systems.Abilities
{
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    public partial struct IdleAbilitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Random source for variations (Unity.Mathematics.Random isn't thread safe without state, 
            // but we can instantiate one deterministically based on entity index + time if needed, 
            // or perform this on main thread if visuals only. For ECS prediction, we usually want deterministic random.)
            uint seed = (uint)SystemAPI.Time.ElapsedTime + 1; 

            foreach (var (idle, input, velocity) in 
                     SystemAPI.Query<RefRW<IdleAbility>, RefRO<PlayerInput>, RefRO<PhysicsVelocity>>())
            {
                bool hasInput = math.length(new float2(input.ValueRO.Horizontal, input.ValueRO.Vertical)) > 0.01f;
                bool isMoving = math.lengthsq(velocity.ValueRO.Linear) > 0.1f;
                
                if (hasInput || isMoving)
                {
                    // Reset Idle
                    idle.ValueRW.IdleTime = 0;
                    idle.ValueRW.CurrentVariation = 0; // 0 = standard idle
                    idle.ValueRW.NextVariationTime = 0;
                }
                else
                {
                    // Accumulate idle time
                    idle.ValueRW.IdleTime += deltaTime;
                    
                    if (idle.ValueRW.NextVariationTime <= 0)
                    {
                        // Initialize next variation timer
                        var rand = Unity.Mathematics.Random.CreateFromIndex(seed++);
                        idle.ValueRW.NextVariationTime = idle.ValueRW.IdleTime + 
                                                         rand.NextFloat(idle.ValueRO.MinVariationInterval, idle.ValueRO.MaxVariationInterval);
                    }
                    
                    if (idle.ValueRW.IdleTime >= idle.ValueRW.NextVariationTime)
                    {
                        // Trigger variation
                        var rand = Unity.Mathematics.Random.CreateFromIndex(seed++);
                        // Variation logic: e.g. 1 to Count
                        int nextVar = rand.NextInt(1, idle.ValueRO.VariationCount + 1);
                        
                        idle.ValueRW.CurrentVariation = nextVar;
                        
                        // Set next trigger
                        idle.ValueRW.NextVariationTime = idle.ValueRW.IdleTime + 
                                                         rand.NextFloat(idle.ValueRO.MinVariationInterval, idle.ValueRO.MaxVariationInterval);
                    }
                    else
                    {
                        // Reset variation after a short time? Or let animation handle it?
                        // Usually we set a trigger, animation plays, then returns to 0.
                        // For this data struct, we hold the variation index. 
                    }
                }
            }
        }
    }
}
