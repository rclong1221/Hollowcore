using UnityEngine;

namespace DIG.Analytics
{
    /// <summary>
    /// Optional wrapper around Unity Analytics SDK.
    /// Falls back to no-op if the package is not installed.
    /// </summary>
    public class UnityAnalyticsTarget : IAnalyticsTarget
    {
        public string TargetName => "UnityAnalytics";

        public void Initialize(DispatchTargetConfig config)
        {
#if UNITY_ANALYTICS_AVAILABLE
            Debug.Log("[Analytics:UnityAnalytics] Unity Analytics target initialized");
#endif
        }

        public void SendBatch(AnalyticsEvent[] events)
        {
#if UNITY_ANALYTICS_AVAILABLE
            if (events == null) return;
            for (int i = 0; i < events.Length; i++)
            {
                ref var evt = ref events[i];
                Unity.Services.Analytics.AnalyticsService.Instance.CustomData(
                    evt.Action.ToString(),
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "category", evt.Category.ToString() },
                        { "sessionId", evt.SessionId.ToString() },
                        { "tick", evt.ServerTick },
                        { "props", evt.PropertiesJson.ToString() }
                    }
                );
            }
#endif
        }

        public void Shutdown()
        {
#if UNITY_ANALYTICS_AVAILABLE
            Unity.Services.Analytics.AnalyticsService.Instance.Flush();
#endif
        }
    }
}
