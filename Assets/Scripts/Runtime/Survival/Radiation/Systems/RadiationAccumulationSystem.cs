using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Environment;

namespace DIG.Survival.Radiation
{
    /// <summary>
    /// Accumulates radiation when in radioactive zones, decays when outside.
    /// Runs on server (authoritative) with client prediction.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct RadiationAccumulationSystem : ISystem
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

            new RadiationUpdateJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct RadiationUpdateJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(
                ref RadiationExposure exposure,
                in CurrentEnvironmentZone zone,
                in RadiationSusceptible _)
            {
                // Update current accumulation rate from zone
                exposure.CurrentAccumulationRate = zone.RadiationRate;

                if (zone.RadiationRate > 0)
                {
                    // In radioactive zone: accumulate
                    exposure.Current += zone.RadiationRate * DeltaTime;
                }
                else if (exposure.Current > 0)
                {
                    // Outside radioactive zone: decay
                    exposure.Current -= exposure.DecayRatePerSecond * DeltaTime;
                    
                    // Don't go below zero
                    if (exposure.Current < 0f)
                        exposure.Current = 0f;
                }
            }
        }
    }
}
