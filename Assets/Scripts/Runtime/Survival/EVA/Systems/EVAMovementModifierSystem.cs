using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Modifies PlayerMovementSettings when player enters/exits EVA mode.
    /// Stores original settings and applies EVA modifiers (slower movement, reduced jump, etc.).
    /// Runs before PlayerMoveSystem to ensure modified settings are used.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(EVAStateUpdateSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct EVAMovementModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (eva, modifier, settings, originalSettings) in
                     SystemAPI.Query<RefRO<EVAState>, RefRO<EVAMovementModifier>,
                                    RefRW<PlayerMovementSettings>, RefRW<EVAOriginalMovementSettings>>()
                     .WithAll<Simulate>())
            {
                var evaState = eva.ValueRO;
                var mod = modifier.ValueRO;
                ref var movementSettings = ref settings.ValueRW;
                ref var origSettings = ref originalSettings.ValueRW;

                if (evaState.IsInEVA)
                {
                    // Entering or staying in EVA mode
                    if (!origSettings.HasStoredSettings)
                    {
                        // Store original settings on first frame of EVA
                        origSettings.HasStoredSettings = true;
                        origSettings.WalkSpeed = movementSettings.WalkSpeed;
                        origSettings.RunSpeed = movementSettings.RunSpeed;
                        origSettings.SprintSpeed = movementSettings.SprintSpeed;
                        origSettings.CrouchSpeed = movementSettings.CrouchSpeed;
                        origSettings.ProneSpeed = movementSettings.ProneSpeed;
                        origSettings.JumpForce = movementSettings.JumpForce;
                        origSettings.AirAcceleration = movementSettings.AirAcceleration;
                        origSettings.Gravity = movementSettings.Gravity;
                    }

                    // Apply EVA modifiers
                    movementSettings.WalkSpeed = origSettings.WalkSpeed * mod.SpeedMultiplier;
                    movementSettings.RunSpeed = origSettings.RunSpeed * mod.SpeedMultiplier;
                    movementSettings.SprintSpeed = origSettings.SprintSpeed * mod.SpeedMultiplier;
                    movementSettings.CrouchSpeed = origSettings.CrouchSpeed * mod.SpeedMultiplier;
                    movementSettings.ProneSpeed = origSettings.ProneSpeed * mod.SpeedMultiplier;
                    movementSettings.JumpForce = origSettings.JumpForce * mod.JumpForceMultiplier;
                    movementSettings.AirAcceleration = origSettings.AirAcceleration * mod.AirControlMultiplier;

                    // Apply gravity override if set (not -1)
                    if (mod.GravityOverride >= 0f)
                    {
                        movementSettings.Gravity = -mod.GravityOverride; // Negative because gravity points down
                    }
                }
                else if (origSettings.HasStoredSettings)
                {
                    // Exiting EVA mode - restore original settings
                    movementSettings.WalkSpeed = origSettings.WalkSpeed;
                    movementSettings.RunSpeed = origSettings.RunSpeed;
                    movementSettings.SprintSpeed = origSettings.SprintSpeed;
                    movementSettings.CrouchSpeed = origSettings.CrouchSpeed;
                    movementSettings.ProneSpeed = origSettings.ProneSpeed;
                    movementSettings.JumpForce = origSettings.JumpForce;
                    movementSettings.AirAcceleration = origSettings.AirAcceleration;
                    movementSettings.Gravity = origSettings.Gravity;

                    // Clear stored settings flag
                    origSettings.HasStoredSettings = false;
                }
            }
        }
    }
}
