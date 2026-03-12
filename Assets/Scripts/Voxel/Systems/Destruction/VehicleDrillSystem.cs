using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Processes vehicle-mounted drills and emits destruction requests.
    /// Server-authoritative system.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VoxelDamageValidationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct VehicleDrillSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Process vehicles with drill buffers
            foreach (var (drillBuffer, transform, entity) in
                     SystemAPI.Query<DynamicBuffer<VehicleDrillBuffer>, RefRO<LocalToWorld>>()
                     .WithAll<HasVehicleDrills>()
                     .WithEntityAccess())
            {
                // Get mutable buffer reference
                var mutableBuffer = SystemAPI.GetBuffer<VehicleDrillBuffer>(entity);
                
                for (int i = 0; i < mutableBuffer.Length; i++)
                {
                    var drill = mutableBuffer[i].Drill;
                    
                    // Update heat
                    if (drill.IsActive)
                    {
                        drill.HeatLevel = math.min(drill.HeatLevel + drill.HeatGenerationRate * deltaTime, drill.MaxHeat);
                        
                        // Auto-shutdown on overheat
                        if (drill.HeatLevel >= drill.MaxHeat)
                        {
                            drill.IsActive = false;
                        }
                    }
                    else
                    {
                        drill.HeatLevel = math.max(drill.HeatLevel - drill.HeatDissipationRate * deltaTime, 0f);
                    }
                    
                    // If active and not overheated, emit destruction request
                    if (drill.IsActive && drill.HeatLevel < drill.MaxHeat)
                    {
                        // Calculate world position and rotation
                        float4x4 vehicleMatrix = transform.ValueRO.Value;
                        float3 worldDrillPos = math.transform(vehicleMatrix, drill.LocalOffset);
                        float3 worldDrillDir = math.normalize(math.mul(vehicleMatrix, new float4(drill.LocalDirection, 0)).xyz);
                        
                        // Create rotation from direction
                        quaternion drillRotation = quaternion.LookRotationSafe(worldDrillDir, math.up());
                        
                        // Calculate damage with heat efficiency penalty
                        float heatEfficiency = 1f - (drill.HeatLevel / drill.MaxHeat) * 0.3f; // Max 30% penalty
                        float damage = drill.DamagePerSecond * deltaTime * heatEfficiency;
                        
                        // Create voxel damage request
                        var requestEntity = ecb.CreateEntity();
                        
                        if (drill.ShapeType == VoxelDamageShapeType.Cylinder)
                        {
                            ecb.AddComponent(requestEntity, VoxelDamageRequest.CreateCylinder(
                                sourcePos: worldDrillPos,
                                source: entity,
                                targetPos: worldDrillPos + worldDrillDir * (drill.DrillLength * 0.5f),
                                rotation: drillRotation,
                                radius: drill.DrillRadius,
                                height: drill.DrillLength,
                                damage: damage,
                                falloff: VoxelDamageFalloff.None,
                                edgeMult: 1f,
                                damageType: drill.DamageType
                            ));
                        }
                        else if (drill.ShapeType == VoxelDamageShapeType.Capsule)
                        {
                            ecb.AddComponent(requestEntity, VoxelDamageRequest.CreateCapsule(
                                sourcePos: worldDrillPos,
                                source: entity,
                                targetPos: worldDrillPos + worldDrillDir * (drill.DrillLength * 0.5f),
                                rotation: drillRotation,
                                radius: drill.DrillRadius,
                                length: drill.DrillLength,
                                damage: damage,
                                falloff: VoxelDamageFalloff.Linear,
                                edgeMult: 0.5f,
                                damageType: drill.DamageType
                            ));
                        }
                        else
                        {
                            // Default to sphere at drill tip
                            ecb.AddComponent(requestEntity, VoxelDamageRequest.CreateSphere(
                                sourcePos: worldDrillPos,
                                source: entity,
                                targetPos: worldDrillPos + worldDrillDir * drill.DrillLength,
                                radius: drill.DrillRadius,
                                damage: damage,
                                falloff: VoxelDamageFalloff.Linear,
                                edgeMult: 0.5f,
                                damageType: drill.DamageType
                            ));
                        }
                    }
                    
                    // Update drill state in buffer
                    mutableBuffer[i] = new VehicleDrillBuffer { Drill = drill };
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// EPIC 15.10: Processes drill input from player/AI controllers.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VehicleDrillSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct VehicleDrillInputSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Match input to drills on controlled vehicle
            foreach (var (input, vehicleRef) in
                     SystemAPI.Query<RefRO<VehicleDrillInput>, RefRO<ControlledVehicle>>())
            {
                Entity vehicleEntity = vehicleRef.ValueRO.VehicleEntity;
                
                if (!SystemAPI.HasBuffer<VehicleDrillBuffer>(vehicleEntity))
                    continue;
                
                var drillBuffer = SystemAPI.GetBuffer<VehicleDrillBuffer>(vehicleEntity);
                
                for (int i = 0; i < drillBuffer.Length; i++)
                {
                    var drill = drillBuffer[i].Drill;
                    bool shouldBeActive = input.ValueRO.RequestActive && ((input.ValueRO.ActiveDrillMask & (1 << i)) != 0);
                    
                    drill.IsActive = shouldBeActive;
                    drillBuffer[i] = new VehicleDrillBuffer { Drill = drill };
                }
            }
        }
    }
    
    /// <summary>
    /// EPIC 15.10: Reference to the vehicle being controlled by a player/AI.
    /// </summary>
    public struct ControlledVehicle : IComponentData
    {
        public Entity VehicleEntity;
    }
}
