using Unity.Entities;
using UnityEngine;

namespace DIG.Weather
{
    /// <summary>
    /// Place on a GameObject in the subscene to create weather singletons.
    /// Baker creates the WorldTimeState and WeatherState singleton entities.
    /// </summary>
    [AddComponentMenu("DIG/Weather/Weather Bootstrap")]
    public class WeatherBootstrapAuthoring : MonoBehaviour
    {
        [Header("Configuration")]
        public DayNightConfigSO DayNightConfig;
        public WeatherConfigSO WeatherConfig;

        public class Baker : Baker<WeatherBootstrapAuthoring>
        {
            public override void Bake(WeatherBootstrapAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                float startTime = authoring.DayNightConfig != null
                    ? authoring.DayNightConfig.StartTimeOfDay
                    : 8.0f;

                var defaultWeather = authoring.WeatherConfig != null
                    ? authoring.WeatherConfig.DefaultWeather
                    : WeatherType.Clear;

                var defaultSeason = authoring.WeatherConfig != null
                    ? authoring.WeatherConfig.DefaultSeason
                    : Season.Summer;

                float baseTemp = 20f;
                if (authoring.WeatherConfig != null &&
                    authoring.WeatherConfig.BaseTemperature != null &&
                    authoring.WeatherConfig.BaseTemperature.Length > (int)defaultSeason)
                {
                    baseTemp = authoring.WeatherConfig.BaseTemperature[(int)defaultSeason];
                }

                AddComponent(entity, new WorldTimeState
                {
                    TimeOfDay = startTime,
                    DayCount = 0,
                    Season = defaultSeason,
                    TimeScale = 1.0f,
                    IsPaused = false
                });

                AddComponent(entity, new WeatherState
                {
                    CurrentWeather = defaultWeather,
                    NextWeather = defaultWeather,
                    TransitionProgress = 1.0f,
                    WindDirectionX = 1f,
                    WindDirectionY = 0f,
                    WindSpeed = 0f,
                    RainIntensity = 0f,
                    SnowIntensity = 0f,
                    FogDensity = 0f,
                    LightningTimer = 0f,
                    Temperature = baseTemp
                });
            }
        }
    }
}
