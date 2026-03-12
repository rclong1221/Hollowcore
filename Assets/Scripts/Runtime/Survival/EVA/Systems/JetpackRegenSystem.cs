using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Regenerates jetpack fuel when not thrusting.
    /// Applies regen delay before fuel starts regenerating.
    /// Runs after JetpackSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(JetpackSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct JetpackRegenSystem : ISystem
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

            new JetpackRegenJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct JetpackRegenJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref JetpackState jetpack)
            {
                // Only regen when not thrusting
                if (jetpack.IsThrusting)
                {
                    return;
                }

                // Don't regen if already full
                if (jetpack.IsFull)
                {
                    return;
                }

                // Increment time since thrust
                jetpack.TimeSinceThrust += DeltaTime;

                // Wait for regen delay before regenerating
                if (jetpack.TimeSinceThrust < jetpack.RegenDelay)
                {
                    return;
                }

                // Regenerate fuel
                jetpack.Fuel = math.min(jetpack.MaxFuel, jetpack.Fuel + jetpack.FuelRegenRate * DeltaTime);
            }
        }
    }
}
