using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Survival.Explosives
{
    /// <summary>
    /// Damage event from explosion. Consumed by bridge system to apply to Health.
    /// </summary>
    public struct ExplosionDamageEvent : IComponentData
    {
        /// <summary>
        /// Entity to apply damage to.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Amount of damage to apply.
        /// </summary>
        public float Damage;

        /// <summary>
        /// Entity that caused the explosion (for attribution).
        /// </summary>
        public Entity SourceEntity;
    }

    /// <summary>
    /// Processes detonation requests and applies explosion effects.
    /// Damages entities, requests voxel destruction, applies physics forces.
    /// Server-authoritative.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExplosiveFuseSystem))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ExplosiveDetonationSystem : ISystem
    {
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _velocityLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, explosive, stats, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlacedExplosive>, RefRO<ExplosiveStats>>()
                     .WithAll<DetonationRequest>()
                     .WithEntityAccess())
            {
                float3 explosionCenter = transform.ValueRO.Position;
                var explosiveData = explosive.ValueRO;
                var explosiveStats = stats.ValueRO;

                // Create explosion event for VFX
                var eventEntity = ecb.CreateEntity();
                ecb.AddComponent(eventEntity, new ExplosionEvent
                {
                    Position = explosionCenter,
                    Type = explosiveData.Type,
                    BlastRadius = explosiveStats.BlastRadius,
                    Tick = networkTime.ServerTick
                });

                // Find all entities in blast radius using point distance query
                var hits = new NativeList<DistanceHit>(Allocator.Temp);
                var pointDistanceInput = new PointDistanceInput
                {
                    Position = explosionCenter,
                    MaxDistance = explosiveStats.BlastRadius,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                if (physicsWorld.CollisionWorld.CalculateDistance(pointDistanceInput, ref hits))
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        var hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;

                        // Skip self
                        if (hitEntity == entity)
                            continue;

                        float distance = hit.Distance;
                        float normalizedDistance = distance / explosiveStats.BlastRadius;

                        // Calculate damage falloff
                        float falloff = 1f - math.pow(normalizedDistance, explosiveStats.FalloffExponent);
                        falloff = math.saturate(falloff);

                        // Create damage event for bridge system to consume
                        float damage = explosiveStats.BlastDamage * falloff;
                        if (damage > 0.01f)
                        {
                            var damageEventEntity = ecb.CreateEntity();
                            ecb.AddComponent(damageEventEntity, new ExplosionDamageEvent
                            {
                                TargetEntity = hitEntity,
                                Damage = damage,
                                SourceEntity = explosiveData.PlacerEntity
                            });
                        }

                        // Apply physics impulse
                        if (_velocityLookup.HasComponent(hitEntity) && _transformLookup.HasComponent(hitEntity))
                        {
                            var velocity = _velocityLookup[hitEntity];
                            var hitTransform = _transformLookup[hitEntity];

                            // Direction from explosion to entity
                            float3 direction = math.normalizesafe(hitTransform.Position - explosionCenter);
                            if (math.lengthsq(direction) < 0.001f)
                            {
                                direction = new float3(0, 1, 0);
                            }

                            // Add upward component for more dramatic effect
                            direction = math.normalize(direction + new float3(0, 0.5f, 0));

                            float force = explosiveStats.PhysicsForce * falloff;
                            velocity.Linear += direction * force * 0.01f; // Scale down for velocity units

                            _velocityLookup[hitEntity] = velocity;
                        }
                    }
                }

                hits.Dispose();

                // EPIC 15.10: Create unified voxel damage request based on shape type
                var voxelDamageEntity = ecb.CreateEntity();
                
                switch (explosiveStats.ShapeType)
                {
                    case DIG.Voxel.VoxelDamageShapeType.Cone:
                        // Shaped charge - use cone in explosive's forward direction
                        ecb.AddComponent(voxelDamageEntity, DIG.Voxel.VoxelDamageRequest.CreateCone(
                            sourcePos: explosionCenter,
                            source: explosiveData.PlacerEntity,
                            targetPos: explosionCenter,
                            rotation: transform.ValueRO.Rotation,
                            angleDegrees: explosiveStats.ConeAngle,
                            length: explosiveStats.ShapeLength,
                            tipRadius: explosiveStats.VoxelDamageRadius,
                            damage: explosiveStats.BlastDamage,
                            falloff: DIG.Voxel.VoxelDamageFalloff.Linear,
                            edgeMult: 0.3f,
                            damageType: DIG.Voxel.VoxelDamageType.Explosive
                        ));
                        break;
                        
                    case DIG.Voxel.VoxelDamageShapeType.Cylinder:
                        // Directional cylinder charge
                        ecb.AddComponent(voxelDamageEntity, DIG.Voxel.VoxelDamageRequest.CreateCylinder(
                            sourcePos: explosionCenter,
                            source: explosiveData.PlacerEntity,
                            targetPos: explosionCenter,
                            rotation: transform.ValueRO.Rotation,
                            radius: explosiveStats.VoxelDamageRadius,
                            height: explosiveStats.ShapeLength,
                            damage: explosiveStats.BlastDamage,
                            falloff: DIG.Voxel.VoxelDamageFalloff.None,
                            edgeMult: 1f,
                            damageType: DIG.Voxel.VoxelDamageType.Explosive
                        ));
                        break;
                        
                    default:
                        // Standard sphere explosion
                        ecb.AddComponent(voxelDamageEntity, DIG.Voxel.VoxelDamageRequest.CreateSphere(
                            sourcePos: explosionCenter,
                            source: explosiveData.PlacerEntity,
                            targetPos: explosionCenter,
                            radius: explosiveStats.VoxelDamageRadius,
                            damage: explosiveStats.BlastDamage,
                            falloff: DIG.Voxel.VoxelDamageFalloff.Quadratic,
                            edgeMult: 0.1f,
                            damageType: DIG.Voxel.VoxelDamageType.Explosive
                        ));
                        break;
                }

                // Destroy the explosive entity
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Cleans up explosion damage events after processing.
    /// The actual damage application is handled by a bridge system in the Player assembly.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExplosionDamageEventCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<ExplosionDamageEvent>>()
                     .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Cleans up explosion event entities after they've been processed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExplosiveDetonationSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExplosionEventCleanupSystem : ISystem
    {
        private const int EventLifetimeTicks = 10;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (explosionEvent, entity) in
                     SystemAPI.Query<RefRO<ExplosionEvent>>()
                     .WithEntityAccess())
            {
                // Destroy events after a short delay
                var ticksSince = networkTime.ServerTick.TicksSince(explosionEvent.ValueRO.Tick);
                if (ticksSince > EventLifetimeTicks)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Cleans up processed voxel damage requests.
    /// EPIC 15.10: Now uses the unified VoxelDamageRequest from DIG.Voxel.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(DIG.Voxel.VoxelDamageProcessingSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct VoxelDamageRequestCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, entity) in
                     SystemAPI.Query<RefRO<DIG.Voxel.VoxelDamageRequest>>()
                     .WithEntityAccess())
            {
                // Destroy processed requests (those marked as processed by the voxel system)
                if (request.ValueRO.IsProcessed)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
