using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Tag component to request detonation of a chain-triggerable entity.
    /// Processed by systems that handle explosions.
    /// </summary>
    public struct VoxelDetonationRequest : IComponentData { }
    
    /// <summary>
    /// EPIC 15.10: Event emitted when a voxel explosion occurs.
    /// Used by ChainReactionSystem to detect nearby triggerables.
    /// </summary>
    public struct VoxelExplosionEvent : IComponentData
    {
        public float3 Position;
        public float BlastRadius;
        public float Damage;
    }
    
    /// <summary>
    /// EPIC 15.10: Detects and processes chain reactions from explosions.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoxelDamageProcessingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct ChainReactionSystem : ISystem
    {
        private EntityQuery _explosionQuery;
        
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
            
            // Process triggered chain entities (countdown timers)
            foreach (var (triggerable, transform, entity) in
                     SystemAPI.Query<RefRW<ChainTriggerable>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                if (!triggerable.ValueRO.IsTriggered)
                    continue;
                
                // Count down timer
                triggerable.ValueRW.TriggerTimer -= deltaTime;
                
                if (triggerable.ValueRO.TriggerTimer <= 0)
                {
                    // Time to detonate! Add detonation request
                    ecb.AddComponent<VoxelDetonationRequest>(entity);
                    
                    // Emit chain reaction event
                    var eventEntity = ecb.CreateEntity();
                    ecb.AddComponent(eventEntity, new ChainReactionEvent
                    {
                        SourceEntity = Entity.Null, // Could track original source
                        TriggeredEntity = entity,
                        Position = transform.ValueRO.Position,
                        Damage = 0, // Filled by explosion stats
                        ChainDepth = triggerable.ValueRO.ChainDepth
                    });
                }
            }
            
            // Check for new triggers from recent voxel explosions
            foreach (var explosion in SystemAPI.Query<RefRO<VoxelExplosionEvent>>())
            {
                float3 explosionPos = explosion.ValueRO.Position;
                float explosionRadius = explosion.ValueRO.BlastRadius;
                
                // Find nearby chain-triggerable entities
                foreach (var (triggerable, transform, entity) in
                         SystemAPI.Query<RefRW<ChainTriggerable>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
                {
                    // Skip already triggered or exceeds max chain depth
                    if (triggerable.ValueRO.IsTriggered)
                        continue;
                    
                    if (triggerable.ValueRO.ChainDepth >= triggerable.ValueRO.MaxChainDepth)
                        continue;
                    
                    float3 entityPos = transform.ValueRO.Position;
                    float distance = math.length(entityPos - explosionPos);
                    
                    // Check if within trigger radius
                    if (distance <= explosionRadius + triggerable.ValueRO.TriggerRadius)
                    {
                        // Calculate damage at this distance (simple falloff)
                        float normalizedDist = distance / (explosionRadius + 0.01f);
                        float damage = (1f - normalizedDist) * explosion.ValueRO.Damage;
                        
                        // Check threshold
                        if (damage >= triggerable.ValueRO.TriggerThreshold)
                        {
                            // Trigger!
                            triggerable.ValueRW.IsTriggered = true;
                            triggerable.ValueRW.TriggerTimer = triggerable.ValueRO.TriggerDelay;
                            triggerable.ValueRW.ChainDepth++;
                        }
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// EPIC 15.10: Processes environmental hazard breaches.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoxelHealthTrackingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct EnvironmentalHazardSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Get health storage to check for destroyed voxels
            if (!SystemAPI.TryGetSingleton<VoxelHealthStorage>(out var storage))
            {
                ecb.Dispose();
                return;
            }
            
            // Check hazard positions against destruction queue
            foreach (var (hazard, entity) in
                     SystemAPI.Query<RefRO<EnvironmentalHazard>>()
                     .WithEntityAccess())
            {
                // Check if hazard area has been breached
                // For simplicity, check if health at center is depleted
                int3 voxelCoord = (int3)math.floor(hazard.ValueRO.Position);
                float health = storage.GetHealth(voxelCoord);
                
                if (health <= 0)
                {
                    // Hazard breached! Emit event
                    var eventEntity = ecb.CreateEntity();
                    ecb.AddComponent(eventEntity, new EnvironmentalHazardEvent
                    {
                        Type = hazard.ValueRO.Type,
                        Position = hazard.ValueRO.Position,
                        Intensity = hazard.ValueRO.Intensity,
                        SourceEntity = Entity.Null
                    });
                    
                    // Handle specific hazard types
                    switch (hazard.ValueRO.Type)
                    {
                        case EnvironmentalHazardType.GasPocket:
                            // Create sphere explosion
                            var explosionEntity = ecb.CreateEntity();
                            ecb.AddComponent(explosionEntity, VoxelDamageRequest.CreateSphere(
                                sourcePos: hazard.ValueRO.Position,
                                source: entity,
                                targetPos: hazard.ValueRO.Position,
                                radius: hazard.ValueRO.Radius * hazard.ValueRO.Intensity,
                                damage: 150f * hazard.ValueRO.Intensity,
                                falloff: VoxelDamageFalloff.Quadratic,
                                edgeMult: 0.2f,
                                damageType: VoxelDamageType.Explosive
                            ));
                            break;
                            
                        case EnvironmentalHazardType.LavaFlow:
                            // TODO: Create sustained heat damage over time
                            break;
                            
                        case EnvironmentalHazardType.UnstableGround:
                            // TODO: Trigger collapse physics
                            break;
                    }
                    
                    // Remove hazard after triggering
                    ecb.DestroyEntity(entity);
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
