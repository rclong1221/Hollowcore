using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Weather
{
    /// <summary>
    /// Client-only: blends skybox material properties between dawn/day/dusk/night
    /// and controls star visibility based on time-of-day.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WeatherLightingSystem))]
    public partial class WeatherSkyboxSystem : SystemBase
    {
        private bool _configCached;
        private Material _skyboxDawn, _skyboxDay, _skyboxDusk, _skyboxNight;
        private Material _activeSkybox;

        private static readonly int StarVisibilityId = Shader.PropertyToID("_StarVisibility");
        private static readonly int CloudDensityId = Shader.PropertyToID("_CloudDensity");
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");

        protected override void OnCreate()
        {
            RequireForUpdate<WorldTimeState>();
            RequireForUpdate<WeatherState>();
        }

        protected override void OnUpdate()
        {
            if (!_configCached)
            {
                var dayNightConfig = Resources.Load<DayNightConfigSO>("DayNightConfig");
                if (dayNightConfig != null)
                {
                    _skyboxDawn = dayNightConfig.SkyboxDawnMaterial;
                    _skyboxDay = dayNightConfig.SkyboxDayMaterial;
                    _skyboxDusk = dayNightConfig.SkyboxDuskMaterial;
                    _skyboxNight = dayNightConfig.SkyboxNightMaterial;
                }
                _configCached = true;
            }

            var timeState = SystemAPI.GetSingleton<WorldTimeState>();
            var ws = SystemAPI.GetSingleton<WeatherState>();
            float hour = timeState.TimeOfDay;
            float t = hour / 24f;

            // Determine period and blend
            Material fromMat, toMat;
            float blendT;
            GetSkyboxBlend(hour, out fromMat, out toMat, out blendT);

            // Set active skybox if we have materials
            if (fromMat != null && RenderSettings.skybox != fromMat && blendT < 0.5f)
                RenderSettings.skybox = fromMat;
            else if (toMat != null && blendT >= 0.5f)
                RenderSettings.skybox = toMat;

            // Drive skybox shader properties
            var skybox = RenderSettings.skybox;
            if (skybox != null)
            {
                if (skybox.HasProperty(StarVisibilityId))
                {
                    float starVis = CalculateStarVisibility(t);
                    skybox.SetFloat(StarVisibilityId, starVis);
                }
                if (skybox.HasProperty(CloudDensityId))
                {
                    float cloudFactor = GetCloudDensity(ws);
                    skybox.SetFloat(CloudDensityId, cloudFactor);
                }
                if (skybox.HasProperty(SunDirectionId))
                {
                    float sunAngle = t * math.PI * 2f - math.PI * 0.5f;
                    skybox.SetVector(SunDirectionId,
                        new Vector4(math.cos(sunAngle), math.sin(sunAngle), 0, 0));
                }
            }
        }

        private void GetSkyboxBlend(float hour, out Material from, out Material to, out float blend)
        {
            // Dawn: 5-7, Day: 7-17, Dusk: 17-19, Night: 19-5
            if (hour >= 5f && hour < 7f)
            {
                from = _skyboxNight ?? _skyboxDay;
                to = _skyboxDawn ?? _skyboxDay;
                blend = (hour - 5f) / 2f;
            }
            else if (hour >= 7f && hour < 17f)
            {
                from = _skyboxDawn ?? _skyboxDay;
                to = _skyboxDay;
                blend = math.saturate((hour - 7f) / 2f);
            }
            else if (hour >= 17f && hour < 19f)
            {
                from = _skyboxDay;
                to = _skyboxDusk ?? _skyboxDay;
                blend = (hour - 17f) / 2f;
            }
            else
            {
                from = _skyboxDusk ?? _skyboxNight;
                to = _skyboxNight;
                blend = hour >= 19f
                    ? math.saturate((hour - 19f) / 3f)
                    : 1f;
            }
        }

        private static float CalculateStarVisibility(float t)
        {
            // Stars visible at night (t < 0.2 or t > 0.8), fade at dawn/dusk
            if (t < 0.21f) return 1f;
            if (t < 0.29f) return math.saturate(1f - (t - 0.21f) / 0.08f);
            if (t < 0.71f) return 0f;
            if (t < 0.79f) return math.saturate((t - 0.71f) / 0.08f);
            return 1f;
        }

        private static float GetCloudDensity(WeatherState ws)
        {
            return ws.CurrentWeather switch
            {
                WeatherType.Clear => 0f,
                WeatherType.PartlyCloudy => 0.3f,
                WeatherType.Cloudy => 0.6f,
                WeatherType.LightRain => 0.7f,
                WeatherType.HeavyRain => 0.9f,
                WeatherType.Thunderstorm => 1.0f,
                WeatherType.LightSnow => 0.6f,
                WeatherType.HeavySnow => 0.85f,
                WeatherType.Fog => 0.5f,
                WeatherType.Sandstorm => 0.8f,
                _ => 0f
            };
        }
    }
}
