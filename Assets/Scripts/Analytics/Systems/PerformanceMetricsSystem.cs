using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Analytics;

/// <summary>
/// Client-side performance monitoring. Samples FPS/memory every 60 frames.
/// Only fires analytics events on anomalies (FPS drops, memory spikes, frame hitches).
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class PerformanceMetricsSystem : SystemBase
{
    private int _frameCount;
    private float _rollingFps;
    private float _rollingFrameTimeMs;

    private const int SampleInterval = 60;
    private const float FpsDropThreshold = 20f;
    private const float FrameHitchThresholdMs = 50f;
    private const long MemorySpikeThresholdBytes = 2L * 1024 * 1024 * 1024; // 2 GB

    protected override void OnUpdate()
    {
        _frameCount++;

        if (!AnalyticsAPI.IsInitialized) return;
        if (!AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Performance)) return;

        if (_frameCount % SampleInterval != 0) return;

        float dt = UnityEngine.Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        float currentFps = 1f / dt;
        float currentFrameTimeMs = dt * 1000f;

        _rollingFps = _rollingFps > 0f
            ? _rollingFps * 0.9f + currentFps * 0.1f
            : currentFps;
        _rollingFrameTimeMs = _rollingFrameTimeMs > 0f
            ? _rollingFrameTimeMs * 0.9f + currentFrameTimeMs * 0.1f
            : currentFrameTimeMs;

        long managedMemory = System.GC.GetTotalMemory(false);
        long totalMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();

        if (_rollingFps < FpsDropThreshold)
        {
            var props = new FixedString512Bytes();
            props.Append("{\"fps\":");
            props.Append((int)_rollingFps);
            props.Append(",\"frameTimeMs\":");
            props.Append((int)_rollingFrameTimeMs);
            props.Append(",\"managedMB\":");
            props.Append((int)(managedMemory / (1024 * 1024)));
            props.Append('}');

            AnalyticsAPI.TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Performance,
                Action = new FixedString64Bytes("fps_drop"),
                PropertiesJson = props
            });
        }

        if (totalMemory > MemorySpikeThresholdBytes)
        {
            var props = new FixedString512Bytes();
            props.Append("{\"totalMB\":");
            props.Append((int)(totalMemory / (1024 * 1024)));
            props.Append(",\"managedMB\":");
            props.Append((int)(managedMemory / (1024 * 1024)));
            props.Append('}');

            AnalyticsAPI.TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Performance,
                Action = new FixedString64Bytes("memory_spike"),
                PropertiesJson = props
            });
        }

        if (currentFrameTimeMs > FrameHitchThresholdMs)
        {
            var props = new FixedString512Bytes();
            props.Append("{\"frameTimeMs\":");
            props.Append((int)currentFrameTimeMs);
            props.Append(",\"fps\":");
            props.Append((int)currentFps);
            props.Append('}');

            AnalyticsAPI.TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Performance,
                Action = new FixedString64Bytes("frame_hitch"),
                PropertiesJson = props
            });
        }
    }
}
