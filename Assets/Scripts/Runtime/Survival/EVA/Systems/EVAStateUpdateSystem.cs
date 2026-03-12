using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Environment;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Updates EVAState based on current environment zone.
    /// Sets IsInEVA to true when in vacuum or other non-pressurized zones.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct EVAStateUpdateSystem : ISystem
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
            var currentTime = (float)SystemAPI.Time.ElapsedTime;

            new EVAStateUpdateJob
            {
                DeltaTime = deltaTime,
                CurrentTime = currentTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct EVAStateUpdateJob : IJobEntity
        {
            public float DeltaTime;
            public float CurrentTime;

            void Execute(
                ref EVAState eva,
                in CurrentEnvironmentZone zone,
                in EVACapable _)
            {
                // Determine if we should be in EVA based on zone type
                bool shouldBeInEVA = zone.ZoneType == EnvironmentZoneType.Vacuum ||
                                     zone.ZoneType == EnvironmentZoneType.Toxic;

                // Transition into EVA
                if (shouldBeInEVA && !eva.IsInEVA)
                {
                    eva.IsInEVA = true;
                    eva.EnteredEVATime = CurrentTime;
                    eva.TimeInEVA = 0f;
                }
                // Transition out of EVA
                else if (!shouldBeInEVA && eva.IsInEVA)
                {
                    eva.IsInEVA = false;
                    eva.TetheredToEntity = Entity.Null;
                    // Keep TimeInEVA for mission stats
                }
                // Accumulate time in EVA
                else if (eva.IsInEVA)
                {
                    eva.TimeInEVA += DeltaTime;
                }
            }
        }
    }
}
