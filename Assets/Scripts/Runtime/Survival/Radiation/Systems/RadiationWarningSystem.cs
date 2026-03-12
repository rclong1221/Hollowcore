using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.EVA;

namespace DIG.Survival.Radiation
{
    /// <summary>
    /// Client-side system that triggers radiation warning audio/UI.
    /// Includes Geiger counter clicks that increase with radiation level.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct RadiationWarningSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (exposure, warningState) in 
                SystemAPI.Query<RefRO<RadiationExposure>, RefRW<RadiationWarningState>>())
            {
                var rad = exposure.ValueRO;

                // Geiger counter logic: active whenever radiation > 0
                bool shouldGeigerBeActive = rad.Current > 0;
                if (shouldGeigerBeActive != warningState.ValueRO.GeigerActive)
                {
                    warningState.ValueRW.GeigerActive = shouldGeigerBeActive;
                    // TODO: Start/stop Geiger counter audio
                    // if (shouldGeigerBeActive)
                    //     AudioManager.StartLoop("GeigerCounter", clickRate: rad.Current / 100f);
                    // else
                    //     AudioManager.StopLoop("GeigerCounter");
                }

                // Critical: at or above damage threshold
                if (rad.IsTakingDamage && !warningState.ValueRO.CriticalTriggered)
                {
                    warningState.ValueRW.CriticalTriggered = true;
                    // TODO: Trigger critical radiation warning
                    // AudioManager.Play("RadiationCritical");
                    // UIManager.ShowWarning("RADIATION CRITICAL");
                }
                else if (!rad.IsTakingDamage && warningState.ValueRO.CriticalTriggered)
                {
                    warningState.ValueRW.CriticalTriggered = false;
                }

                // Warning: at warning threshold (e.g., 50% of damage threshold)
                float warningLevel = rad.DamageThreshold * rad.WarningThreshold;
                bool isWarning = rad.Current >= warningLevel && !rad.IsTakingDamage;
                
                if (isWarning && !warningState.ValueRO.WarningTriggered)
                {
                    warningState.ValueRW.WarningTriggered = true;
                    // TODO: Trigger warning
                    // AudioManager.Play("RadiationWarning");
                    // UIManager.ShowWarning("Radiation exposure increasing");
                }
                else if (!isWarning && warningState.ValueRO.WarningTriggered)
                {
                    warningState.ValueRW.WarningTriggered = false;
                }
            }
        }
    }
}
