using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Bridges MetaBank ECS state to the managed IMetaUIProvider.
    /// Runs in PresentationSystemGroup (client/local only).
    /// Notifies UI of currency changes, unlock purchases, and continuous MetaScreen updates.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MetaUIBridgeSystem : SystemBase
    {
        private int _lastMetaCurrency;
        private RunPhase _lastPhase;
        private bool _runResultsNotified;

        protected override void OnCreate()
        {
            RequireForUpdate<MetaBank>();
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            if (!MetaUIRegistry.HasProvider)
                return;

            var bank = SystemAPI.GetSingleton<MetaBank>();
            var run = SystemAPI.GetSingleton<RunState>();
            var provider = MetaUIRegistry.Provider;

            // Detect meta-currency changes
            if (bank.MetaCurrency != _lastMetaCurrency)
            {
                int delta = bank.MetaCurrency - _lastMetaCurrency;
                provider.OnMetaCurrencyChanged(bank.MetaCurrency, delta);
                _lastMetaCurrency = bank.MetaCurrency;
            }

            // Phase transition: entering MetaScreen
            if (run.Phase == RunPhase.MetaScreen && _lastPhase != RunPhase.MetaScreen)
            {
                _runResultsNotified = false;
            }

            // Notify run results once on entering MetaScreen
            if (run.Phase == RunPhase.MetaScreen && !_runResultsNotified)
            {
                // Find the most recent history entry for meta-currency earned
                int metaEarned = 0;
                var bankEntity = SystemAPI.GetSingletonEntity<MetaBank>();
                var history = SystemAPI.GetBuffer<RunHistoryEntry>(bankEntity);
                if (history.Length > 0)
                    metaEarned = history[history.Length - 1].MetaCurrencyEarned;

                provider.OnRunResultsReady(
                    metaEarned,
                    bank.MetaCurrency,
                    run.EndReason,
                    run.Score,
                    run.CurrentZoneIndex
                );
                _runResultsNotified = true;
            }

            // Continuous MetaScreen updates
            if (run.Phase == RunPhase.MetaScreen)
            {
                provider.UpdateMetaScreen(
                    bank.MetaCurrency,
                    bank.TotalRunsAttempted,
                    bank.TotalRunsWon,
                    bank.BestScore,
                    bank.BestZoneReached,
                    bank.TotalPlaytime
                );
            }

            _lastPhase = run.Phase;
        }
    }
}
