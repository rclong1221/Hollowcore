using Unity.Entities;
using UnityEngine;

namespace DIG.Weather
{
    /// <summary>
    /// Place on the player prefab to receive weather gameplay modifiers.
    /// Adds WeatherVisionModifier and WeatherMovementModifier with neutral defaults.
    /// </summary>
    [AddComponentMenu("DIG/Weather/Weather Gameplay Modifier")]
    public class WeatherGameplayModifierAuthoring : MonoBehaviour
    {
        public class Baker : Baker<WeatherGameplayModifierAuthoring>
        {
            public override void Bake(WeatherGameplayModifierAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new WeatherVisionModifier { RangeMultiplier = 1.0f });
                AddComponent(entity, new WeatherMovementModifier { SpeedMultiplier = 1.0f });
                AddComponent(entity, new LocalWeatherOverride
                {
                    HasOverride = false,
                    OverrideWeather = WeatherType.Clear,
                    BlendWeight = 0f,
                    BiomeType = 0
                });
            }
        }
    }
}
