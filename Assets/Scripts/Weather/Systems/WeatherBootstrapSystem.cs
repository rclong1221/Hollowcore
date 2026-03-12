using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Weather
{
    /// <summary>
    /// Loads DayNightConfigSO and WeatherConfigSO from Resources,
    /// builds BlobAssets, creates WorldTimeState + WeatherState singletons.
    /// Follows the SurfaceGameplayConfigSystem bootstrap pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                        WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class WeatherBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var dayNightConfig = Resources.Load<DayNightConfigSO>("DayNightConfig");
            var weatherConfig = Resources.Load<WeatherConfigSO>("WeatherConfig");

            BuildDayNightBlob(dayNightConfig);
            BuildWeatherBlobs(weatherConfig);
            CreateSingletons(dayNightConfig, weatherConfig);

            _initialized = true;
            Enabled = false;
        }

        private void BuildDayNightBlob(DayNightConfigSO config)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<DayNightBlob>();

            if (config != null)
            {
                root.DayLengthSeconds = config.DayLengthSeconds;
                root.SeasonLengthDays = config.SeasonLengthDays;
                root.SunriseHour = config.SunriseHour;
                root.SunsetHour = config.SunsetHour;
                root.SunPitchMax = config.SunPitchMax;
                root.MoonPitchMax = config.MoonPitchMax;
                root.NightAmbientIntensity = config.NightAmbientIntensity;
                root.DayAmbientIntensity = config.DayAmbientIntensity;
            }
            else
            {
                root.DayLengthSeconds = 1200f;
                root.SeasonLengthDays = 7;
                root.SunriseHour = 6f;
                root.SunsetHour = 18f;
                root.SunPitchMax = 75f;
                root.MoonPitchMax = 40f;
                root.NightAmbientIntensity = 0.05f;
                root.DayAmbientIntensity = 1.0f;
            }

            // Sun color gradient: 8 keyframes sampled from SO or defaults
            var sunColors = builder.Allocate(ref root.SunColorGradient, 8);
            if (config != null && config.SunColorGradient != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    float t = i / 7f;
                    var c = config.SunColorGradient.Evaluate(t);
                    sunColors[i] = new ColorKeyframe { Time = t, R = c.r, G = c.g, B = c.b };
                }
            }
            else
            {
                SetDefaultSunGradient(ref sunColors);
            }

            // Ambient color gradient: 8 keyframes
            var ambientColors = builder.Allocate(ref root.AmbientColorGradient, 8);
            if (config != null && config.AmbientColorGradient != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    float t = i / 7f;
                    var c = config.AmbientColorGradient.Evaluate(t);
                    ambientColors[i] = new ColorKeyframe { Time = t, R = c.r, G = c.g, B = c.b };
                }
            }
            else
            {
                SetDefaultAmbientGradient(ref ambientColors);
            }

            // Sun intensity curve: 24 samples (one per hour)
            var intensity = builder.Allocate(ref root.SunIntensityCurve, 24);
            if (config != null && config.SunIntensityCurve != null)
            {
                for (int i = 0; i < 24; i++)
                    intensity[i] = config.SunIntensityCurve.Evaluate(i / 24f);
            }
            else
            {
                for (int i = 0; i < 24; i++)
                {
                    float t = i / 24f;
                    float sunFactor = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI));
                    intensity[i] = sunFactor;
                }
            }

            var blobRef = builder.CreateBlobAssetReference<DayNightBlob>(Allocator.Persistent);

            var mgr = GetOrCreateManager();
            mgr.DayNightConfig = blobRef;
        }

        private void BuildWeatherBlobs(WeatherConfigSO config)
        {
            const int weatherCount = 10;
            const int seasonCount = 4;

            // --- Transition weights blob ---
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<WeatherTransitionBlob>();
                root.WeatherTypeCount = weatherCount;
                root.SeasonCount = seasonCount;

                int totalWeights = weatherCount * seasonCount * weatherCount; // 400
                var weights = builder.Allocate(ref root.TransitionWeights, totalWeights);

                // Initialize all to 0
                for (int i = 0; i < totalWeights; i++)
                    weights[i] = 0f;

                if (config != null)
                {
                    foreach (var entry in config.TransitionProbabilities)
                    {
                        if (entry.Weights == null || entry.Weights.Length < weatherCount) continue;
                        int baseIdx = ((int)entry.FromWeather * seasonCount + (int)entry.Season) * weatherCount;
                        for (int w = 0; w < weatherCount; w++)
                            weights[baseIdx + w] = entry.Weights[w];
                    }
                }

                // Fill any rows that are all-zero with a default (stay in same weather 0.5, transition to Clear 0.5)
                for (int from = 0; from < weatherCount; from++)
                {
                    for (int season = 0; season < seasonCount; season++)
                    {
                        int baseIdx = (from * seasonCount + season) * weatherCount;
                        float sum = 0;
                        for (int w = 0; w < weatherCount; w++)
                            sum += weights[baseIdx + w];

                        if (sum < 0.001f)
                        {
                            weights[baseIdx + from] = 0.5f;
                            weights[baseIdx + 0] = 0.5f; // Clear
                        }
                    }
                }

                var blobRef = builder.CreateBlobAssetReference<WeatherTransitionBlob>(Allocator.Persistent);
                var mgr = GetOrCreateManager();
                mgr.TransitionTable = blobRef;
            }

            // --- Weather params blob ---
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<WeatherParamsBlob>();

                var targets = builder.Allocate(ref root.TargetParams, weatherCount);
                var gameplay = builder.Allocate(ref root.GameplayParams, weatherCount);

                // Defaults
                for (int i = 0; i < weatherCount; i++)
                {
                    targets[i] = new WeatherTargetParams
                    {
                        TransitionDurationMin = 30f,
                        TransitionDurationMax = 90f,
                        MinDuration = 120f,
                        MaxDuration = 480f
                    };
                    gameplay[i] = new WeatherGameplayParams
                    {
                        VisionRangeMultiplier = 1f,
                        MovementSpeedMultiplier = 1f,
                        SurfaceFrictionMultiplier = 1f,
                        NoiseMultiplier = 1f,
                        ProjectileSpeedMultiplier = 1f
                    };
                }

                // Hardcoded defaults per weather type
                SetDefaultWeatherTargets(ref targets);
                SetDefaultGameplayParams(ref gameplay);

                // Override from config
                if (config != null)
                {
                    foreach (var e in config.WeatherTargetParams)
                    {
                        int idx = (int)e.Weather;
                        if (idx < 0 || idx >= weatherCount) continue;
                        targets[idx] = new WeatherTargetParams
                        {
                            TargetRainIntensity = e.RainIntensity,
                            TargetSnowIntensity = e.SnowIntensity,
                            TargetFogDensity = e.FogDensity,
                            TargetWindSpeed = e.WindSpeed,
                            TargetTemperatureOffset = e.TemperatureOffset,
                            TransitionDurationMin = e.TransitionDurationMin,
                            TransitionDurationMax = e.TransitionDurationMax,
                            MinDuration = e.MinDuration,
                            MaxDuration = e.MaxDuration,
                            LightningIntervalMin = e.LightningIntervalMin,
                            LightningIntervalMax = e.LightningIntervalMax
                        };
                    }
                    foreach (var e in config.GameplayModifiers)
                    {
                        int idx = (int)e.Weather;
                        if (idx < 0 || idx >= weatherCount) continue;
                        gameplay[idx] = new WeatherGameplayParams
                        {
                            VisionRangeMultiplier = e.VisionRangeMultiplier,
                            MovementSpeedMultiplier = e.MovementSpeedMultiplier,
                            SurfaceFrictionMultiplier = e.SurfaceFrictionMultiplier,
                            NoiseMultiplier = e.NoiseMultiplier,
                            ProjectileSpeedMultiplier = e.ProjectileSpeedMultiplier
                        };
                    }
                }

                var blobRef = builder.CreateBlobAssetReference<WeatherParamsBlob>(Allocator.Persistent);
                var mgr = GetOrCreateManager();
                mgr.WeatherParams = blobRef;
            }
        }

        private void CreateSingletons(DayNightConfigSO dayNight, WeatherConfigSO weather)
        {
            float startTime = dayNight != null ? dayNight.StartTimeOfDay : 8.0f;
            var defaultWeather = weather != null ? weather.DefaultWeather : WeatherType.Clear;
            var defaultSeason = weather != null ? weather.DefaultSeason : Season.Summer;
            uint seed = weather != null ? weather.RandomSeed : 0;
            if (seed == 0)
                seed = (uint)System.Environment.TickCount;

            // WorldTimeState singleton
            var timeEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(timeEntity, new WorldTimeState
            {
                TimeOfDay = startTime,
                DayCount = 0,
                Season = defaultSeason,
                TimeScale = 1.0f,
                IsPaused = false
            });

            // WeatherState singleton
            var weatherEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(weatherEntity, new WeatherState
            {
                CurrentWeather = defaultWeather,
                NextWeather = defaultWeather,
                TransitionProgress = 1.0f,
                WindDirectionX = 1f,
                WindDirectionY = 0f,
                WindSpeed = 0f,
                RainIntensity = 0f,
                SnowIntensity = 0f,
                FogDensity = 0f,
                LightningTimer = 0f,
                Temperature = weather != null && weather.BaseTemperature.Length > (int)defaultSeason
                    ? weather.BaseTemperature[(int)defaultSeason]
                    : 20f
            });

            // WeatherWetness singleton
            var wetnessEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(wetnessEntity, new WeatherWetness { Value = 0f });

            // Finalize managed singleton
            var mgr = GetOrCreateManager();
            mgr.RandomSeed = seed;
            mgr.TimeSinceLastTransition = 0f;
            mgr.NextTransitionInterval = weather != null
                ? Random.Range(weather.WeatherChangeIntervalMin, weather.WeatherChangeIntervalMax)
                : 300f;
            mgr.CurrentTransitionDuration = 60f;
            mgr.IsInitialized = true;

            // Unmanaged BlobRef singleton (enables Burst-compiled systems)
            var refsEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(refsEntity, new WeatherConfigRefs
            {
                TransitionTable = mgr.TransitionTable,
                DayNightConfig = mgr.DayNightConfig,
                WeatherParams = mgr.WeatherParams
            });

            // Unmanaged transition runtime state (decoupled from managed singleton)
            var rng = Unity.Mathematics.Random.CreateFromIndex(seed);
            var transEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(transEntity, new WeatherTransitionRuntimeState
            {
                RandomState = rng.state,
                TimeSinceLastTransition = 0f,
                NextTransitionInterval = mgr.NextTransitionInterval,
                CurrentTransitionDuration = mgr.CurrentTransitionDuration
            });
        }

        private WeatherManagerSingleton GetOrCreateManager()
        {
            if (SystemAPI.ManagedAPI.TryGetSingleton<WeatherManagerSingleton>(out var existing))
                return existing;

            var entity = EntityManager.CreateEntity();
            var mgr = new WeatherManagerSingleton();
            EntityManager.AddComponentData(entity, mgr);
            return mgr;
        }

        private static void SetDefaultSunGradient(ref BlobBuilderArray<ColorKeyframe> kf)
        {
            kf[0] = new ColorKeyframe { Time = 0.000f, R = 0.2f, G = 0.2f, B = 0.4f };
            kf[1] = new ColorKeyframe { Time = 0.143f, R = 0.3f, G = 0.25f, B = 0.4f };
            kf[2] = new ColorKeyframe { Time = 0.286f, R = 1.0f, G = 0.5f, B = 0.2f };
            kf[3] = new ColorKeyframe { Time = 0.429f, R = 1.0f, G = 0.9f, B = 0.7f };
            kf[4] = new ColorKeyframe { Time = 0.571f, R = 1.0f, G = 0.95f, B = 0.85f };
            kf[5] = new ColorKeyframe { Time = 0.714f, R = 1.0f, G = 0.6f, B = 0.3f };
            kf[6] = new ColorKeyframe { Time = 0.857f, R = 0.4f, G = 0.3f, B = 0.4f };
            kf[7] = new ColorKeyframe { Time = 1.000f, R = 0.2f, G = 0.2f, B = 0.4f };
        }

        private static void SetDefaultAmbientGradient(ref BlobBuilderArray<ColorKeyframe> kf)
        {
            kf[0] = new ColorKeyframe { Time = 0.000f, R = 0.05f, G = 0.05f, B = 0.15f };
            kf[1] = new ColorKeyframe { Time = 0.143f, R = 0.1f, G = 0.1f, B = 0.2f };
            kf[2] = new ColorKeyframe { Time = 0.286f, R = 0.6f, G = 0.5f, B = 0.4f };
            kf[3] = new ColorKeyframe { Time = 0.429f, R = 0.8f, G = 0.75f, B = 0.7f };
            kf[4] = new ColorKeyframe { Time = 0.571f, R = 0.8f, G = 0.85f, B = 0.9f };
            kf[5] = new ColorKeyframe { Time = 0.714f, R = 0.7f, G = 0.4f, B = 0.2f };
            kf[6] = new ColorKeyframe { Time = 0.857f, R = 0.15f, G = 0.1f, B = 0.2f };
            kf[7] = new ColorKeyframe { Time = 1.000f, R = 0.05f, G = 0.05f, B = 0.15f };
        }

        private static void SetDefaultWeatherTargets(ref BlobBuilderArray<WeatherTargetParams> t)
        {
            // Clear
            t[0] = new WeatherTargetParams { TargetWindSpeed = 2f, TransitionDurationMin = 30, TransitionDurationMax = 60, MinDuration = 180, MaxDuration = 600 };
            // PartlyCloudy
            t[1] = new WeatherTargetParams { TargetFogDensity = 0.05f, TargetWindSpeed = 5f, TransitionDurationMin = 30, TransitionDurationMax = 90, MinDuration = 120, MaxDuration = 480 };
            // Cloudy
            t[2] = new WeatherTargetParams { TargetFogDensity = 0.1f, TargetWindSpeed = 6f, TargetTemperatureOffset = -2, TransitionDurationMin = 30, TransitionDurationMax = 90, MinDuration = 120, MaxDuration = 480 };
            // LightRain
            t[3] = new WeatherTargetParams { TargetRainIntensity = 0.4f, TargetFogDensity = 0.1f, TargetWindSpeed = 7f, TargetTemperatureOffset = -3, TransitionDurationMin = 30, TransitionDurationMax = 90, MinDuration = 120, MaxDuration = 480 };
            // HeavyRain
            t[4] = new WeatherTargetParams { TargetRainIntensity = 0.9f, TargetFogDensity = 0.2f, TargetWindSpeed = 12f, TargetTemperatureOffset = -3, TransitionDurationMin = 30, TransitionDurationMax = 90, MinDuration = 120, MaxDuration = 480 };
            // Thunderstorm
            t[5] = new WeatherTargetParams { TargetRainIntensity = 1.0f, TargetFogDensity = 0.3f, TargetWindSpeed = 18f, TargetTemperatureOffset = -5, TransitionDurationMin = 20, TransitionDurationMax = 60, MinDuration = 90, MaxDuration = 360, LightningIntervalMin = 5, LightningIntervalMax = 20 };
            // LightSnow
            t[6] = new WeatherTargetParams { TargetSnowIntensity = 0.4f, TargetFogDensity = 0.1f, TargetWindSpeed = 5f, TargetTemperatureOffset = -8, TransitionDurationMin = 40, TransitionDurationMax = 120, MinDuration = 120, MaxDuration = 600 };
            // HeavySnow
            t[7] = new WeatherTargetParams { TargetSnowIntensity = 0.9f, TargetFogDensity = 0.3f, TargetWindSpeed = 10f, TargetTemperatureOffset = -12, TransitionDurationMin = 40, TransitionDurationMax = 120, MinDuration = 120, MaxDuration = 600 };
            // Fog
            t[8] = new WeatherTargetParams { TargetFogDensity = 0.8f, TargetWindSpeed = 2f, TargetTemperatureOffset = -2, TransitionDurationMin = 60, TransitionDurationMax = 180, MinDuration = 180, MaxDuration = 600 };
            // Sandstorm
            t[9] = new WeatherTargetParams { TargetFogDensity = 0.6f, TargetWindSpeed = 25f, TargetTemperatureOffset = 5, TransitionDurationMin = 30, TransitionDurationMax = 90, MinDuration = 90, MaxDuration = 360 };
        }

        private static void SetDefaultGameplayParams(ref BlobBuilderArray<WeatherGameplayParams> g)
        {
            // Clear -- all 1.0
            g[0] = new WeatherGameplayParams { VisionRangeMultiplier = 1f, MovementSpeedMultiplier = 1f, SurfaceFrictionMultiplier = 1f, NoiseMultiplier = 1f, ProjectileSpeedMultiplier = 1f };
            g[1] = new WeatherGameplayParams { VisionRangeMultiplier = 0.95f, MovementSpeedMultiplier = 1f, SurfaceFrictionMultiplier = 1f, NoiseMultiplier = 1f, ProjectileSpeedMultiplier = 1f };
            g[2] = new WeatherGameplayParams { VisionRangeMultiplier = 0.9f, MovementSpeedMultiplier = 1f, SurfaceFrictionMultiplier = 1f, NoiseMultiplier = 1f, ProjectileSpeedMultiplier = 1f };
            // LightRain
            g[3] = new WeatherGameplayParams { VisionRangeMultiplier = 0.8f, MovementSpeedMultiplier = 0.95f, SurfaceFrictionMultiplier = 0.85f, NoiseMultiplier = 0.8f, ProjectileSpeedMultiplier = 0.98f };
            // HeavyRain
            g[4] = new WeatherGameplayParams { VisionRangeMultiplier = 0.6f, MovementSpeedMultiplier = 0.9f, SurfaceFrictionMultiplier = 0.7f, NoiseMultiplier = 0.6f, ProjectileSpeedMultiplier = 0.95f };
            // Thunderstorm
            g[5] = new WeatherGameplayParams { VisionRangeMultiplier = 0.5f, MovementSpeedMultiplier = 0.85f, SurfaceFrictionMultiplier = 0.65f, NoiseMultiplier = 0.4f, ProjectileSpeedMultiplier = 0.9f };
            // LightSnow
            g[6] = new WeatherGameplayParams { VisionRangeMultiplier = 0.8f, MovementSpeedMultiplier = 0.85f, SurfaceFrictionMultiplier = 0.8f, NoiseMultiplier = 0.7f, ProjectileSpeedMultiplier = 0.95f };
            // HeavySnow
            g[7] = new WeatherGameplayParams { VisionRangeMultiplier = 0.5f, MovementSpeedMultiplier = 0.7f, SurfaceFrictionMultiplier = 0.6f, NoiseMultiplier = 0.5f, ProjectileSpeedMultiplier = 0.9f };
            // Fog
            g[8] = new WeatherGameplayParams { VisionRangeMultiplier = 0.3f, MovementSpeedMultiplier = 0.95f, SurfaceFrictionMultiplier = 1f, NoiseMultiplier = 0.8f, ProjectileSpeedMultiplier = 1f };
            // Sandstorm
            g[9] = new WeatherGameplayParams { VisionRangeMultiplier = 0.4f, MovementSpeedMultiplier = 0.8f, SurfaceFrictionMultiplier = 0.9f, NoiseMultiplier = 0.3f, ProjectileSpeedMultiplier = 0.85f };
        }

        protected override void OnDestroy()
        {
            if (SystemAPI.ManagedAPI.TryGetSingleton<WeatherManagerSingleton>(out var mgr))
            {
                if (mgr.TransitionTable.IsCreated) mgr.TransitionTable.Dispose();
                if (mgr.DayNightConfig.IsCreated) mgr.DayNightConfig.Dispose();
                if (mgr.WeatherParams.IsCreated) mgr.WeatherParams.Dispose();
            }
        }
    }
}
