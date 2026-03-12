using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Weather
{
    /// <summary>
    /// Weather state machine: lerps intensity params, picks weighted random
    /// transitions per season, manages lightning timer.
    /// Burst-compiled: all state lives in unmanaged singletons.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorldTimeSystem))]
    public partial struct WeatherTransitionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeatherState>();
            state.RequireForUpdate<WorldTimeState>();
            state.RequireForUpdate<WeatherConfigRefs>();
            state.RequireForUpdate<WeatherTransitionRuntimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var refs = SystemAPI.GetSingleton<WeatherConfigRefs>();
            if (!refs.TransitionTable.IsCreated || !refs.WeatherParams.IsCreated) return;

            var runtimeRW = SystemAPI.GetSingletonRW<WeatherTransitionRuntimeState>();
            ref var rt = ref runtimeRW.ValueRW;

            var rng = new Random(rt.RandomState);
            if (rng.state == 0) rng = Random.CreateFromIndex(1);

            float dt = SystemAPI.Time.DeltaTime;
            var timeState = SystemAPI.GetSingleton<WorldTimeState>();
            if (timeState.IsPaused) { rt.RandomState = rng.state; return; }

            var ws = SystemAPI.GetSingletonRW<WeatherState>();
            ref var w = ref ws.ValueRW;

            ref var transBlob = ref refs.TransitionTable.Value;
            ref var paramsBlob = ref refs.WeatherParams.Value;

            int currentIdx = (int)w.CurrentWeather;
            int nextIdx = (int)w.NextWeather;

            // --- Lerp intensities toward NextWeather targets ---
            if (w.TransitionProgress < 1.0f)
            {
                w.TransitionProgress += dt / math.max(rt.CurrentTransitionDuration, 0.1f);
                w.TransitionProgress = math.min(w.TransitionProgress, 1.0f);

                ref var curTarget = ref paramsBlob.TargetParams[currentIdx];
                ref var nxtTarget = ref paramsBlob.TargetParams[nextIdx];
                float t = w.TransitionProgress;

                w.RainIntensity = math.lerp(curTarget.TargetRainIntensity, nxtTarget.TargetRainIntensity, t);
                w.SnowIntensity = math.lerp(curTarget.TargetSnowIntensity, nxtTarget.TargetSnowIntensity, t);
                w.FogDensity = math.lerp(curTarget.TargetFogDensity, nxtTarget.TargetFogDensity, t);
                w.WindSpeed = math.lerp(curTarget.TargetWindSpeed, nxtTarget.TargetWindSpeed, t);

                float seasonTemp = GetSeasonBaseTemp(timeState.Season);
                w.Temperature = math.lerp(
                    seasonTemp + curTarget.TargetTemperatureOffset,
                    seasonTemp + nxtTarget.TargetTemperatureOffset, t);

                if (w.TransitionProgress >= 1.0f)
                {
                    w.CurrentWeather = w.NextWeather;
                    currentIdx = nextIdx;
                }
            }

            // --- Lightning timer (Thunderstorm only) ---
            if (w.CurrentWeather == WeatherType.Thunderstorm)
            {
                ref var thunderParams = ref paramsBlob.TargetParams[(int)WeatherType.Thunderstorm];
                if (w.LightningTimer > 0f)
                {
                    w.LightningTimer -= dt;
                }
                else if (thunderParams.LightningIntervalMax > 0f)
                {
                    w.LightningTimer = rng.NextFloat(
                        thunderParams.LightningIntervalMin,
                        thunderParams.LightningIntervalMax);
                }
            }
            else
            {
                w.LightningTimer = 0f;
            }

            // --- Check if it's time for a new transition ---
            rt.TimeSinceLastTransition += dt;
            if (rt.TimeSinceLastTransition >= rt.NextTransitionInterval && w.TransitionProgress >= 1.0f)
            {
                w.NextWeather = PickNextWeather(ref transBlob, currentIdx, (int)timeState.Season, ref rng);
                nextIdx = (int)w.NextWeather;

                ref var nxtTarget = ref paramsBlob.TargetParams[nextIdx];
                rt.CurrentTransitionDuration = rng.NextFloat(
                    nxtTarget.TransitionDurationMin,
                    math.max(nxtTarget.TransitionDurationMin, nxtTarget.TransitionDurationMax));

                w.TransitionProgress = 0.0f;
                rt.TimeSinceLastTransition = 0f;
                rt.NextTransitionInterval = rng.NextFloat(
                    nxtTarget.MinDuration,
                    math.max(nxtTarget.MinDuration, nxtTarget.MaxDuration));

                float angle = rng.NextFloat(0f, math.PI * 2f);
                w.WindDirectionX = math.cos(angle);
                w.WindDirectionY = math.sin(angle);
            }

            rt.RandomState = rng.state;
        }

        private static WeatherType PickNextWeather(
            ref WeatherTransitionBlob blob, int fromWeather, int season, ref Random rng)
        {
            int weatherCount = blob.WeatherTypeCount;
            int seasonCount = blob.SeasonCount;
            int baseIdx = (fromWeather * seasonCount + season) * weatherCount;

            float totalWeight = 0f;
            for (int i = 0; i < weatherCount; i++)
                totalWeight += blob.TransitionWeights[baseIdx + i];

            if (totalWeight <= 0f)
                return (WeatherType)fromWeather;

            float roll = rng.NextFloat(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < weatherCount; i++)
            {
                cumulative += blob.TransitionWeights[baseIdx + i];
                if (roll <= cumulative)
                    return (WeatherType)i;
            }

            return (WeatherType)(weatherCount - 1);
        }

        private static float GetSeasonBaseTemp(Season season)
        {
            return season switch
            {
                Season.Spring => 15f,
                Season.Summer => 25f,
                Season.Autumn => 12f,
                Season.Winter => -2f,
                _ => 20f
            };
        }
    }
}
