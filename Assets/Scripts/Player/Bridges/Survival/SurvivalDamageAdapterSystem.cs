using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Core;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// [DEPRECATED] Bridge system that consumes SurvivalDamageEvent and applies to Health.
    /// This is now replaced by SurvivalDamageBridgeSystem which routes through the DamageEvent pipeline.
    /// </summary>
    /// <remarks>
    /// This system is disabled to allow SurvivalDamageBridgeSystem to handle survival damage.
    /// The new system converts SurvivalDamageEvent → DamageEvent → DamageApplySystem → Health.
    /// This ensures all damage goes through the unified pipeline (EPIC 4.1).
    /// 
    /// To re-enable direct damage (bypassing pipeline), remove [DisableAutoCreation].
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation] // DISABLED: replaced by SurvivalDamageBridgeSystem (EPIC 4.1)
    public partial struct SurvivalDamageAdapterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ApplySurvivalDamageJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct ApplySurvivalDamageJob : IJobEntity
        {
            void Execute(
                ref Health health,
                ref SurvivalDamageEvent damageEvent)
            {
                // Skip if no damage pending
                if (damageEvent.PendingDamage <= 0f)
                    return;

                // Apply accumulated survival damage
                health.Current -= damageEvent.PendingDamage;

                // Clamp to zero
                if (health.Current < 0f)
                    health.Current = 0f;

                // Reset for next frame
                damageEvent.PendingDamage = 0f;
                damageEvent.Source = SurvivalDamageSource.None;
            }
        }
    }
}

