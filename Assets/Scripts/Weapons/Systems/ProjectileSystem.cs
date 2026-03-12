using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Weapons;

using DIG.Combat.Systems;
using DIG.Combat.Resolvers;
using DIG.Combat.Components;
using Player.Components;
using DIG.Combat.Utility;
using DIG.Voxel.Components;
using UnityEngine;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Handles projectile physics, movement, and impact detection.
    /// Implements 13.16.1 Hitbox multipliers and damage application.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ProjectileSystem : ISystem
    {
        private const bool DEBUG_GRENADES = false;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
            float deltaTime = SystemAPI.Time.DeltaTime;

            // These singletons are guaranteed to exist by RequireForUpdate in OnCreate
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 13.16.1: Hitbox & Damage Lookups
            var hitboxLookup = SystemAPI.GetComponentLookup<Hitbox>(true);
            var hasHitboxesLookup = SystemAPI.GetComponentLookup<HasHitboxes>(true); // 13.16.2
            var damageBufferLookup = SystemAPI.GetBufferLookup<DamageEvent>(false);
            var hitboxOwnerLinkLookup = SystemAPI.GetComponentLookup<HitboxOwnerLink>(true);

            // EPIC 15.29: DamageProfile lookup (stamped onto projectile by ThrowableActionSystem)
            var damageProfileLookup = SystemAPI.GetComponentLookup<DamageProfile>(true);

            // EPIC 15.10: Explosion component lookups
            var detonateOnTimerLookup = SystemAPI.GetComponentLookup<DetonateOnTimer>(true);
            var detonateOnImpactLookup = SystemAPI.GetComponentLookup<DetonateOnImpact>(true);
            var projectileDetonatedLookup = SystemAPI.GetComponentLookup<ProjectileDetonated>(true);

            // Network Time for DamageEvent
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            foreach (var (projectile, movement, impact, transform, entity) in 
                     SystemAPI.Query<RefRW<Projectile>, RefRW<ProjectileMovement>, RefRW<ProjectileImpact>, RefRW<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithNone<ProjectileImpacted>()
                     .WithEntityAccess())
            {
                ref var projRef = ref projectile.ValueRW;
                ref var moveRef = ref movement.ValueRW;
                ref var impactRef = ref impact.ValueRW;
                ref var transformRef = ref transform.ValueRW;

                // EPIC 15.29: Read base damage element from projectile's DamageProfile
                var themeElement = damageProfileLookup.HasComponent(entity)
                    ? damageProfileLookup[entity].Element
                    : DIG.Targeting.Theming.DamageType.Physical;
                var survivalElement = DamageTypeConverter.ToSurvival(themeElement);

                if (DEBUG_GRENADES && projRef.Type == ProjectileType.Grenade)
                {
                    Debug.Log($"[GRENADE] Projectile {entity.Index}: Pos={transformRef.Position}, Elapsed={projRef.ElapsedTime:F2}/{projRef.Lifetime:F1}s, Bounces={impactRef.CurrentBounces}/{impactRef.MaxBounces}");
                }

                // Update lifetime
                projRef.ElapsedTime += deltaTime;

                // EPIC 15.10: Don't destroy if has DetonateOnTimer - let ProjectileExplosionSystem handle it
                // Only destroy on server - client ghosts are destroyed automatically when server despawns them
                if (projRef.ElapsedTime >= projRef.Lifetime && !detonateOnTimerLookup.HasComponent(entity) && state.WorldUnmanaged.IsServer())
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Apply gravity
                if (moveRef.HasGravity)
                {
                    moveRef.Velocity.y -= moveRef.Gravity * deltaTime;
                }

                // Apply drag
                if (moveRef.Drag > 0)
                {
                    float speed = math.length(moveRef.Velocity);
                    if (speed > 0)
                    {
                        float dragForce = moveRef.Drag * speed * speed * deltaTime;
                        float newSpeed = math.max(0, speed - dragForce);
                        moveRef.Velocity = math.normalize(moveRef.Velocity) * newSpeed;
                    }
                }

                // Move projectile
                float3 oldPos = transformRef.Position;
                float3 newPos = oldPos + moveRef.Velocity * deltaTime;

                // Check for collision
                var rayInput = new RaycastInput
                {
                    Start = oldPos,
                    End = newPos,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                if (physicsWorld.CastRay(rayInput, out var hit))
                {
                    // Impact!
                    if (impactRef.BounceOnImpact && impactRef.CurrentBounces < impactRef.MaxBounces)
                    {
                        // Bounce
                        float3 reflected = math.reflect(moveRef.Velocity, hit.SurfaceNormal);
                        moveRef.Velocity = reflected * 0.6f; // Lose some energy
                        impactRef.CurrentBounces++;
                        transformRef.Position = hit.Position + hit.SurfaceNormal * 0.01f;

                        if (DEBUG_GRENADES && projRef.Type == ProjectileType.Grenade)
                        {
                            Debug.Log($"[GRENADE] Projectile {entity.Index} BOUNCED at {hit.Position}, bounce #{impactRef.CurrentBounces}");
                        }
                    }
                    else
                    {
                        // Final impact
                        transformRef.Position = hit.Position;

                        if (DEBUG_GRENADES && projRef.Type == ProjectileType.Grenade)
                        {
                            Debug.Log($"[GRENADE] Projectile {entity.Index} FINAL IMPACT at {hit.Position}");
                        }

                        // Mark as impacted
                        ecb.AddComponent(entity, new ProjectileImpacted
                        {
                            ImpactPoint = hit.Position,
                            ImpactNormal = hit.SurfaceNormal,
                            HitEntity = hit.Entity
                        });

                        if (impactRef.ExplodeOnImpact && impactRef.ImpactRadius > 0)
                        {
                            var explosionEntity = ecb.CreateEntity();
                            ecb.AddComponent(explosionEntity, new ModifierExplosionRequest
                            {
                                Position = hit.Position,
                                SourceEntity = projRef.Owner,
                                Damage = projRef.Damage,
                                Radius = impactRef.ImpactRadius,
                                Element = themeElement,
                                KnockbackForce = 0f
                            });
                        }
                        else if (hit.Entity != Entity.Null)
                        {
                            // 13.16.1: Hitbox Logic & Direct Damage
                            Entity targetEntity = hit.Entity;
                            float multiplier = 1.0f;

                            // 13.16.2: Raycast Fallback for Nested Hitboxes
                            Entity bestHitbox = Entity.Null; // Declare outside for scope access

                            // If we hit a Root Owner but not a specific Hitbox, try to find one inside.
                            if (!hitboxLookup.HasComponent(hit.Entity) && hasHitboxesLookup.HasComponent(hit.Entity))
                            {
                                // Main Capsule Hit. Perform penetration raycast.
                                var penetrationInput = rayInput;
                                penetrationInput.Start = rayInput.Start; // Determine from original start
                                penetrationInput.End = rayInput.End;

                                var allHits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
                                if (physicsWorld.CastRay(penetrationInput, ref allHits))
                                {
                                    float closestDist = float.MaxValue;

                                    for (int i = 0; i < allHits.Length; i++)
                                    {
                                        var subHit = allHits[i];
                                        if (subHit.Entity == hit.Entity) continue; // Ignore Root
                                        
                                        if (hitboxLookup.HasComponent(subHit.Entity))
                                        {
                                            var possibleHitbox = hitboxLookup[subHit.Entity];
                                            if (possibleHitbox.OwnerEntity == hit.Entity)
                                            {
                                                // It's a child Hitbox of the Root we hit
                                                float dist = math.distancesq(rayInput.Start, subHit.Position);
                                                if (dist < closestDist)
                                                {
                                                    closestDist = dist;
                                                    bestHitbox = subHit.Entity;
                                                }
                                            }
                                        }
                                    }
                                }
                                allHits.Dispose();
                            }

                            Entity hitEntityToProcess = (bestHitbox != Entity.Null) ? bestHitbox : hit.Entity;

                            // Check collision with hitboxes
                            HitboxRegion hitRegion = HitboxRegion.Torso;
                            if (hitboxLookup.HasComponent(hitEntityToProcess))
                            {
                                var hitbox = hitboxLookup[hitEntityToProcess];
                                targetEntity = hitbox.OwnerEntity; // Redirect to root player
                                multiplier = hitbox.DamageMultiplier;
                                hitRegion = hitbox.Region;
                            }

                            // Redirect compound collider hits from ROOT → CHILD
                            if (hitboxOwnerLinkLookup.HasComponent(targetEntity))
                                targetEntity = hitboxOwnerLinkLookup[targetEntity].HitboxOwner;

                            // Calculate Damage
                            float finalDamage = projRef.Damage * multiplier;

                            // Apply DamageEvent if target can receive damage
                            if (finalDamage > 0 && damageBufferLookup.HasBuffer(targetEntity))
                            {
                                ecb.AppendToBuffer(targetEntity, new DamageEvent
                                {
                                    Amount = finalDamage,
                                    SourceEntity = projRef.Owner,
                                    HitPosition = hit.Position,
                                    ServerTick = currentTick,
                                    Type = survivalElement
                                });
                            }

                            // EPIC 15.28: Create PendingCombatHit for combat resolution (server only)
                            if (state.WorldUnmanaged.IsServer())
                            {
                                var combatHitEntity = ecb.CreateEntity();
                                ecb.AddComponent(combatHitEntity, new PendingCombatHit
                                {
                                    AttackerEntity = projRef.Owner,
                                    TargetEntity = targetEntity,
                                    WeaponEntity = entity,
                                    HitPoint = hit.Position,
                                    HitNormal = hit.SurfaceNormal,
                                    HitDistance = math.distance(rayInput.Start, hit.Position),
                                    WasPhysicsHit = true,
                                    ResolverType = CombatResolverType.Hybrid,
                                    HitRegion = hitRegion,
                                    HitboxMultiplier = multiplier,
                                    DamagePreApplied = true,
                                    AttackDirection = math.normalizesafe(hit.Position - rayInput.Start),
                                    WeaponData = new WeaponStats
                                    {
                                        BaseDamage = projRef.Damage,
                                        DamageType = themeElement,
                                        CanCrit = true
                                    }
                                });
                            }
                        }
                    }
                }
                else
                {
                    transformRef.Position = newPos;
                }

                // Update rotation to face velocity
                if (math.lengthsq(moveRef.Velocity) > 0.01f)
                {
                    transformRef.Rotation = quaternion.LookRotation(math.normalize(moveRef.Velocity), math.up());
                }
            }
            
            // Cleanup impacted projectiles after delay
            // EPIC 15.10: Skip explosive projectiles - VoxelDetonationSystem destroys them after explosion
            foreach (var (projectile, impacted, entity) in
                     SystemAPI.Query<RefRO<Projectile>, RefRO<ProjectileImpacted>>()
                     .WithEntityAccess())
            {
                // Don't cleanup if waiting for explosion (DetonateOnImpact OR DetonateOnTimer but not yet detonated)
                // Fix: Grenades (Timer) were being destroyed on impact because they lacked DetonateOnImpact
                bool isWaitingForExplosion = (detonateOnImpactLookup.HasComponent(entity) || detonateOnTimerLookup.HasComponent(entity)) 
                                             && !projectileDetonatedLookup.HasComponent(entity);

                if (isWaitingForExplosion)
                {
                    continue;
                }

                // Destroy after short delay (for VFX to play)
                // Only destroy on server - client ghosts are destroyed automatically when server despawns them
                if (projectile.ValueRO.ElapsedTime > projectile.ValueRO.Lifetime * 0.1f + 0.5f && state.WorldUnmanaged.IsServer())
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
