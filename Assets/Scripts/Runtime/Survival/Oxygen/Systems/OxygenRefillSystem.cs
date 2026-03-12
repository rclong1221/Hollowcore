using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Environment;

namespace DIG.Survival.Oxygen
{
    /// <summary>
    /// Refills oxygen when entity is in a pressurized zone (not requiring oxygen).
    /// Simulates automatic refill from ship/station atmosphere.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(OxygenDepletionSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct OxygenRefillSystem : ISystem
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

            new OxygenRefillJob
            {
                DeltaTime = deltaTime,
                RefillRatePerSecond = 20f // Refill 5x faster than depletion
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct OxygenRefillJob : IJobEntity
        {
            public float DeltaTime;
            public float RefillRatePerSecond;

            void Execute(
                ref OxygenTank tank,
                in CurrentEnvironmentZone zone,
                in OxygenConsumer _)
            {
                // Only refill in zones that don't require oxygen
                if (zone.OxygenRequired)
                    return;

                // Already full
                if (tank.Current >= tank.Max)
                    return;

                // Refill
                tank.Current += RefillRatePerSecond * DeltaTime;

                // Clamp to max
                if (tank.Current > tank.Max)
                    tank.Current = tank.Max;
            }
        }
    }
}
