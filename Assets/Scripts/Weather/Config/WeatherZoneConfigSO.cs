using UnityEngine;

namespace DIG.Weather
{
    [CreateAssetMenu(menuName = "DIG/Weather/Weather Zone Config")]
    public class WeatherZoneConfigSO : ScriptableObject
    {
        [Tooltip("Biome index for transition table lookup.")]
        public byte BiomeType = 0;

        [Tooltip("Forced weather type. Set to Sandstorm+1 (invalid) to follow global weather.")]
        public WeatherType WeatherOverride = (WeatherType)255;

        [Tooltip("Overlap resolution priority (higher wins).")]
        public byte Priority = 0;

        [Tooltip("Transition blend distance at zone edges (meters).")]
        public float BlendRadius = 20f;
    }
}
