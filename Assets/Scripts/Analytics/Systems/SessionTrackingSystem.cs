using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Analytics
{
    /// <summary>
    /// Tracks session duration and player count changes.
    /// Fires player_join / player_leave events when player count changes.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(AnalyticsFlushSystem))]
    public partial class SessionTrackingSystem : SystemBase
    {
        private EntityQuery _playerQuery;
        private EntityQuery _sessionQuery;
        private float _sessionDuration;

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerTag>());
            _sessionQuery = GetEntityQuery(ComponentType.ReadWrite<SessionState>());
            RequireForUpdate(_sessionQuery);
        }

        protected override void OnUpdate()
        {
            if (!AnalyticsAPI.IsInitialized) return;
            if (!AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Session)) return;

            _sessionDuration += SystemAPI.Time.DeltaTime;

            int currentCount = _playerQuery.CalculateEntityCount();
            var session = _sessionQuery.GetSingleton<SessionState>();
            int previousCount = session.PlayerCount;

            if (currentCount != previousCount)
            {
                if (currentCount > previousCount)
                {
                    int joined = currentCount - previousCount;
                    for (int i = 0; i < joined; i++)
                    {
                        AnalyticsAPI.TrackEvent("Session", "player_join",
                            new Dictionary<string, object>
                            {
                                { "playerCount", currentCount },
                                { "sessionDuration", (int)_sessionDuration }
                            });
                    }
                }
                else
                {
                    int left = previousCount - currentCount;
                    for (int i = 0; i < left; i++)
                    {
                        AnalyticsAPI.TrackEvent("Session", "player_leave",
                            new Dictionary<string, object>
                            {
                                { "playerCount", currentCount },
                                { "sessionDuration", (int)_sessionDuration }
                            });
                    }
                }

                session.PlayerCount = currentCount;
                _sessionQuery.SetSingleton(session);
            }
        }
    }
}
