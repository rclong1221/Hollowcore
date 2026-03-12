using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Core;

namespace DIG.Survival.Radiation
{
    /// <summary>
    /// Applies radiation damage when exposure exceeds threshold.
    /// Damage scales with how far over the threshold the exposure is.
    /// </summary>
    /// <remarks>
    /// A bridge system (SurvivalDamageAdapterSystem) consumes the damage event.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(RadiationAccumulationSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct RadiationDamageSystem : ISystem
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

            new RadiationDamageJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct RadiationDamageJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(
                ref SurvivalDamageEvent damageEvent,
                in RadiationExposure exposure,
                in RadiationSusceptible _)
            {
                // Only apply damage if over threshold
                if (!exposure.IsTakingDamage)
                    return;

                // Damage scales linearly with how far over threshold
                // At threshold: 0 damage. At 2x threshold: full DamagePerSecond
                float overThreshold = exposure.Current - exposure.DamageThreshold;
                float damageMultiplier = overThreshold / exposure.DamageThreshold;
                float damage = exposure.DamagePerSecond * damageMultiplier * DeltaTime;

                damageEvent.PendingDamage += damage;
                damageEvent.Source = SurvivalDamageSource.Radiation;
            }
        }
    }
}
