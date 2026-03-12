using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Environment;

namespace DIG.Survival.Oxygen
{
    /// <summary>
    /// Client-side system that triggers oxygen warning audio/UI.
    /// Fires one-shot warnings when crossing thresholds.
    /// </summary>
    /// <remarks>
    /// Runs on client only (PresentationSystemGroup).
    /// Uses OxygenWarningState to track which warnings have fired.
    /// Resets warning state when oxygen is refilled above threshold.
    /// </remarks>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct OxygenWarningSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Only run on client
            state.RequireForUpdate<NetworkId>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (tank, warningState, zone) in 
                SystemAPI.Query<RefRO<OxygenTank>, RefRW<OxygenWarningState>, RefRO<CurrentEnvironmentZone>>())
            {
                // Only process if in oxygen-required zone
                if (!zone.ValueRO.OxygenRequired)
                {
                    // Reset warnings when in safe zone
                    if (warningState.ValueRO.WarningTriggered || 
                        warningState.ValueRO.CriticalTriggered ||
                        warningState.ValueRO.SuffocatingActive)
                    {
                        warningState.ValueRW.WarningTriggered = false;
                        warningState.ValueRW.CriticalTriggered = false;
                        warningState.ValueRW.SuffocatingActive = false;
                    }
                    continue;
                }

                var oxygen = tank.ValueRO;

                // Check suffocation (oxygen depleted)
                if (oxygen.IsDepleted)
                {
                    if (!warningState.ValueRO.SuffocatingActive)
                    {
                        warningState.ValueRW.SuffocatingActive = true;
                        // TODO: Trigger suffocation audio/VFX
                        // AudioManager.Play("Suffocating");
                        // VFXManager.StartEffect("SuffocationOverlay");
                    }
                }
                else
                {
                    warningState.ValueRW.SuffocatingActive = false;
                }

                // Check critical threshold
                if (oxygen.IsCritical && !warningState.ValueRO.CriticalTriggered)
                {
                    warningState.ValueRW.CriticalTriggered = true;
                    // TODO: Trigger critical audio/UI
                    // AudioManager.Play("OxygenCritical");
                    // UIManager.ShowWarning("OXYGEN CRITICAL");
                }
                // Reset critical if oxygen refilled above critical
                else if (!oxygen.IsCritical && warningState.ValueRO.CriticalTriggered)
                {
                    warningState.ValueRW.CriticalTriggered = false;
                }

                // Check warning threshold
                if (oxygen.IsWarning && !oxygen.IsCritical && !warningState.ValueRO.WarningTriggered)
                {
                    warningState.ValueRW.WarningTriggered = true;
                    // TODO: Trigger warning audio/UI
                    // AudioManager.Play("OxygenWarning");
                    // UIManager.ShowWarning("Oxygen Low");
                }
                // Reset warning if oxygen refilled above warning
                else if (!oxygen.IsWarning && warningState.ValueRO.WarningTriggered)
                {
                    warningState.ValueRW.WarningTriggered = false;
                }
            }
        }
    }
}
