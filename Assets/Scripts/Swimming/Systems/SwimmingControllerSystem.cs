using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Swimming.Systems
{
    /// <summary>
    /// 12.3.10: Controller Lock System
    /// Manages controller state transitions when entering/exiting swimming.
    /// Disables ground check and caches original values for restoration.
    /// Updates PlayerState.MovementState to Swimming when appropriate.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwimmingMovementSystem))]
    public partial struct SwimmingControllerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (swimState, controllerState, playerState, entity) in
                SystemAPI.Query<
                    RefRO<SwimmingState>,
                    RefRW<SwimmingControllerState>,
                    RefRW<PlayerState>>()
                    .WithAll<CanSwim>()
                    .WithEntityAccess())
            {
                bool isSwimming = swimState.ValueRO.IsSwimming;
                bool wasSwimming = controllerState.ValueRO.WasSwimming;

                // Detect swim state transitions
                if (isSwimming && !wasSwimming)
                {
                    // ENTERING SWIM MODE
                    OnEnterSwimming(ref controllerState.ValueRW, ref playerState.ValueRW);
                }
                else if (!isSwimming && wasSwimming)
                {
                    // EXITING SWIM MODE
                    OnExitSwimming(ref controllerState.ValueRW, ref playerState.ValueRW);
                }

                // Update movement state while swimming
                if (isSwimming)
                {
                    // Override movement state to Swimming
                    if (playerState.ValueRO.MovementState != PlayerMovementState.Swimming)
                    {
                        playerState.ValueRW.MovementState = PlayerMovementState.Swimming;
                    }

                    // Disable ground check by setting distance to 0
                    // This prevents IsGrounded from triggering during swim
                    playerState.ValueRW.GroundCheckDistance = 0f;

                    // Force IsGrounded to false while swimming
                    playerState.ValueRW.IsGrounded = false;
                }

                // Update was swimming for next frame
                controllerState.ValueRW.WasSwimming = isSwimming;
            }
        }

        /// <summary>
        /// Called when player enters swimming mode.
        /// Caches original controller values and disables conflicting systems.
        /// </summary>
        private static void OnEnterSwimming(ref SwimmingControllerState controllerState, ref PlayerState playerState)
        {
            // Cache original values if not already cached
            if (!controllerState.HasCachedValues)
            {
                controllerState.OriginalGroundCheckDistance = playerState.GroundCheckDistance;
                controllerState.OriginalColliderHeight = playerState.CurrentHeight;
                controllerState.HasCachedValues = true;
            }

            // Set movement state
            playerState.MovementState = PlayerMovementState.Swimming;

            // Disable ground check
            playerState.GroundCheckDistance = 0f;
            playerState.IsGrounded = false;
        }

        /// <summary>
        /// Called when player exits swimming mode.
        /// Restores original controller values.
        /// </summary>
        private static void OnExitSwimming(ref SwimmingControllerState controllerState, ref PlayerState playerState)
        {
            // Restore cached values
            if (controllerState.HasCachedValues)
            {
                playerState.GroundCheckDistance = controllerState.OriginalGroundCheckDistance;
                // Note: Collider height is managed by PlayerStanceSystem, we just restore ground check
            }

            // Reset movement state (will be updated by other systems based on input)
            if (playerState.MovementState == PlayerMovementState.Swimming)
            {
                playerState.MovementState = PlayerMovementState.Falling;
            }

            // Clear cached flag to allow re-caching on next swim
            controllerState.HasCachedValues = false;
        }
    }
}
