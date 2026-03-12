using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Weather
{
    /// <summary>
    /// Reads WeatherState and writes gameplay modifiers:
    /// - WeatherVisionModifier on entities that have it
    /// - WeatherMovementModifier on entities that have it
    /// - WeatherWetness singleton for SurfaceSlipSystem
    /// Burst-compiled: reads from unmanaged WeatherConfigRefs singleton.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeatherTransitionSystem))]
    public partial struct WeatherGameplaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeatherState>();
            state.RequireForUpdate<WeatherConfigRefs>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var refs = SystemAPI.GetSingleton<WeatherConfigRefs>();
            if (!refs.WeatherParams.IsCreated) return;

            var ws = SystemAPI.GetSingleton<WeatherState>();
            ref var paramsBlob = ref refs.WeatherParams.Value;

            int curIdx = (int)ws.CurrentWeather;
            int nxtIdx = (int)ws.NextWeather;
            float t = ws.TransitionProgress;

            ref var curGP = ref paramsBlob.GameplayParams[curIdx];
            ref var nxtGP = ref paramsBlob.GameplayParams[nxtIdx];

            float visionMult = math.lerp(curGP.VisionRangeMultiplier, nxtGP.VisionRangeMultiplier, t);
            float speedMult = math.lerp(curGP.MovementSpeedMultiplier, nxtGP.MovementSpeedMultiplier, t);

            foreach (var visionMod in SystemAPI.Query<RefRW<WeatherVisionModifier>>())
            {
                visionMod.ValueRW.RangeMultiplier = visionMult;
            }

            foreach (var moveMod in SystemAPI.Query<RefRW<WeatherMovementModifier>>())
            {
                moveMod.ValueRW.SpeedMultiplier = speedMult;
            }

            if (SystemAPI.TryGetSingletonRW<WeatherWetness>(out var wetness))
            {
                wetness.ValueRW.Value = math.max(ws.RainIntensity * 0.8f, ws.SnowIntensity * 0.3f);
            }
        }
    }
}
