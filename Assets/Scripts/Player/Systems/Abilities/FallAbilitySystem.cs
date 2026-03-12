using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Abilities;
using Player.Components;

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// DEPRECATED: This system has been superseded by FallDetectionSystem (EPIC 13.14).
    ///
    /// FallDetectionSystem now provides full Opsive parity with:
    /// - 13.14.1: Minimum fall height check
    /// - 13.14.2: Land surface impact VFX/audio
    /// - 13.14.3: Animation event wait for land complete
    /// - 13.14.4: Blend tree float data
    /// - 13.14.5: Immediate transform change handling
    /// - 13.14.6: State index for animation
    ///
    /// This system is kept for reference but disabled via [DisableAutoCreation].
    /// Remove this file after verifying FallDetectionSystem works correctly.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [DisableAutoCreation] // EPIC 13.14: Disabled - superseded by FallDetectionSystem
    public partial struct FallAbilitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system is disabled and superseded by FallDetectionSystem.
            // Keeping code for reference only.

            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (fall, playerState, transform) in
                     SystemAPI.Query<RefRW<FallAbility>, RefRO<PlayerState>, RefRO<LocalTransform>>())
            {
                if (!playerState.ValueRO.IsGrounded)
                {
                    if (!fall.ValueRO.IsFalling)
                    {
                        fall.ValueRW.IsFalling = true;
                        fall.ValueRW.FallStartHeight = transform.ValueRO.Position.y;
                        fall.ValueRW.FallDuration = 0;
                    }
                    else
                    {
                        fall.ValueRW.FallDuration += deltaTime;
                    }
                }
                else
                {
                    if (fall.ValueRO.IsFalling)
                    {
                        fall.ValueRW.IsFalling = false;
                        fall.ValueRW.FallDuration = 0;
                    }
                }
            }
        }
    }
}
