using Unity.Entities;
using Unity.NetCode;

namespace DIG.Weather
{
    /// <summary>
    /// Server-authoritative weather state singleton (40 bytes).
    /// Ghost-replicated to all clients for presentation systems.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeatherState : IComponentData
    {
        [GhostField] public WeatherType CurrentWeather;
        [GhostField] public WeatherType NextWeather;
        [GhostField(Quantization = 1000)] public float TransitionProgress; // 0.0 - 1.0
        [GhostField(Quantization = 100)] public float WindDirectionX;
        [GhostField(Quantization = 100)] public float WindDirectionY;
        [GhostField(Quantization = 100)] public float WindSpeed;           // m/s
        [GhostField(Quantization = 1000)] public float RainIntensity;      // 0.0 - 1.0
        [GhostField(Quantization = 1000)] public float SnowIntensity;      // 0.0 - 1.0
        [GhostField(Quantization = 1000)] public float FogDensity;         // 0.0 - 1.0
        [GhostField(Quantization = 100)] public float LightningTimer;      // seconds until next strike
        [GhostField(Quantization = 10)] public float Temperature;          // Celsius
    }
}
