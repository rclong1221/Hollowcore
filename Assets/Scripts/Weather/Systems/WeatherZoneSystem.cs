using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DIG.Weather
{
    /// <summary>
    /// Client-only: checks local player overlap with WeatherZone volumes,
    /// writes LocalWeatherOverride on the local player entity.
    /// Highest-priority overlapping zone wins.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class WeatherZoneSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<WeatherState>();
        }

        protected override void OnUpdate()
        {
            // Find local player entities with LocalWeatherOverride
            foreach (var (localOverride, playerTransform) in
                SystemAPI.Query<RefRW<LocalWeatherOverride>, RefRO<LocalTransform>>())
            {
                float3 playerPos = playerTransform.ValueRO.Position;

                bool foundZone = false;
                byte bestPriority = 0;
                WeatherType bestWeather = WeatherType.Clear;
                float bestBlend = 0f;
                byte bestBiome = 0;

                foreach (var (zone, zoneTransform) in
                    SystemAPI.Query<RefRO<WeatherZone>, RefRO<LocalTransform>>()
                        .WithAll<WeatherZoneTag>())
                {
                    // Skip zones with no override
                    if ((byte)zone.ValueRO.WeatherOverride == 255) continue;

                    float distance = math.distance(playerPos, zoneTransform.ValueRO.Position);
                    float radius = zone.ValueRO.Radius;
                    if (radius <= 0f) radius = 50f;

                    if (distance > radius) continue;

                    byte priority = zone.ValueRO.Priority;
                    if (!foundZone || priority > bestPriority)
                    {
                        foundZone = true;
                        bestPriority = priority;
                        bestWeather = zone.ValueRO.WeatherOverride;
                        bestBiome = zone.ValueRO.BiomeType;

                        // Blend weight: 1.0 at center, 0.0 at edge
                        float blendRadius = radius * 0.2f; // 20% edge blend
                        float edgeDist = radius - distance;
                        bestBlend = math.saturate(edgeDist / math.max(blendRadius, 0.01f));
                    }
                }

                localOverride.ValueRW.HasOverride = foundZone;
                localOverride.ValueRW.OverrideWeather = bestWeather;
                localOverride.ValueRW.BlendWeight = bestBlend;
                localOverride.ValueRW.BiomeType = bestBiome;
            }
        }
    }
}
