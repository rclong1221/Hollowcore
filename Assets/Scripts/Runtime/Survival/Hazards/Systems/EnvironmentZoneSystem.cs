using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Survival.Hazards
{
    /// <summary>
    /// Detects which environment zone entities are in using physics overlap.
    /// Uses trigger colliders on zone entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct EnvironmentZoneDetectionSystem : ISystem
    {
        private ComponentLookup<EnvironmentZone> _zoneLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _zoneLookup = state.GetComponentLookup<EnvironmentZone>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _zoneLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            // Build list of zone entities
            var zones = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<EnvironmentZone>>()
                     .WithEntityAccess())
            {
                zones.Add(entity);
            }

            // For each entity with InEnvironmentZone, find overlapping zones
            foreach (var (transform, inZone, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<InEnvironmentZone>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                float3 position = transform.ValueRO.Position;

                // Find highest priority overlapping zone
                Entity bestZone = Entity.Null;
                int bestPriority = int.MinValue;
                EnvironmentZone bestZoneData = default;

                // Use point distance query to find nearby zones
                var hits = new NativeList<DistanceHit>(Allocator.Temp);
                var pointInput = new PointDistanceInput
                {
                    Position = position,
                    MaxDistance = 0.1f, // Very small - we want to be inside the trigger
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                // Check each zone entity directly by position overlap
                for (int i = 0; i < zones.Length; i++)
                {
                    var zoneEntity = zones[i];
                    if (!_zoneLookup.HasComponent(zoneEntity))
                        continue;

                    var zone = _zoneLookup[zoneEntity];

                    // Simple AABB check - zones should have colliders
                    // For now, check if any physics body with EnvironmentZone contains the point
                    if (IsInsideZone(ref physicsWorld, zoneEntity, position))
                    {
                        if (zone.Priority > bestPriority)
                        {
                            bestPriority = zone.Priority;
                            bestZone = zoneEntity;
                            bestZoneData = zone;
                        }
                    }
                }

                hits.Dispose();

                // Update InEnvironmentZone
                ref var inZoneRef = ref inZone.ValueRW;
                if (bestZone != Entity.Null)
                {
                    inZoneRef.ZoneEntity = bestZone;
                    inZoneRef.ZoneType = bestZoneData.ZoneType;
                    inZoneRef.ZoneTemperature = bestZoneData.Temperature;
                    inZoneRef.RadiationLevel = bestZoneData.RadiationLevel;
                    inZoneRef.OxygenAvailable = bestZoneData.OxygenAvailable;
                }
                else
                {
                    // Default to vacuum/space when outside all zones
                    inZoneRef.ZoneEntity = Entity.Null;
                    inZoneRef.ZoneType = ZoneType.Vacuum;
                    inZoneRef.ZoneTemperature = -270f; // Near absolute zero
                    inZoneRef.RadiationLevel = 0f;
                    inZoneRef.OxygenAvailable = false;
                }
            }

            zones.Dispose();
        }

        private static bool IsInsideZone(ref PhysicsWorld world, Entity zoneEntity, float3 position)
        {
            // Find the rigid body for this zone entity
            for (int i = 0; i < world.NumBodies; i++)
            {
                if (world.Bodies[i].Entity == zoneEntity)
                {
                    // Check if point is inside the collider
                    var body = world.Bodies[i];
                    if (body.Collider.IsCreated)
                    {
                        var pointInput = new PointDistanceInput
                        {
                            Position = position,
                            MaxDistance = 0f,
                            Filter = CollisionFilter.Default
                        };

                        if (body.Collider.Value.CalculateDistance(pointInput, out _))
                        {
                            return true;
                        }
                    }
                    break;
                }
            }
            return false;
        }
    }
}
