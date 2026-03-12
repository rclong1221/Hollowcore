using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Environment.Gravity;

namespace DIG.Environment.Gravity
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct GravityZoneSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 1. Reset Overrides
            foreach (var overrideComp in SystemAPI.Query<RefRW<GravityOverride>>())
            {
                overrideComp.ValueRW.IsActive = false;
                overrideComp.ValueRW.Priority = float.MinValue;
            }

            // 2. Calculate Gravity for every entity with GravityOverride + Transform
            // Note: N*M check is expensive. For production, use TriggerEvents or SpatialQuery.
            // For now, assuming low count of GravityZones.
            
            var zoneQuery = SystemAPI.QueryBuilder().WithAll<GravityZoneComponent, LocalTransform>().Build();
            var zones = zoneQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var zoneComps = zoneQuery.ToComponentDataArray<GravityZoneComponent>(Unity.Collections.Allocator.Temp);
            var zoneTransforms = zoneQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

            if (zones.Length == 0) return;

            // Iterate players (or anything with GravityOverride)
            foreach (var (transform, gravityOverride, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<GravityOverride>>()
                     .WithEntityAccess())
            {
                float3 position = transform.ValueRO.Position;

                for (int i = 0; i < zones.Length; i++)
                {
                    var zone = zoneComps[i];
                    var zonePos = zoneTransforms[i].Position + zone.Center;
                    float distSq = math.distancesq(position, zonePos);
                    float radiusSq = zone.Radius * zone.Radius;

                    if (distSq <= radiusSq)
                    {
                        // Inside Zone
                        // Direction: Center - Player (Pull) or constant direction?
                        // Spherical Gravity usually pulls to center.
                        
                        float3 dir = math.normalizesafe(zonePos - position);
                        float strength = zone.Strength;

                        // Falloff logic
                        if (zone.Falloff > 0)
                        {
                            float dist = math.sqrt(distSq);
                            float t = dist / zone.Radius; // 0 at center, 1 at edge
                            // Simple linear falloff: 1 - t
                            strength *= math.max(0, 1f - t);
                        }

                        // Apply if higher priority (Strength as priority proxy for now)
                        if (strength > gravityOverride.ValueRO.Priority)
                        {
                            gravityOverride.ValueRW.IsActive = true;
                            gravityOverride.ValueRW.GravityVector = dir * strength;
                            gravityOverride.ValueRW.Priority = strength;
                        }
                    }
                }
            }

            zones.Dispose();
            zoneComps.Dispose();
            zoneTransforms.Dispose();
        }
    }
}
