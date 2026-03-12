using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;
using DIG.Combat.Utility;
using DIG.Combat.Components;
using DIG.Weapons;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// EPIC 15.13: Processes compositional projectile behaviors.
    /// Works alongside ProjectileSystem and ProjectileExplosionSystem.
    ///
    /// Handles:
    /// - DamageOnImpact: Area damage on impact (vs direct hit damage in ProjectileSystem)
    /// - DamageOnDetonate: Area damage on detonation (vs voxel destruction in ProjectileExplosionSystem)
    /// - StickOnImpact: Stick to surfaces instead of bouncing
    /// - ApplyStatusOnHit: Apply status effects to hit targets
    /// - ApplyStatusOnDetonate: Apply area status effects on detonation
    /// - CreateAreaOnDetonate: Spawn fire pools, smoke clouds, etc.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileExplosionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct ProjectileBehaviorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            // Lookups
            var damageBufferLookup = SystemAPI.GetBufferLookup<DamageEvent>(false);
            var hitboxLookup = SystemAPI.GetComponentLookup<Hitbox>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Unity.Transforms.Parent>(true);
            var hitboxOwnerLinkLookup = SystemAPI.GetComponentLookup<HitboxOwnerLink>(true);
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var damageProfileLookup = SystemAPI.GetComponentLookup<DamageProfile>(true);

            // === STICK ON IMPACT ===
            ProcessStickOnImpact(ref state, ref ecb);

            // === DAMAGE ON IMPACT (Area damage) ===
            ProcessDamageOnImpact(ref state, ref ecb, ref damageBufferLookup, ref hitboxLookup, ref hitboxOwnerLinkLookup, ref parentLookup, ref physicsWorld, ref damageProfileLookup, currentTick);

            // === DAMAGE ON DETONATE ===
            ProcessDamageOnDetonate(ref state, ref ecb, ref damageBufferLookup, ref hitboxLookup, ref hitboxOwnerLinkLookup, ref parentLookup, ref physicsWorld, ref damageProfileLookup, currentTick);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private void ProcessStickOnImpact(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (stick, movement, impacted, transform, entity) in
                     SystemAPI.Query<RefRO<StickOnImpact>, RefRW<ProjectileMovement>, RefRO<ProjectileImpacted>, RefRW<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithNone<ProjectileStuck>()
                     .WithEntityAccess())
            {
                // Stop all movement
                movement.ValueRW.Velocity = float3.zero;
                movement.ValueRW.HasGravity = false;

                // Embed into surface
                float3 embedPosition = impacted.ValueRO.ImpactPoint +
                                       impacted.ValueRO.ImpactNormal * (-stick.ValueRO.PenetrationDepth);
                transform.ValueRW.Position = embedPosition;

                // Align rotation to impact normal (point away from surface for arrows/knives)
                if (stick.ValueRO.AlignToSurface)
                {
                    float3 forward = -impacted.ValueRO.ImpactNormal; // Point into surface
                    if (math.abs(math.dot(forward, math.up())) < 0.99f)
                    {
                        transform.ValueRW.Rotation = quaternion.LookRotation(forward, math.up());
                    }
                }

                // Mark as stuck (prevents re-processing)
                ecb.AddComponent(entity, new ProjectileStuck
                {
                    StuckPosition = embedPosition,
                    StuckEntity = impacted.ValueRO.HitEntity
                });
            }
        }

        [BurstCompile]
        private void ProcessDamageOnImpact(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            ref BufferLookup<DamageEvent> damageBufferLookup,
            ref ComponentLookup<Hitbox> hitboxLookup,
            ref ComponentLookup<HitboxOwnerLink> hitboxOwnerLinkLookup,
            ref ComponentLookup<Unity.Transforms.Parent> parentLookup,
            ref PhysicsWorldSingleton physicsWorld,
            ref ComponentLookup<DamageProfile> damageProfileLookup,
            uint currentTick)
        {
            foreach (var (damageOnImpact, projectile, impacted, entity) in
                     SystemAPI.Query<RefRO<DamageOnImpact>, RefRO<Projectile>, RefRO<ProjectileImpacted>>()
                     .WithAll<Simulate>()
                     .WithNone<DamageOnImpactApplied>()
                     .WithEntityAccess())
            {
                var config = damageOnImpact.ValueRO;
                float3 impactPoint = impacted.ValueRO.ImpactPoint;
                Entity owner = projectile.ValueRO.Owner;

                // EPIC 15.29: Read base damage element from projectile's DamageProfile
                var survivalElement = damageProfileLookup.HasComponent(entity)
                    ? DamageTypeConverter.ToSurvival(damageProfileLookup[entity].Element)
                    : DamageType.Physical;

                // Direct hit damage to the entity we hit
                if (config.ApplyToHitEntity && impacted.ValueRO.HitEntity != Entity.Null)
                {
                    ApplyDamageToEntity(
                        ref ecb,
                        ref damageBufferLookup,
                        ref hitboxLookup,
                        ref hitboxOwnerLinkLookup,
                        impacted.ValueRO.HitEntity,
                        config.Damage,
                        impactPoint,
                        owner,
                        currentTick,
                        survivalElement
                    );
                }

                // Area damage (splash damage on impact)
                if (config.DamageRadius > 0)
                {
                    ApplyAreaDamage(
                        ref ecb,
                        ref damageBufferLookup,
                        ref hitboxLookup,
                        ref hitboxOwnerLinkLookup,
                        ref parentLookup,
                        ref physicsWorld,
                        impactPoint,
                        config.DamageRadius,
                        config.Damage,
                        config.DamageFalloff,
                        owner,
                        currentTick,
                        survivalElement
                    );
                }

                // Mark as applied (prevents double damage)
                ecb.AddComponent<DamageOnImpactApplied>(entity);
            }
        }

        [BurstCompile]
        private void ProcessDamageOnDetonate(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            ref BufferLookup<DamageEvent> damageBufferLookup,
            ref ComponentLookup<Hitbox> hitboxLookup,
            ref ComponentLookup<HitboxOwnerLink> hitboxOwnerLinkLookup,
            ref ComponentLookup<Unity.Transforms.Parent> parentLookup,
            ref PhysicsWorldSingleton physicsWorld,
            ref ComponentLookup<DamageProfile> damageProfileLookup,
            uint currentTick)
        {
            foreach (var (damageOnDetonate, projectile, detonated, entity) in
                     SystemAPI.Query<RefRO<DamageOnDetonate>, RefRO<Projectile>, RefRO<ProjectileDetonated>>()
                     .WithAll<Simulate>()
                     .WithNone<DamageOnDetonateApplied>()
                     .WithEntityAccess())
            {
                var config = damageOnDetonate.ValueRO;
                float3 detonationPoint = detonated.ValueRO.DetonationPoint;
                Entity owner = projectile.ValueRO.Owner;

                // EPIC 15.29: Read base damage element from projectile's DamageProfile
                var survivalElement = damageProfileLookup.HasComponent(entity)
                    ? DamageTypeConverter.ToSurvival(damageProfileLookup[entity].Element)
                    : DamageType.Explosion;

                // Apply area damage
                ApplyAreaDamage(
                    ref ecb,
                    ref damageBufferLookup,
                    ref hitboxLookup,
                    ref hitboxOwnerLinkLookup,
                    ref parentLookup,
                    ref physicsWorld,
                    detonationPoint,
                    config.Radius,
                    config.Damage,
                    config.FalloffExponent,
                    owner,
                    currentTick,
                    survivalElement
                );

                // Mark as applied (prevents double damage)
                ecb.AddComponent<DamageOnDetonateApplied>(entity);
            }
        }

        [BurstCompile]
        private void ApplyDamageToEntity(
            ref EntityCommandBuffer ecb,
            ref BufferLookup<DamageEvent> damageBufferLookup,
            ref ComponentLookup<Hitbox> hitboxLookup,
            ref ComponentLookup<HitboxOwnerLink> hitboxOwnerLinkLookup,
            Entity targetEntity,
            float baseDamage,
            float3 hitPosition,
            Entity source,
            uint serverTick,
            DamageType damageType)
        {
            // Resolve hitbox to owner entity
            Entity actualTarget = targetEntity;
            float multiplier = 1.0f;

            if (hitboxLookup.HasComponent(targetEntity))
            {
                var hitbox = hitboxLookup[targetEntity];
                actualTarget = hitbox.OwnerEntity;
                multiplier = hitbox.DamageMultiplier;
            }

            // Redirect compound collider hits from ROOT → CHILD
            if (hitboxOwnerLinkLookup.HasComponent(actualTarget))
                actualTarget = hitboxOwnerLinkLookup[actualTarget].HitboxOwner;

            float finalDamage = baseDamage * multiplier;

            if (finalDamage > 0 && damageBufferLookup.HasBuffer(actualTarget))
            {
                ecb.AppendToBuffer(actualTarget, new DamageEvent
                {
                    Amount = finalDamage,
                    SourceEntity = source,
                    HitPosition = hitPosition,
                    ServerTick = serverTick,
                    Type = damageType
                });
            }
        }

        [BurstCompile]
        private int ApplyAreaDamage(
            ref EntityCommandBuffer ecb,
            ref BufferLookup<DamageEvent> damageBufferLookup,
            ref ComponentLookup<Hitbox> hitboxLookup,
            ref ComponentLookup<HitboxOwnerLink> hitboxOwnerLinkLookup,
            ref ComponentLookup<Unity.Transforms.Parent> parentLookup,
            ref PhysicsWorldSingleton physicsWorld,
            float3 center,
            float radius,
            float baseDamage,
            float falloffExponent,
            Entity source,
            uint serverTick,
            DamageType damageType)
        {
            int damagedCount = 0;

            // Find all entities within radius using physics overlap
            var hits = new NativeList<DistanceHit>(Allocator.Temp);
            var filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = ~0u,
                GroupIndex = 0
            };

            if (physicsWorld.OverlapSphere(center, radius, ref hits, filter))
            {
                // Track which root entities we've already damaged (to avoid double-damage from multiple hitboxes)
                var damagedEntities = new NativeHashSet<Entity>(hits.Length, Allocator.Temp);

                for (int i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    Entity targetEntity = hit.Entity;

                    // Skip self (the projectile entity's physics body)
                    if (targetEntity == source)
                        continue;

                    // Resolve to damageable entity - check hitbox, then parent chain
                    Entity actualTarget = targetEntity;
                    float multiplier = 1.0f;

                    // First check if it's a hitbox
                    if (hitboxLookup.HasComponent(targetEntity))
                    {
                        var hitbox = hitboxLookup[targetEntity];
                        actualTarget = hitbox.OwnerEntity;
                        multiplier = hitbox.DamageMultiplier;
                    }

                    // Redirect compound collider hits from ROOT → CHILD
                    if (hitboxOwnerLinkLookup.HasComponent(actualTarget))
                        actualTarget = hitboxOwnerLinkLookup[actualTarget].HitboxOwner;

                    // If no damage buffer, walk up the parent chain to find one
                    if (!damageBufferLookup.HasBuffer(actualTarget))
                    {
                        Entity current = targetEntity;
                        int maxDepth = 10;
                        while (maxDepth-- > 0 && parentLookup.HasComponent(current))
                        {
                            current = parentLookup[current].Value;
                            if (damageBufferLookup.HasBuffer(current))
                            {
                                actualTarget = current;
                                break;
                            }
                        }
                    }

                    // Skip if we've already damaged this entity
                    if (damagedEntities.Contains(actualTarget))
                        continue;
                    damagedEntities.Add(actualTarget);

                    // Calculate distance-based falloff
                    float distance = hit.Distance;
                    float normalizedDistance = math.saturate(distance / radius);
                    float falloffMultiplier = math.pow(1.0f - normalizedDistance, falloffExponent);

                    float finalDamage = baseDamage * multiplier * falloffMultiplier;

                    if (finalDamage > 0 && damageBufferLookup.HasBuffer(actualTarget))
                    {
                        ecb.AppendToBuffer(actualTarget, new DamageEvent
                        {
                            Amount = finalDamage,
                            SourceEntity = source,
                            HitPosition = center,
                            ServerTick = serverTick,
                            Type = damageType
                        });
                        damagedCount++;
                    }
                }

                damagedEntities.Dispose();
            }

            hits.Dispose();
            return damagedCount;
        }
    }

    /// <summary>
    /// Tag component: Projectile has stuck to a surface.
    /// </summary>
    public struct ProjectileStuck : IComponentData
    {
        public float3 StuckPosition;
        public Entity StuckEntity;
    }

    /// <summary>
    /// Tag component: DamageOnImpact has been applied.
    /// </summary>
    public struct DamageOnImpactApplied : IComponentData { }

    /// <summary>
    /// Tag component: DamageOnDetonate has been applied.
    /// </summary>
    public struct DamageOnDetonateApplied : IComponentData { }
}
