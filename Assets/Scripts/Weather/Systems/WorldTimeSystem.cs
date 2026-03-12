using Unity.Burst;
using Unity.Entities;

namespace DIG.Weather
{
    /// <summary>
    /// Ticks the world clock: advances TimeOfDay, DayCount, and Season.
    /// Server-authoritative; clients receive via ghost replication.
    /// Burst-compiled: reads config from unmanaged WeatherConfigRefs singleton.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WeatherTransitionSystem))]
    public partial struct WorldTimeSystem : ISystem
    {
        private bool _cachedConfig;
        private float _dayLengthSeconds;
        private int _seasonLengthDays;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldTimeState>();
            state.RequireForUpdate<WeatherConfigRefs>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_cachedConfig)
            {
                var refs = SystemAPI.GetSingleton<WeatherConfigRefs>();
                if (!refs.DayNightConfig.IsCreated) return;
                ref var cfg = ref refs.DayNightConfig.Value;
                _dayLengthSeconds = cfg.DayLengthSeconds;
                _seasonLengthDays = cfg.SeasonLengthDays;
                _cachedConfig = true;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;
            var timeState = SystemAPI.GetSingletonRW<WorldTimeState>();
            ref var ts = ref timeState.ValueRW;

            if (ts.IsPaused) return;

            ts.TimeOfDay += (deltaTime / _dayLengthSeconds) * 24.0f * ts.TimeScale;

            if (ts.TimeOfDay >= 24.0f)
            {
                ts.TimeOfDay -= 24.0f;
                ts.DayCount++;

                if (_seasonLengthDays > 0 && ts.DayCount % _seasonLengthDays == 0)
                {
                    ts.Season = (Season)(((int)ts.Season + 1) % 4);
                }
            }
        }
    }
}
