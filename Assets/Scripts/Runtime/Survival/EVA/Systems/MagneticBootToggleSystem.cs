using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Toggles magnetic boots on/off based on player input.
    /// Only allows toggling in EVA mode.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(EVAStateUpdateSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MagneticBootToggleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (eva, input, bootState) in
                     SystemAPI.Query<RefRO<EVAState>, RefRO<PlayerInput>, RefRW<MagneticBootState>>()
                     .WithAll<Simulate>())
            {
                var evaState = eva.ValueRO;
                var playerInput = input.ValueRO;
                ref var boots = ref bootState.ValueRW;

                // Only allow toggling in EVA mode
                if (!evaState.IsInEVA)
                {
                    // Disable boots when not in EVA
                    if (boots.IsEnabled)
                    {
                        boots.IsEnabled = false;
                        boots.IsAttached = false;
                    }
                    continue;
                }

                // Toggle boots on input
                if (playerInput.ToggleMagneticBoots.IsSet)
                {
                    boots.IsEnabled = !boots.IsEnabled;

                    // If disabling boots, also detach
                    if (!boots.IsEnabled)
                    {
                        boots.IsAttached = false;
                    }
                }
            }
        }
    }
}
