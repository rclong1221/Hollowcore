using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

namespace DIG.Weather
{
    /// <summary>
    /// Client-only: drives ambient weather audio via AudioMixer exposed parameters.
    /// Smoothly crossfades rain/wind/thunder/night ambient volumes.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WeatherShaderSystem))]
    public partial class WeatherAudioSystem : SystemBase
    {
        private AudioMixer _mixer;
        private bool _mixerSearched;
        private float _lastLightningTimer;

        private float _currentRainDb;
        private float _currentWindDb;
        private float _currentThunderDb;
        private float _currentNightDb;

        private const float CROSSFADE_SPEED = 2f; // dB per second smoothing rate
        private const float MIN_DB = -80f;
        private const float MAX_DB = 0f;

        protected override void OnCreate()
        {
            RequireForUpdate<WorldTimeState>();
            RequireForUpdate<WeatherState>();

            _currentRainDb = MIN_DB;
            _currentWindDb = MIN_DB;
            _currentThunderDb = MIN_DB;
            _currentNightDb = MIN_DB;
        }

        protected override void OnUpdate()
        {
            if (!_mixerSearched)
            {
                // Find the AudioMixer from any AudioSource's output group in the scene
                var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                foreach (var src in sources)
                {
                    if (src.outputAudioMixerGroup != null)
                    {
                        _mixer = src.outputAudioMixerGroup.audioMixer;
                        break;
                    }
                }
                _mixerSearched = true;

                if (_mixer == null)
                {
                    Debug.LogWarning("[WeatherAudioSystem] No AudioMixer found. Weather audio disabled.");
                    Enabled = false;
                    return;
                }
            }

            if (_mixer == null) return;

            float dt = SystemAPI.Time.DeltaTime;
            var ws = SystemAPI.GetSingleton<WeatherState>();
            var timeState = SystemAPI.GetSingleton<WorldTimeState>();

            // Target volumes (linear 0-1 -> dB)
            float targetRainDb = IntensityToDb(ws.RainIntensity);
            float targetWindDb = IntensityToDb(ws.WindSpeed / 30f); // Normalize wind to 0-1
            float sunFactor = math.saturate(math.sin((timeState.TimeOfDay / 24f) * math.PI));
            float targetNightDb = IntensityToDb(1f - sunFactor);

            // Thunder spike on lightning event
            float targetThunderDb = _currentThunderDb;
            if (ws.LightningTimer < _lastLightningTimer - 1f && ws.CurrentWeather == WeatherType.Thunderstorm)
            {
                targetThunderDb = MAX_DB;
            }
            else
            {
                targetThunderDb = math.max(_currentThunderDb - dt * 20f, MIN_DB); // Fast decay
            }
            _lastLightningTimer = ws.LightningTimer;

            // Smooth crossfade
            float lerpRate = math.saturate(CROSSFADE_SPEED * dt);
            _currentRainDb = math.lerp(_currentRainDb, targetRainDb, lerpRate);
            _currentWindDb = math.lerp(_currentWindDb, targetWindDb, lerpRate);
            _currentThunderDb = math.lerp(_currentThunderDb, targetThunderDb, lerpRate);
            _currentNightDb = math.lerp(_currentNightDb, targetNightDb, lerpRate);

            _mixer.SetFloat("RainVolume", _currentRainDb);
            _mixer.SetFloat("WindVolume", _currentWindDb);
            _mixer.SetFloat("ThunderVolume", _currentThunderDb);
            _mixer.SetFloat("AmbientNightVolume", _currentNightDb);
        }

        private static float IntensityToDb(float intensity)
        {
            intensity = math.saturate(intensity);
            if (intensity < 0.01f) return MIN_DB;
            return 20f * math.log10(intensity);
        }
    }
}
