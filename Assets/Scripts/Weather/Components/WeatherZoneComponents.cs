using Unity.Entities;

namespace DIG.Weather
{
    /// <summary>
    /// Placed on trigger volume entities to define per-biome weather overrides.
    /// </summary>
    public struct WeatherZone : IComponentData
    {
        /// <summary>Designer-defined biome index (0-255).</summary>
        public byte BiomeType;

        /// <summary>Forced weather in this zone. 255 = follow global weather.</summary>
        public WeatherType WeatherOverride;

        /// <summary>Higher priority zones win on overlap.</summary>
        public byte Priority;

        public byte Padding;

        /// <summary>Zone radius for distance falloff. 0 = use collider bounds only.</summary>
        public float Radius;
    }

    /// <summary>Tag for weather zone volume entities.</summary>
    public struct WeatherZoneTag : IComponentData { }

    /// <summary>
    /// Client-only: effective local weather after zone overrides are applied.
    /// Written by WeatherZoneSystem on the local player entity.
    /// </summary>
    public struct LocalWeatherOverride : IComponentData
    {
        public bool HasOverride;
        public WeatherType OverrideWeather;
        /// <summary>0 = global weather, 1 = full zone override.</summary>
        public float BlendWeight;
        public byte BiomeType;
    }
}
