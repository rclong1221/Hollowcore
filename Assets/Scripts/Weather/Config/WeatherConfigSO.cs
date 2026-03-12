using System.Collections.Generic;
using UnityEngine;

namespace DIG.Weather
{
    [CreateAssetMenu(menuName = "DIG/Weather/Weather Config")]
    public class WeatherConfigSO : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("Minimum seconds between weather transitions.")]
        public float WeatherChangeIntervalMin = 180f;

        [Tooltip("Maximum seconds between weather transitions.")]
        public float WeatherChangeIntervalMax = 600f;

        [Header("Defaults")]
        public WeatherType DefaultWeather = WeatherType.Clear;
        public Season DefaultSeason = Season.Summer;

        [Tooltip("0 = time-based seed.")]
        public uint RandomSeed = 0;

        [Header("Season Temperatures (Celsius)")]
        [Tooltip("Base temperature per season: [Spring, Summer, Autumn, Winter].")]
        public float[] BaseTemperature = { 15f, 25f, 12f, -2f };

        [Header("Transition Probabilities")]
        public List<WeatherTransitionEntry> TransitionProbabilities = new();

        [Header("Per-Weather Target Parameters")]
        public List<WeatherTargetParamsEntry> WeatherTargetParams = new();

        [Header("Gameplay Modifiers")]
        public List<WeatherGameplayEntry> GameplayModifiers = new();

        [System.Serializable]
        public struct WeatherTransitionEntry
        {
            public WeatherType FromWeather;
            public Season Season;
            [Tooltip("10 weights, one per target WeatherType (Clear through Sandstorm).")]
            public float[] Weights;
        }

        [System.Serializable]
        public struct WeatherTargetParamsEntry
        {
            public WeatherType Weather;
            [Range(0f, 1f)] public float RainIntensity;
            [Range(0f, 1f)] public float SnowIntensity;
            [Range(0f, 1f)] public float FogDensity;
            [Range(0f, 30f)] public float WindSpeed;
            public float TemperatureOffset;
            public float TransitionDurationMin;
            public float TransitionDurationMax;
            public float MinDuration;
            public float MaxDuration;
            public float LightningIntervalMin;
            public float LightningIntervalMax;
        }

        [System.Serializable]
        public struct WeatherGameplayEntry
        {
            public WeatherType Weather;
            [Range(0f, 2f)] public float VisionRangeMultiplier;
            [Range(0f, 2f)] public float MovementSpeedMultiplier;
            [Range(0f, 2f)] public float SurfaceFrictionMultiplier;
            [Range(0f, 2f)] public float NoiseMultiplier;
            [Range(0f, 2f)] public float ProjectileSpeedMultiplier;
        }
    }
}
