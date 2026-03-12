using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Weather
{
    /// <summary>
    /// Client-only: manages weather particle systems (rain, snow, sandstorm),
    /// lightning screen flash, and RenderSettings fog.
    /// Particle systems follow the camera and scale with weather intensity.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WeatherAudioSystem))]
    public partial class WeatherVFXSystem : SystemBase
    {
        private ParticleSystem _rainPS;
        private ParticleSystem _snowPS;
        private ParticleSystem _sandPS;

        private Camera _mainCamera;
        private Light _mainLight;
        private bool _sceneRefsCached;

        private float _lightningFlash;
        private float _lastLightningTimer;
        private float _baseLightIntensity;

        private const float MAX_RAIN_PARTICLES = 3000f;
        private const float MAX_SNOW_PARTICLES = 2000f;
        private const float MAX_SAND_PARTICLES = 1500f;
        private const float MAX_FOG_DENSITY = 0.08f;

        protected override void OnCreate()
        {
            RequireForUpdate<WeatherState>();
        }

        protected override void OnUpdate()
        {
            if (!_sceneRefsCached)
            {
                _mainCamera = Camera.main;
                _mainLight = Object.FindAnyObjectByType<Light>();
                _sceneRefsCached = true;
            }
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            var ws = SystemAPI.GetSingleton<WeatherState>();
            float dt = SystemAPI.Time.DeltaTime;
            Vector3 camPos = _mainCamera.transform.position;

            // --- Rain ---
            UpdateParticleSystem(ref _rainPS, "WeatherRain", camPos, ws.RainIntensity,
                MAX_RAIN_PARTICLES, ws.WindDirectionX, ws.WindDirectionY, ws.WindSpeed, fallSpeed: 8f);

            // --- Snow ---
            UpdateParticleSystem(ref _snowPS, "WeatherSnow", camPos, ws.SnowIntensity,
                MAX_SNOW_PARTICLES, ws.WindDirectionX, ws.WindDirectionY, ws.WindSpeed * 0.3f, fallSpeed: 2f);

            // --- Sandstorm ---
            float sandIntensity = ws.CurrentWeather == WeatherType.Sandstorm
                ? math.saturate(ws.TransitionProgress)
                : 0f;
            UpdateParticleSystem(ref _sandPS, "WeatherSand", camPos, sandIntensity,
                MAX_SAND_PARTICLES, ws.WindDirectionX, ws.WindDirectionY, ws.WindSpeed, fallSpeed: 0.5f);

            // --- Lightning flash ---
            if (ws.LightningTimer < _lastLightningTimer - 1f && ws.CurrentWeather == WeatherType.Thunderstorm)
            {
                _lightningFlash = 1.0f;
                if (_mainLight != null)
                    _baseLightIntensity = _mainLight.intensity;
            }
            _lastLightningTimer = ws.LightningTimer;

            if (_lightningFlash > 0f)
            {
                _lightningFlash -= dt * 10f; // 0.1 second flash
                if (_mainLight != null)
                    _mainLight.intensity = _baseLightIntensity + math.max(_lightningFlash, 0f) * 3f;
            }

            // --- Fog ---
            bool fogEnabled = ws.FogDensity > 0.01f;
            RenderSettings.fog = fogEnabled;
            if (fogEnabled)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = ws.FogDensity * MAX_FOG_DENSITY;
                var ambientColor = RenderSettings.ambientLight;
                RenderSettings.fogColor = Color.Lerp(ambientColor, Color.gray, 0.5f);
            }
        }

        private void UpdateParticleSystem(ref ParticleSystem ps, string name, Vector3 camPos,
            float intensity, float maxParticles, float windX, float windY, float windSpeed, float fallSpeed)
        {
            if (intensity < 0.01f)
            {
                if (ps != null && ps.isPlaying)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            if (ps == null)
                ps = CreateParticleSystem(name);

            // Position at camera
            ps.transform.position = camPos + Vector3.up * 15f;

            // Emission rate
            var emission = ps.emission;
            emission.rateOverTime = intensity * maxParticles;

            if (!ps.isPlaying)
                ps.Play();

            // Wind influence
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = windX * windSpeed * 0.5f;
            vel.y = -fallSpeed;
            vel.z = windY * windSpeed * 0.5f;
        }

        private static ParticleSystem CreateParticleSystem(string name)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 5000;
            main.startLifetime = 3f;
            main.startSize = 0.05f;
            main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.6f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 0.1f, 30f);

            var emission = ps.emission;
            emission.rateOverTime = 0;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            return ps;
        }

        protected override void OnDestroy()
        {
            DestroyPS(ref _rainPS);
            DestroyPS(ref _snowPS);
            DestroyPS(ref _sandPS);
        }

        private static void DestroyPS(ref ParticleSystem ps)
        {
            if (ps != null)
            {
                Object.Destroy(ps.gameObject);
                ps = null;
            }
        }
    }
}
