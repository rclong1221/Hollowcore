using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Weather
{
    /// <summary>
    /// Client-only: drives directional light rotation, color, intensity,
    /// and ambient light based on time-of-day and weather state.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class WeatherLightingSystem : SystemBase
    {
        private Light _cachedLight;
        private bool _lightSearched;

        protected override void OnCreate()
        {
            RequireForUpdate<WorldTimeState>();
            RequireForUpdate<WeatherState>();
            RequireForUpdate<WeatherManagerSingleton>();
        }

        protected override void OnUpdate()
        {
            if (!_lightSearched)
            {
                _cachedLight = Object.FindAnyObjectByType<Light>();
                _lightSearched = true;
            }

            var mgr = SystemAPI.ManagedAPI.GetSingleton<WeatherManagerSingleton>();
            if (!mgr.IsInitialized || !mgr.DayNightConfig.IsCreated) return;

            var timeState = SystemAPI.GetSingleton<WorldTimeState>();
            var ws = SystemAPI.GetSingleton<WeatherState>();
            ref var cfg = ref mgr.DayNightConfig.Value;

            float t = timeState.TimeOfDay / 24.0f; // 0-1 normalized

            // --- Sun angle ---
            float sunAngle = CalculateSunAngle(
                timeState.TimeOfDay, cfg.SunriseHour, cfg.SunsetHour, cfg.SunPitchMax);

            // Sun yaw rotates 360 degrees over the day
            float sunYaw = t * 360f - 90f;

            if (_cachedLight != null)
            {
                _cachedLight.transform.rotation = Quaternion.Euler(sunAngle, sunYaw, 0f);

                // Sun color from gradient
                var sunColor = SampleColorGradient(ref cfg.SunColorGradient, t);
                _cachedLight.color = new Color(sunColor.x, sunColor.y, sunColor.z);

                // Sun intensity from curve, dimmed by weather
                float baseIntensity = SampleIntensityCurve(ref cfg.SunIntensityCurve, t);
                float cloudCoverage = GetCloudCoverage(ws);
                float weatherDim = 1.0f - (cloudCoverage * 0.4f + ws.RainIntensity * 0.3f + ws.SnowIntensity * 0.2f);
                _cachedLight.intensity = baseIntensity * math.max(weatherDim, 0.05f);
            }

            // --- Ambient light ---
            var ambientColor = SampleColorGradient(ref cfg.AmbientColorGradient, t);
            RenderSettings.ambientLight = new Color(ambientColor.x, ambientColor.y, ambientColor.z);

            float sunFactor = math.saturate(math.sin(t * math.PI));
            RenderSettings.ambientIntensity = math.lerp(
                cfg.NightAmbientIntensity, cfg.DayAmbientIntensity, sunFactor);
        }

        private static float CalculateSunAngle(float timeOfDay, float sunrise, float sunset, float maxPitch)
        {
            if (timeOfDay >= sunrise && timeOfDay <= sunset)
            {
                float dayProgress = (timeOfDay - sunrise) / (sunset - sunrise);
                return math.sin(dayProgress * math.PI) * maxPitch;
            }

            // Night: sun below horizon
            float nightDuration = 24f - (sunset - sunrise);
            float nightProgress;
            if (timeOfDay > sunset)
                nightProgress = (timeOfDay - sunset) / nightDuration;
            else
                nightProgress = (timeOfDay + 24f - sunset) / nightDuration;

            return -math.sin(nightProgress * math.PI) * 20f;
        }

        private static float3 SampleColorGradient(ref BlobArray<ColorKeyframe> gradient, float t)
        {
            if (gradient.Length == 0)
                return new float3(1, 1, 1);

            // Find the two keyframes to lerp between
            int lower = 0;
            for (int i = 0; i < gradient.Length - 1; i++)
            {
                if (gradient[i + 1].Time > t)
                    break;
                lower = i + 1;
            }

            int upper = math.min(lower + 1, gradient.Length - 1);
            if (lower == upper)
                return new float3(gradient[lower].R, gradient[lower].G, gradient[lower].B);

            float segT = math.saturate(
                (t - gradient[lower].Time) / math.max(gradient[upper].Time - gradient[lower].Time, 0.001f));

            return math.lerp(
                new float3(gradient[lower].R, gradient[lower].G, gradient[lower].B),
                new float3(gradient[upper].R, gradient[upper].G, gradient[upper].B),
                segT);
        }

        private static float SampleIntensityCurve(ref BlobArray<float> curve, float t)
        {
            if (curve.Length == 0) return 1f;
            float idx = t * (curve.Length - 1);
            int lower = (int)idx;
            int upper = math.min(lower + 1, curve.Length - 1);
            float frac = idx - lower;
            return math.lerp(curve[lower], curve[upper], frac);
        }

        private static float GetCloudCoverage(WeatherState ws)
        {
            return ws.CurrentWeather switch
            {
                WeatherType.Clear => 0f,
                WeatherType.PartlyCloudy => 0.3f,
                WeatherType.Cloudy => 0.7f,
                WeatherType.LightRain => 0.8f,
                WeatherType.HeavyRain => 0.95f,
                WeatherType.Thunderstorm => 1.0f,
                WeatherType.LightSnow => 0.7f,
                WeatherType.HeavySnow => 0.9f,
                WeatherType.Fog => 0.6f,
                WeatherType.Sandstorm => 0.9f,
                _ => 0f
            };
        }
    }
}
