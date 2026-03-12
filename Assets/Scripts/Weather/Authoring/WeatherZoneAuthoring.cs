using Unity.Entities;
using UnityEngine;

namespace DIG.Weather
{
    /// <summary>
    /// Place on trigger volume GameObjects in the subscene to define weather zones.
    /// </summary>
    [AddComponentMenu("DIG/Weather/Weather Zone")]
    public class WeatherZoneAuthoring : MonoBehaviour
    {
        [Header("Zone Configuration")]
        public WeatherZoneConfigSO Config;

        [Header("Inline (used if Config is null)")]
        public byte BiomeType;
        public WeatherType WeatherOverride = (WeatherType)255;
        public byte Priority;
        public float BlendRadius = 20f;

        public class Baker : Baker<WeatherZoneAuthoring>
        {
            public override void Bake(WeatherZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                byte biome = authoring.BiomeType;
                var weatherOverride = authoring.WeatherOverride;
                byte priority = authoring.Priority;
                float radius = authoring.BlendRadius;

                if (authoring.Config != null)
                {
                    biome = authoring.Config.BiomeType;
                    weatherOverride = authoring.Config.WeatherOverride;
                    priority = authoring.Config.Priority;
                    radius = authoring.Config.BlendRadius;
                }

                AddComponent(entity, new WeatherZone
                {
                    BiomeType = biome,
                    WeatherOverride = weatherOverride,
                    Priority = priority,
                    Radius = radius
                });

                AddComponent(entity, new WeatherZoneTag());
            }
        }
    }
}
