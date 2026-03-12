using Unity.Entities;

namespace DIG.Weather
{
    /// <summary>
    /// BlobAsset containing weather transition probability weights.
    /// Flattened 3D array: [fromWeather * SeasonCount + season] -> array of weights per target WeatherType.
    /// </summary>
    public struct WeatherTransitionBlob
    {
        /// <summary>Flattened weights: (10 * 4) rows x 10 columns = 400 floats.</summary>
        public BlobArray<float> TransitionWeights;
        public int WeatherTypeCount; // 10
        public int SeasonCount;      // 4
    }

    public struct DayNightBlob
    {
        public float DayLengthSeconds;       // Real seconds per game day (default 1200)
        public int SeasonLengthDays;         // Game days per season (default 7)
        public float SunriseHour;            // 6.0
        public float SunsetHour;             // 18.0
        public float SunPitchMax;            // Maximum sun elevation degrees
        public float MoonPitchMax;           // Moon elevation at midnight
        public float NightAmbientIntensity;  // Ambient light at midnight
        public float DayAmbientIntensity;    // Ambient light at noon

        public BlobArray<ColorKeyframe> SunColorGradient;     // 8 keyframes
        public BlobArray<ColorKeyframe> AmbientColorGradient; // 8 keyframes
        public BlobArray<float> SunIntensityCurve;            // 24 samples (one per hour)
    }

    public struct ColorKeyframe
    {
        public float Time; // 0.0 - 1.0 (fraction of day)
        public float R, G, B;
    }

    public struct WeatherParamsBlob
    {
        public BlobArray<WeatherTargetParams> TargetParams;     // 10 entries
        public BlobArray<WeatherGameplayParams> GameplayParams; // 10 entries
    }

    public struct WeatherTargetParams
    {
        public float TargetRainIntensity;
        public float TargetSnowIntensity;
        public float TargetFogDensity;
        public float TargetWindSpeed;
        public float TargetTemperatureOffset;
        public float TransitionDurationMin;
        public float TransitionDurationMax;
        public float MinDuration;
        public float MaxDuration;
        public float LightningIntervalMin;
        public float LightningIntervalMax;
    }

    public struct WeatherGameplayParams
    {
        public float VisionRangeMultiplier;
        public float MovementSpeedMultiplier;
        public float SurfaceFrictionMultiplier;
        public float NoiseMultiplier;
        public float ProjectileSpeedMultiplier;
    }
}
