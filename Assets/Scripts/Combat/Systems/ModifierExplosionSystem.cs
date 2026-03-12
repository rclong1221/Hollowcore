using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;
using Unity.Transforms;
using DIG.Combat.Components;
using DIG.Combat.Utility;
using DIG.Combat.Knockback;
using Player.Components;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 15.29: Processes ModifierExplosionRequest entities created by
    /// CombatResolutionSystem's modifier processing.
    /// Applies area damage with distance falloff at the explosion point.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatResolutionSystem))]
    public partial struct ModifierExplosionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModifierExplosionRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var damageBufferLookup = SystemAPI.GetBufferLookup<DamageEvent>(false);
            var hitboxLookup = SystemAPI.GetComponentLookup<Hitbox>(true);
            var hitboxOwnerLinkLookup = SystemAPI.GetComponentLookup<HitboxOwnerLink>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Unity.Transforms.Parent>(true);
            var networkTime = SystemAPI.GetSingleton<Unity.NetCode.NetworkTime>();
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            foreach (var (request, entity) in SystemAPI.Query<RefRO<ModifierExplosionRequest>>().WithEntityAccess())
            {
                var explosion = request.ValueRO;
                var survivalElement = DamageTypeConverter.ToSurvival(explosion.Element);

                // Physics overlap sphere to find targets in radius
                var hits = new NativeList<DistanceHit>(Allocator.Temp);
                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                };

                if (physicsWorld.OverlapSphere(explosion.Position, explosion.Radius, ref hits, filter))
                {
                    var damagedEntities = new NativeHashSet<Entity>(hits.Length, Allocator.Temp);

                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        Entity targetEntity = hit.Entity;

                        // Skip source entity
                        if (targetEntity == explosion.SourceEntity) continue;

                        // Resolve to damageable entity
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

                        if (!damageBufferLookup.HasBuffer(actualTarget))
                        {
                            // Walk parent chain
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

                        // Skip duplicates
                        if (damagedEntities.Contains(actualTarget)) continue;
                        damagedEntities.Add(actualTarget);

                        // Distance-based falloff (quadratic)
                        float distance = hit.Distance;
                        float normalizedDistance = math.saturate(distance / explosion.Radius);
                        float falloff = math.pow(1.0f - normalizedDistance, 2f);
                        float finalDamage = explosion.Damage * multiplier * falloff;

                        if (finalDamage > 0 && damageBufferLookup.HasBuffer(actualTarget))
                        {
                            ecb.AppendToBuffer(actualTarget, new DamageEvent
                            {
                                Amount = finalDamage,
                                SourceEntity = explosion.SourceEntity,
                                HitPosition = explosion.Position,
                                ServerTick = currentTick,
                                Type = survivalElement
                            });
                        }

                        // EPIC 16.9: Create knockback request if force > 0
                        if (explosion.KnockbackForce > 0f)
                        {
                            float3 targetPos = hit.Position;
                            float3 kbDir = math.normalizesafe(targetPos - explosion.Position, new float3(0, 1, 0));
                            var kbEntity = ecb.CreateEntity();
                            ecb.AddComponent(kbEntity, new KnockbackRequest
                            {
                                TargetEntity = actualTarget,
                                SourceEntity = explosion.SourceEntity,
                                Direction = kbDir,
                                Force = explosion.KnockbackForce,
                                Type = KnockbackType.Push,
                                Falloff = KnockbackFalloff.Quadratic,
                                Distance = distance,
                                MaxRadius = explosion.Radius,
                                Easing = KnockbackEasing.EaseOut,
                                TriggersInterrupt = true
                            });
                        }
                    }

                    damagedEntities.Dispose();
                }

                hits.Dispose();

                // Destroy the request entity
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
