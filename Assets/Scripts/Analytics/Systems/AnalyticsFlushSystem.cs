using Unity.Entities;
using Unity.NetCode;

namespace DIG.Analytics
{
    /// <summary>
    /// Periodic flush trigger. Signals background dispatcher on timer expiry.
    /// On shutdown: ends session and flushes all pending events.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class AnalyticsFlushSystem : SystemBase
    {
        private float _timeSinceFlush;
        private EntityQuery _configQuery;

        protected override void OnCreate()
        {
            _configQuery = GetEntityQuery(ComponentType.ReadOnly<AnalyticsConfig>());
            RequireForUpdate(_configQuery);
        }

        protected override void OnUpdate()
        {
            if (!AnalyticsAPI.IsInitialized) return;

            var config = _configQuery.GetSingleton<AnalyticsConfig>();
            _timeSinceFlush += SystemAPI.Time.DeltaTime;

            if (_timeSinceFlush >= config.FlushIntervalSec)
            {
                _timeSinceFlush = 0f;
                AnalyticsAPI.Dispatcher?.SignalFlush();
            }
        }

        protected override void OnDestroy()
        {
            if (!AnalyticsAPI.IsInitialized) return;
            AnalyticsAPI.EndSession("shutdown");
            AnalyticsAPI.Shutdown();
        }
    }
}
