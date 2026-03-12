using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Core;
using DIG.Survival.Environment;

namespace DIG.Survival.Oxygen
{
    /// <summary>
    /// Applies suffocation damage when oxygen is depleted.
    /// Runs on server (authoritative) with client prediction.
    /// </summary>
    /// <remarks>
    /// Damage is applied when:
    /// 1. OxygenTank.Current == 0
    /// 2. Entity is in a zone that requires oxygen
    /// 3. Entity has SurvivalDamageEvent component
    /// 
    /// Damage rate is defined by OxygenTank.SuffocationDamagePerSecond.
    /// A bridge system (SurvivalDamageAdapterSystem) consumes the damage event.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OxygenDepletionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct OxygenSuffocationSystem : ISystem
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

            new SuffocationDamageJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct SuffocationDamageJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(
                ref SurvivalDamageEvent damageEvent,
                in OxygenTank tank,
                in CurrentEnvironmentZone zone,
                in OxygenConsumer _)
            {
                // Only apply damage if oxygen is required AND depleted
                if (!zone.OxygenRequired || !tank.IsDepleted)
                    return;

                // Accumulate suffocation damage
                float damage = tank.SuffocationDamagePerSecond * DeltaTime;
                damageEvent.PendingDamage += damage;
                damageEvent.Source = SurvivalDamageSource.Suffocation;
            }
        }
    }
}
