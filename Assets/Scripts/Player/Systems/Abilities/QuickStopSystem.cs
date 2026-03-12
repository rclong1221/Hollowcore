using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using DIG.Player.Abilities;
using Player.Components; 

namespace DIG.Player.Systems.Abilities
{
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    public partial struct QuickStopSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (stopAbility, stopSettings, velocity, input) in 
                     SystemAPI.Query<RefRW<QuickStopAbility>, RefRO<QuickStopSettings>, RefRW<PhysicsVelocity>, RefRO<PlayerInput>>())
            {
                var settings = stopSettings.ValueRO;
                float inputMag = math.length(new float2(input.ValueRO.Horizontal, input.ValueRO.Vertical));
                float3 horizontalVel = velocity.ValueRO.Linear * new float3(1, 0, 1);
                float speed = math.length(horizontalVel);

                if (!stopAbility.ValueRO.IsActive)
                {
                    // Moving fast -> No Input
                    if (speed > settings.MinVelocityToTrigger && inputMag < 0.1f)
                    {
                        stopAbility.ValueRW.IsActive = true;
                        stopAbility.ValueRW.ElapsedTime = 0;
                        stopAbility.ValueRW.Duration = settings.Duration;
                        stopAbility.ValueRW.DecelerationMultiplier = settings.DecelerationMultiplier;
                        stopAbility.ValueRW.StopDirection = math.normalize(horizontalVel);
                        stopAbility.ValueRW.MinVelocityToTrigger = settings.MinVelocityToTrigger;
                    }
                }
                else
                {
                    stopAbility.ValueRW.ElapsedTime += deltaTime;

                    // Stop if duration ends OR player starts moving again
                    if (stopAbility.ValueRW.ElapsedTime >= stopAbility.ValueRO.Duration || inputMag > 0.1f)
                    {
                        stopAbility.ValueRW.IsActive = false;
                    }
                    else
                    {
                        // Apply braking force
                         float brakingFactor = settings.DecelerationMultiplier * deltaTime;
                         // Reduce velocity against the stop direction
                         velocity.ValueRW.Linear -= stopAbility.ValueRO.StopDirection * brakingFactor * speed; // Proportional braking
                    }
                }
            }
        }
    }
}
