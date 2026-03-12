using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.EVA;
using DIG.Survival.Environment;

namespace DIG.Survival.Oxygen
{
    /// <summary>
    /// Depletes oxygen for entities in zones that require oxygen.
    /// Only runs on server (authoritative) with client prediction.
    /// </summary>
    /// <remarks>
    /// Oxygen depletion triggers when:
    /// 1. Entity has OxygenTank component
    /// 2. Entity has OxygenConsumer tag (not a robot/vehicle)
    /// 3. Entity is in a zone where OxygenRequired == true
    /// 
    /// Note: EVAState.IsInEVA is set based on zone detection, but this system
    /// uses CurrentEnvironmentZone.OxygenRequired directly for more flexibility
    /// (e.g., underwater areas also require oxygen but aren't "EVA").
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Survival.Environment.EnvironmentZoneDetectionSystem))]
    public partial struct OxygenDepletionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (tank, zone, _) in 
                     SystemAPI.Query<RefRW<OxygenTank>, RefRO<CurrentEnvironmentZone>, RefRO<OxygenConsumer>>())
            {
                if (!zone.ValueRO.OxygenRequired)
                    continue;
                
                float depletionRate = tank.ValueRO.DepletionRatePerSecond 
                    * tank.ValueRO.LeakMultiplier 
                    * zone.ValueRO.OxygenDepletionMultiplier;
                
                tank.ValueRW.Current -= depletionRate * deltaTime;
                
                if (tank.ValueRO.Current < 0f)
                    tank.ValueRW.Current = 0f;
            }
        }

        [BurstCompile]
        partial struct OxygenDepletionJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(
                ref OxygenTank tank,
                in CurrentEnvironmentZone zone,
                in OxygenConsumer _) // Tag filter
            {
                // Only deplete if current zone requires oxygen
                if (!zone.OxygenRequired)
                    return;

                // Calculate depletion: base rate * leak multiplier * zone multiplier
                float depletionRate = tank.DepletionRatePerSecond 
                    * tank.LeakMultiplier 
                    * zone.OxygenDepletionMultiplier;

                // Deplete oxygen
                tank.Current -= depletionRate * DeltaTime;

                // Clamp to zero (don't go negative)
                if (tank.Current < 0f)
                    tank.Current = 0f;
            }
        }
    }
}
