using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Player.Components;

namespace Player.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LandingRecoverySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (lsRef, pStateRef, entity) in SystemAPI.Query<RefRW<LandingState>, RefRW<PlayerState>>().WithEntityAccess())
            {
                // Access data directly via RefRW to modify in place without ECB or copying
                ref var ls = ref lsRef.ValueRW;
                ref var pState = ref pStateRef.ValueRW;
                
                if (!ls.IsRecovering) continue;

                ls.RecoveryTimer -= dt;
                if (ls.RecoveryTimer <= 0f)
                {
                    ls.IsRecovering = false;
                    ls.RecoveryTimer = 0f;
                    pState.MovementState = PlayerMovementState.Idle;
                }
                else
                {
                    // While recovering, force movement state to Idle to block locomotion
                    pState.MovementState = PlayerMovementState.Idle;
                }
                
                // No need to write back or use ECB - RefRW modifies component data directly in the chunk
            }

            // Zero player input for recovering players (entities that have PlayerInputComponent)
            foreach (var (inputRef, lsRef, entity) in SystemAPI.Query<RefRW<Player.Components.PlayerInputComponent>, RefRO<LandingState>>().WithEntityAccess())
            {
                if (!lsRef.ValueRO.IsRecovering) continue;
                
                ref var input = ref inputRef.ValueRW;
                input.Move = float2.zero;
                input.Jump = 0;
                input.Crouch = 0;
                input.Sprint = 0;
                
                // No need for ECB
            }
        }
    }
}
