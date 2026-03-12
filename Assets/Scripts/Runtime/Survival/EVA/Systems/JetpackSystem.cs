using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Handles jetpack thrust input and fuel consumption.
    /// Applies vertical thrust when Jump is held in EVA mode and fuel is available.
    /// Runs after input gathering, before player movement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(EVAStateUpdateSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct JetpackSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (eva, input, jetpack, velocity) in
                     SystemAPI.Query<RefRO<EVAState>, RefRO<PlayerInput>,
                                    RefRW<JetpackState>, RefRW<PhysicsVelocity>>()
                     .WithAll<Simulate>())
            {
                var evaState = eva.ValueRO;
                var playerInput = input.ValueRO;
                ref var jetpackState = ref jetpack.ValueRW;
                ref var vel = ref velocity.ValueRW;

                // Jetpack only works in EVA mode
                if (!evaState.IsInEVA)
                {
                    jetpackState.IsThrusting = false;
                    continue;
                }

                // Check if thrust input is held and we have fuel
                bool wantsThrust = playerInput.Jump.IsSet;
                bool hasFuel = jetpackState.Fuel > 0f;
                bool shouldThrust = wantsThrust && hasFuel;

                if (shouldThrust)
                {
                    // Apply vertical thrust
                    vel.Linear.y += jetpackState.ThrustForce * deltaTime;

                    // Consume fuel
                    jetpackState.Fuel = math.max(0f, jetpackState.Fuel - jetpackState.FuelConsumptionRate * deltaTime);
                    jetpackState.IsThrusting = true;
                    jetpackState.TimeSinceThrust = 0f;
                }
                else
                {
                    // Not thrusting - will be handled by JetpackRegenSystem
                    if (jetpackState.IsThrusting)
                    {
                        // Just stopped thrusting - reset timer for regen delay
                        jetpackState.TimeSinceThrust = 0f;
                    }
                    jetpackState.IsThrusting = false;
                }
            }
        }
    }
}
