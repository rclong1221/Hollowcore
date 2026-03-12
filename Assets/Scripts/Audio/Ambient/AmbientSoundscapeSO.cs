using System;
using UnityEngine;

namespace Audio.Ambient
{
    [Serializable]
    public struct TimeOfDayBlend
    {
        [Tooltip("Volume multiplier during dawn (4:00-7:00).")]
        [Range(0f, 1f)] public float Dawn;

        [Tooltip("Volume multiplier during day (7:00-17:00).")]
        [Range(0f, 1f)] public float Day;

        [Tooltip("Volume multiplier during dusk (17:00-20:00).")]
        [Range(0f, 1f)] public float Dusk;

        [Tooltip("Volume multiplier during night (20:00-4:00).")]
        [Range(0f, 1f)] public float Night;

        public static TimeOfDayBlend AllDay => new TimeOfDayBlend { Dawn = 1f, Day = 1f, Dusk = 1f, Night = 1f };

        public float Evaluate(float hourOfDay)
        {
            if (hourOfDay < 4f) return Night;
            if (hourOfDay < 7f)
            {
                float t = (hourOfDay - 4f) / 3f;
                return Mathf.Lerp(Night, Dawn, t);
            }
            if (hourOfDay < 8f)
            {
                float t = hourOfDay - 7f;
                return Mathf.Lerp(Dawn, Day, t);
            }
            if (hourOfDay < 17f) return Day;
            if (hourOfDay < 18f)
            {
                float t = hourOfDay - 17f;
                return Mathf.Lerp(Day, Dusk, t);
            }
            if (hourOfDay < 20f)
            {
                float t = (hourOfDay - 18f) / 2f;
                return Mathf.Lerp(Dusk, Night, t);
            }
            return Night;
        }
    }

    [Serializable]
    public struct WeatherBlend
    {
        [Tooltip("Volume multiplier during clear weather.")]
        [Range(0f, 1f)] public float Clear;

        [Tooltip("Volume multiplier during rain.")]
        [Range(0f, 1f)] public float Rain;

        [Tooltip("Volume multiplier during storm.")]
        [Range(0f, 1f)] public float Storm;

        [Tooltip("Volume multiplier during snow.")]
        [Range(0f, 1f)] public float Snow;

        public static WeatherBlend AllWeather => new WeatherBlend { Clear = 1f, Rain = 1f, Storm = 1f, Snow = 1f };
    }

    [Serializable]
    public class AmbientLayer
    {
        [Tooltip("Editor label for this layer.")]
        public string LayerName;

        [Tooltip("Looping clips. One is selected randomly on zone enter.")]
        public AudioClip[] Clips = Array.Empty<AudioClip>();

        [Tooltip("Base volume for this layer.")]
        [Range(0f, 1f)]
        public float Volume = 0.5f;

        [Tooltip("Random volume modulation applied per-second (± this value).")]
        [Range(0f, 0.2f)]
        public float VolumeVariance;

        [Tooltip("Volume multiplier based on time of day.")]
        public TimeOfDayBlend DayBlend = TimeOfDayBlend.AllDay;

        [Tooltip("Volume multiplier based on weather.")]
        public WeatherBlend WeatherBlend = WeatherBlend.AllWeather;

        [Tooltip("True = positioned at zone center. False = 2D ambient background.")]
        public bool Is3D;
    }

    [CreateAssetMenu(fileName = "AmbientSoundscape", menuName = "DIG/Audio/Ambient Soundscape")]
    public class AmbientSoundscapeSO : ScriptableObject
    {
        [Tooltip("Unique identifier for this soundscape.")]
        public string SoundscapeId;

        [Tooltip("Ambient sound layers composing this soundscape.")]
        public AmbientLayer[] Layers = Array.Empty<AmbientLayer>();

        [Tooltip("Crossfade duration when transitioning to/from this soundscape.")]
        [Min(0.1f)]
        public float CrossfadeDuration = 3f;

        [Tooltip("Zone priority. Higher priority zones override lower when overlapping.")]
        public int Priority;
    }
}
