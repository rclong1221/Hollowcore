using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Survival.Environment
{
    /// <summary>
    /// Detects which environment zone an entity is in using simple bounds checks.
    /// Updates CurrentEnvironmentZone component with zone properties.
    /// 
    /// This system uses ZoneBounds components for detection, NOT physics triggers.
    /// This avoids all physics-related issues (floating, invisible walls, etc.).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))]
    public partial struct EnvironmentZoneDetectionSystem : ISystem
    {
        private const bool DebugEnabled = false;

        private struct ZoneHit
        {
            public Entity ZoneEntity;
            public EnvironmentZone ZoneData;
        }

        private int _frameCounter;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _frameCounter = 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Get all zone entities with bounds
            var zoneQuery = SystemAPI.QueryBuilder()
                .WithAll<EnvironmentZone, ZoneBounds>()
                .Build();
            
            var zoneEntities = zoneQuery.ToEntityArray(Allocator.TempJob);
            var zoneBoundsArray = zoneQuery.ToComponentDataArray<ZoneBounds>(Allocator.TempJob);
            var zoneDataArray = zoneQuery.ToComponentDataArray<EnvironmentZone>(Allocator.TempJob);
            
            int zoneCount = zoneEntities.Length;
            
            // Create map to collect zones for each sensitive entity
            var zoneMap = new NativeParallelMultiHashMap<Entity, ZoneHit>(32, Allocator.TempJob);
            
            // Detect zones for each sensitive entity
            var detectJob = new DetectZonesJob
            {
                ZoneEntitiesArray = zoneEntities,
                ZoneBoundsArray = zoneBoundsArray,
                ZoneDataArray = zoneDataArray,
                ZoneMap = zoneMap.AsParallelWriter()
            };
            
            state.Dependency = detectJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
            
            // Debug logging
            _frameCounter++;
            if (DebugEnabled && _frameCounter % 300 == 0)
            {
                int mapCount = zoneMap.Count();
                int playerCount = 0;
                
                foreach (var _ in SystemAPI.Query<RefRO<EnvironmentSensitive>>())
                    playerCount++;
                
                UnityEngine.Debug.Log($"[ZoneDetection] {state.World.Name}: ZonesInMap={mapCount}, ZoneEntities={zoneCount}, Players={playerCount}");
            }
            
            // Apply zones to sensitive entities
            var applyJob = new ApplyZonesJob
            {
                ZoneMap = zoneMap
            };
            
            state.Dependency = applyJob.ScheduleParallel(state.Dependency);
            state.Dependency = zoneMap.Dispose(state.Dependency);
            state.Dependency = zoneEntities.Dispose(state.Dependency);
            state.Dependency = zoneBoundsArray.Dispose(state.Dependency);
            state.Dependency = zoneDataArray.Dispose(state.Dependency);
        }

        /// <summary>
        /// Job that detects which zones each sensitive entity is inside using simple bounds checks.
        /// </summary>
        [BurstCompile]
        partial struct DetectZonesJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity> ZoneEntitiesArray;
            [ReadOnly] public NativeArray<ZoneBounds> ZoneBoundsArray;
            [ReadOnly] public NativeArray<EnvironmentZone> ZoneDataArray;
            public NativeParallelMultiHashMap<Entity, ZoneHit>.ParallelWriter ZoneMap;

            void Execute(Entity entity, in LocalTransform transform, in EnvironmentSensitive sensitive)
            {
                float3 position = transform.Position;
                
                // Check each zone
                for (int i = 0; i < ZoneBoundsArray.Length; i++)
                {
                    if (ZoneBoundsArray[i].ContainsPoint(position))
                    {
                        ZoneMap.Add(entity, new ZoneHit 
                        { 
                            ZoneEntity = ZoneEntitiesArray[i],
                            ZoneData = ZoneDataArray[i]
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Job that applies the best zone to each sensitive entity.
        /// </summary>
        partial struct ApplyZonesJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, ZoneHit> ZoneMap;

            void Execute(Entity entity, ref CurrentEnvironmentZone currentZone, in EnvironmentSensitive sensitive)
            {
                var bestZone = CurrentEnvironmentZone.Default;
                int bestPriority = -1;

                if (ZoneMap.TryGetFirstValue(entity, out var hit, out var iterator))
                {
                    do
                    {
                        var zone = hit.ZoneData;
                        int priority = GetZonePriority(zone.ZoneType);
                        if (priority > bestPriority)
                        {
                            bestPriority = priority;
                            bestZone = new CurrentEnvironmentZone
                            {
                                ZoneEntity = hit.ZoneEntity,
                                ZoneType = zone.ZoneType,
                                OxygenRequired = zone.OxygenRequired,
                                OxygenDepletionMultiplier = zone.OxygenDepletionMultiplier,
                                RadiationRate = zone.RadiationRate,
                                IsDark = zone.IsDark,
                                StressMultiplier = zone.StressMultiplier,
                                Temperature = zone.Temperature 
                            };
                        }
                    } while (ZoneMap.TryGetNextValue(out hit, ref iterator));
                }

                // Debug log to verify transition
                if (DebugEnabled && (currentZone.ZoneType != bestZone.ZoneType || currentZone.IsDark != bestZone.IsDark))
                {
                    UnityEngine.Debug.Log($"[ZoneDetection] Player {entity.Index} Zone Changed: {currentZone.ZoneType} -> {bestZone.ZoneType}, IsDark: {currentZone.IsDark} -> {bestZone.IsDark}");
                }

                currentZone = bestZone;
            }

            static int GetZonePriority(EnvironmentZoneType type)
            {
                return type switch
                {
                    EnvironmentZoneType.Vacuum => 100,
                    EnvironmentZoneType.Toxic => 90,
                    EnvironmentZoneType.Radioactive => 80,
                    EnvironmentZoneType.Cold => 70,
                    EnvironmentZoneType.Hot => 70,
                    EnvironmentZoneType.Underwater => 60,
                    EnvironmentZoneType.Pressurized => 0,
                    _ => 0
                };
            }
        }
    }
}
