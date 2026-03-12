using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Managed SystemBase in PresentationSystemGroup. Reads RunState,
    /// pushes to RunUIRegistry for game HUD. Follows CombatUIBridgeSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RunUIBridgeSystem : SystemBase
    {
        private RunPhase _lastReportedPhase;
        private byte _lastReportedZone;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            if (!RunUIRegistry.HasProvider)
                return;

            var run = SystemAPI.GetSingleton<RunState>();
            var provider = RunUIRegistry.Provider;

            // Phase change notification
            if (run.Phase != _lastReportedPhase)
            {
                provider.OnPhaseChanged(run.Phase, _lastReportedPhase);

                if (_lastReportedPhase == RunPhase.None && run.Phase == RunPhase.Lobby)
                    provider.OnRunStart(run.RunId, run.Seed, run.MaxZones);

                _lastReportedPhase = run.Phase;
            }

            // Zone change notification
            if (run.CurrentZoneIndex != _lastReportedZone && run.Phase >= RunPhase.Active)
            {
                provider.OnZoneChanged(run.CurrentZoneIndex, run.ZoneSeed);
                _lastReportedZone = run.CurrentZoneIndex;
            }

            // Continuous HUD update during active phases
            if (run.Phase >= RunPhase.Active && run.Phase <= RunPhase.ZoneTransition)
            {
                provider.UpdateHUD(
                    run.ElapsedTime,
                    run.Score,
                    run.RunCurrency,
                    run.CurrentZoneIndex,
                    run.MaxZones
                );
            }
        }
    }
}
