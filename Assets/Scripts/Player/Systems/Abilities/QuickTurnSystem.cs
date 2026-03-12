using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Abilities;
using DIG.Core.Input;
using Player.Components; 

namespace DIG.Player.Systems.Abilities
{
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    public partial struct QuickTurnSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // EPIC 15.20: Skip QuickTurn entirely in screen-relative (isometric/ARPG) mode
            // In these modes, the character instantly faces movement direction, so QuickTurn
            // would fight with that rotation and cause jitter
            bool useScreenRelativeMovement = false;
            if (SystemAPI.HasSingleton<ParadigmSettings>())
            {
                var paradigmSettings = SystemAPI.GetSingleton<ParadigmSettings>();
                useScreenRelativeMovement = paradigmSettings.IsValid && paradigmSettings.UseScreenRelativeMovement;
            }
            
            if (useScreenRelativeMovement)
                return; // Skip QuickTurn entirely in isometric mode

            foreach (var (turnAbility, turnSettings, velocity, input, transform) in 
                     SystemAPI.Query<RefRW<QuickTurnAbility>, RefRO<QuickTurnSettings>, RefRW<PhysicsVelocity>, RefRO<PlayerInput>, RefRW<LocalTransform>>())
            {
                var settings = turnSettings.ValueRO;
                
                // Get local input vector
                float3 inputVecLocal = new float3(input.ValueRO.Horizontal, 0, input.ValueRO.Vertical);
                float inputMagSq = math.lengthsq(inputVecLocal);
                
                // Get world velocity
                float3 velFlat = velocity.ValueRO.Linear * new float3(1, 0, 1);
                float speedSq = math.lengthsq(velFlat);

                if (!turnAbility.ValueRO.IsActive)
                {
                    // Optimization: Check squared magnitudes first to avoid expensive Sqrt/Rotate calls
                    // 2.0f * 2.0f = 4.0f
                    // 0.5f * 0.5f = 0.25f
                    if (speedSq > 4.0f && inputMagSq > 0.25f)
                    {
                        // Now calculate actual magnitudes and world rotation (only when needed)
                        // Optimization: Rotate logic moved inside the check
                        float3 inputVecWorld = math.rotate(transform.ValueRO.Rotation, inputVecLocal);
                        
                        float speed = math.sqrt(speedSq);
                        
                        // Optimization: Avoid normalize() which uses expensive rsqrt()
                        // Instead, scale the threshold by magnitudes: dot(A,B) < thresh * |A| * |B|
                        float inputMag = math.sqrt(inputMagSq);
                        float scaledThreshold = settings.DirectionThreshold * speed * inputMag;

                        if (math.dot(velFlat, inputVecWorld) < scaledThreshold)
                        {
                            turnAbility.ValueRW.IsActive = true;
                            turnAbility.ValueRW.ElapsedTime = 0;
                            turnAbility.ValueRW.Duration = settings.Duration;
                            turnAbility.ValueRW.TurnSpeed = settings.TurnSpeed;
                            
                            // We do need normalized direction here for the target state
                            turnAbility.ValueRW.TargetDirection = math.normalize(inputVecWorld); 
                            turnAbility.ValueRW.MomentumRetention = settings.MomentumRetention;
                            
                            // Preserve some momentum in new direction
                            velocity.ValueRW.Linear = turnAbility.ValueRW.TargetDirection * speed * settings.MomentumRetention; 
                        }
                    }
                }
                else
                {
                    turnAbility.ValueRW.ElapsedTime += deltaTime;
                    
                    // Rotate character smoothly towards target direction
                    // Note: PlayerMovementSystem also rotates, so we might fight it unless we set a strict state
                    quaternion targetRot = quaternion.LookRotation(turnAbility.ValueRO.TargetDirection, math.up());
                    
                    transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, settings.TurnSpeed * deltaTime);

                    if (turnAbility.ValueRW.ElapsedTime >= turnAbility.ValueRO.Duration)
                    {
                        turnAbility.ValueRW.IsActive = false;
                    }
                }
            }
        }
    }
}
