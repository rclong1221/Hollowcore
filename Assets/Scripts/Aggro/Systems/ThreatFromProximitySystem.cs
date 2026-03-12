using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;
using DIG.Vision.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Generates threat when Detectable entities are within
    /// ProximityThreatRadius of an AI. This is a "body pull" — 360-degree,
    /// no LOS required, designed for close-range stealth breaks.
    ///
    /// Stealth multiplier on the Detectable entity reduces the effective radius.
    /// Only active when ProximityThreatRadius > 0 on the AI's AggroConfig.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Vision.Systems.DetectionSystem))]
    [BurstCompile]
    public partial struct ThreatFromProximitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggroConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Collect all detectable entities into a temp array for O(N*M) check
            var detectables = new NativeList<DetectableData>(32, Allocator.Temp);
            foreach (var (detectable, transform, entity) in
                SystemAPI.Query<RefRO<Detectable>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                detectables.Add(new DetectableData
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    StealthMultiplier = detectable.ValueRO.StealthMultiplier
                });
            }

            if (detectables.Length == 0)
            {
                detectables.Dispose();
                return;
            }

            // For each AI with proximity enabled, check all detectables
            foreach (var (config, transform, entity) in
                SystemAPI.Query<RefRO<AggroConfig>, RefRO<LocalTransform>>()
                .WithAll<ThreatEntry>()
                .WithEntityAccess())
            {
                float radius = config.ValueRO.ProximityThreatRadius;
                if (radius <= 0f)
                    continue;

                float threatPerSec = config.ValueRO.ProximityThreatPerSecond;
                int maxTargets = config.ValueRO.MaxTrackedTargets;
                float3 aiPos = transform.ValueRO.Position;

                var threatBuffer = SystemAPI.GetBuffer<ThreatEntry>(entity);

                for (int d = 0; d < detectables.Length; d++)
                {
                    var det = detectables[d];

                    // Skip self
                    if (det.Entity == entity)
                        continue;

                    // Stealth reduces effective radius
                    float effectiveRadius = radius * math.clamp(det.StealthMultiplier, 0f, 1f);
                    if (effectiveRadius <= 0f)
                        continue;

                    float distance = math.distance(aiPos, det.Position);
                    if (distance > effectiveRadius)
                        continue;

                    float threatToAdd = threatPerSec * dt;

                    // Find or create threat entry
                    int existingIndex = -1;
                    for (int t = 0; t < threatBuffer.Length; t++)
                    {
                        if (threatBuffer[t].SourceEntity == det.Entity)
                        {
                            existingIndex = t;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                    {
                        var entry = threatBuffer[existingIndex];
                        entry.ThreatValue += threatToAdd;
                        entry.LastKnownPosition = det.Position;
                        entry.SourceFlags |= ThreatSourceFlags.Proximity;
                        threatBuffer[existingIndex] = entry;
                    }
                    else if (threatBuffer.Length < maxTargets)
                    {
                        threatBuffer.Add(new ThreatEntry
                        {
                            SourceEntity = det.Entity,
                            ThreatValue = threatToAdd,
                            LastKnownPosition = det.Position,
                            TimeSinceVisible = 999f,
                            IsCurrentlyVisible = false,
                            SourceFlags = ThreatSourceFlags.Proximity
                        });
                    }
                }
            }

            detectables.Dispose();
        }

        private struct DetectableData
        {
            public Entity Entity;
            public float3 Position;
            public float StealthMultiplier;
        }
    }
}
