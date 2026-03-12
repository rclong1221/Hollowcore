using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using DIG.Survival.Environment;

namespace DIG.Swimming.Systems
{
    /// <summary>
    /// Detects when players enter/exit water zones and updates SwimmingState.
    /// Uses the existing ZoneBounds-based detection from EnvironmentZoneDetectionSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnvironmentZoneDetectionSystem))]
    public partial struct WaterDetectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Query all water zones
            foreach (var (swimState, transform, envZone, entity) in 
                SystemAPI.Query<RefRW<SwimmingState>, RefRO<LocalTransform>, RefRW<CurrentEnvironmentZone>>()
                    .WithAll<CanSwim>()
                    .WithEntityAccess())
            {
                float playerFeetY = transform.ValueRO.Position.y;
                var currentZone = envZone.ValueRO;
                float playerHeadY = playerFeetY + swimState.ValueRO.PlayerHeight;
                float playerCenterY = playerFeetY + swimState.ValueRO.PlayerHeight * 0.5f;
                
                // Check current environment zone for underwater type
                bool inWaterZone = false;
                float waterSurfaceY = float.MinValue;
                Entity waterEntity = Entity.Null;
                
                // CurrentEnvironmentZone is now part of the query, so direct access is possible
                if (currentZone.ZoneType == EnvironmentZoneType.Underwater)
                {
                    inWaterZone = true;
                    waterEntity = currentZone.ZoneEntity;
                    
                    // Try to get water properties for surface level
                    if (waterEntity != Entity.Null && SystemAPI.HasComponent<WaterProperties>(waterEntity))
                    {
                        var waterProps = SystemAPI.GetComponent<WaterProperties>(waterEntity);
                        waterSurfaceY = waterProps.SurfaceY;
                    }
                    else
                    {
                        // Fallback: get from ZoneBounds if available
                        if (waterEntity != Entity.Null && SystemAPI.HasComponent<ZoneBounds>(waterEntity))
                        {
                            var bounds = SystemAPI.GetComponent<ZoneBounds>(waterEntity);
                            waterSurfaceY = bounds.Center.y + bounds.HalfExtents.y;
                        }
                    }
                }
                
                if (inWaterZone && waterSurfaceY > float.MinValue)
                {
                    // Calculate submersion
                    float submersion = math.max(0, waterSurfaceY - playerFeetY);
                    float submersionRatio = submersion / swimState.ValueRO.PlayerHeight;
                    
                    // [SWIM_DIAG]
                    // Debug.Log($"[SWIM_DIAG] Entity {entity.Index}: InWaterZone=True SurfaceY={waterSurfaceY:F2} FeetY={playerFeetY:F2} Ratio={submersionRatio:F2}");
                    
                    swimState.ValueRW.WaterSurfaceY = waterSurfaceY;
                    swimState.ValueRW.SubmersionDepth = submersion;
                    swimState.ValueRW.WaterZoneEntity = waterEntity;
                    swimState.ValueRW.IsSubmerged = playerHeadY < (waterSurfaceY - 0.2f);
                    
                    // Hysteresis for swim state transitions
                    if (!swimState.ValueRO.IsSwimming)
                    {
                        // Enter swimming when sufficiently submerged
                        if (submersionRatio >= swimState.ValueRO.SwimEntryThreshold)
                        {
                            UnityEngine.Debug.Log($"[SWIM_DIAG] Entity {entity.Index}: ENTER SWIM! Ratio ({submersionRatio:F2}) >= Threshold ({swimState.ValueRO.SwimEntryThreshold:F2})");
                            swimState.ValueRW.IsSwimming = true;
                        }
                    }
                    else
                    {
                        // Exit swimming when mostly out of water
                        if (submersionRatio < swimState.ValueRO.SwimExitThreshold)
                        {
                            UnityEngine.Debug.Log($"[SWIM_DIAG] Entity {entity.Index}: EXIT SWIM! Ratio ({submersionRatio:F2}) < Threshold ({swimState.ValueRO.SwimExitThreshold:F2})");
                            swimState.ValueRW.IsSwimming = false;
                        }
                    }

                    // Override oxygen requirement if head is above water
                    // This prevents OxygenSuffocationSystem from killing player when surfacing to breathe
                    if (envZone.ValueRO.ZoneType == EnvironmentZoneType.Underwater)
                    {
                        envZone.ValueRW.OxygenRequired = swimState.ValueRW.IsSubmerged;
                    }
                }
                else
                {
                    if (swimState.ValueRO.IsSwimming)
                    {
                         UnityEngine.Debug.Log($"[SWIM_DIAG] Entity {entity.Index}: FORCE EXIT SWIM - Not in water zone or invalid surface.");
                    }

                    // Not in water
                    swimState.ValueRW.WaterSurfaceY = float.MinValue;
                    swimState.ValueRW.SubmersionDepth = 0f;
                    swimState.ValueRW.WaterZoneEntity = Entity.Null;
                    swimState.ValueRW.IsSwimming = false;
                    swimState.ValueRW.IsSubmerged = false;
                }
            }
        }
    }
}
