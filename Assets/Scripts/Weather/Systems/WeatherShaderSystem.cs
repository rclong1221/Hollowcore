using Unity.Entities;
using UnityEngine;

namespace DIG.Weather
{
    /// <summary>
    /// Client-only: sets global shader properties every frame so any shader
    /// can read weather data without system dependencies.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WeatherSkyboxSystem))]
    public partial class WeatherShaderSystem : SystemBase
    {
        private static readonly int TimeOfDayId = Shader.PropertyToID("_TimeOfDay");
        private static readonly int NormalizedTimeId = Shader.PropertyToID("_NormalizedTime");
        private static readonly int RainIntensityId = Shader.PropertyToID("_RainIntensity");
        private static readonly int SnowIntensityId = Shader.PropertyToID("_SnowIntensity");
        private static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
        private static readonly int WindDirectionXId = Shader.PropertyToID("_WindDirectionX");
        private static readonly int WindDirectionYId = Shader.PropertyToID("_WindDirectionY");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int TemperatureId = Shader.PropertyToID("_Temperature");
        private static readonly int WeatherTransitionId = Shader.PropertyToID("_WeatherTransition");

        protected override void OnCreate()
        {
            RequireForUpdate<WorldTimeState>();
            RequireForUpdate<WeatherState>();
        }

        protected override void OnUpdate()
        {
            var timeState = SystemAPI.GetSingleton<WorldTimeState>();
            var ws = SystemAPI.GetSingleton<WeatherState>();

            Shader.SetGlobalFloat(TimeOfDayId, timeState.TimeOfDay);
            Shader.SetGlobalFloat(NormalizedTimeId, timeState.TimeOfDay / 24.0f);
            Shader.SetGlobalFloat(RainIntensityId, ws.RainIntensity);
            Shader.SetGlobalFloat(SnowIntensityId, ws.SnowIntensity);
            Shader.SetGlobalFloat(FogDensityId, ws.FogDensity);
            Shader.SetGlobalFloat(WindDirectionXId, ws.WindDirectionX);
            Shader.SetGlobalFloat(WindDirectionYId, ws.WindDirectionY);
            Shader.SetGlobalFloat(WindSpeedId, ws.WindSpeed);
            Shader.SetGlobalFloat(TemperatureId, ws.Temperature);
            Shader.SetGlobalFloat(WeatherTransitionId, ws.TransitionProgress);
        }
    }
}
