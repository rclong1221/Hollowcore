using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Abilities;
using Player.Components; // For PlayerInput, PlayerState
using DIG.Player.Components; // For PlayerMovementSettings

namespace DIG.Player.Systems.Abilities
{
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    public partial struct QuickStartSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (startAbility, startSettings, velocity, input, transform) in 
                     SystemAPI.Query<RefRW<QuickStartAbility>, RefRO<QuickStartSettings>, RefRW<PhysicsVelocity>, RefRO<PlayerInput>, RefRO<LocalTransform>>())
            {
                var settings = startSettings.ValueRO;
                float inputMag = math.length(new float2(input.ValueRO.Horizontal, input.ValueRO.Vertical));
                float speed = math.length(velocity.ValueRO.Linear * new float3(1, 0, 1)); // Horizontal speed

                // Transition: Idle -> Moving
                if (!startAbility.ValueRO.IsActive)
                {
                    // If moving slowly and sudden input detected
                    if (speed < settings.VelocityThreshold && inputMag > settings.MinInputMagnitude)
                    {
                        startAbility.ValueRW.IsActive = true;
                        startAbility.ValueRW.ElapsedTime = 0;
                        startAbility.ValueRW.Duration = settings.Duration;
                        startAbility.ValueRW.AccelerationMultiplier = settings.AccelerationMultiplier;
                        startAbility.ValueRW.MinInputMagnitude = settings.MinInputMagnitude;
                    }
                }
                else
                {
                    // Active Logic
                    startAbility.ValueRW.ElapsedTime += deltaTime;
                    
                    if (startAbility.ValueRW.ElapsedTime >= startAbility.ValueRO.Duration || inputMag < startAbility.ValueRO.MinInputMagnitude)
                    {
                        startAbility.ValueRW.IsActive = false;
                    }
                    else
                    {
                        // Apply Boost (This modifies velocity directly, or could modify a SpeedModifier)
                        // For simplicity in this architecture, we might just boost current velocity or rely on PlayerMovementSystem to read this state
                        // Assuming direct velocity modification for immediate "feel"
                        
                        // NOTE: In a perfect world, this writes to a SpeedModifier, but for "AddImpulse" feel, we might nudge velocity
                        // Let's assume we just flag IsActive, and PlayerMovementSystem or SpeedModifierSystem applies the force.
                        // OR we apply a forward impulse here.
                        
                        // Applying direct impulse for "Quick Start" feel
                        // Calculate forward dir from input relative to camera (simplified here as we don't have camera fwd access easily in this pure job without looking it up)
                        // Actually, let's defer velocity modification to the SpeedModifierSystem which will read QuickStartAbility.IsActive
                    }
                }
            }
        }
    }
}
