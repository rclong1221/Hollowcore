using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Combat.Components;
using DIG.Survival.Explosives;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Creates KnockbackRequest entities for entities within placed explosive blast radius.
    /// Reads ExplosionEvent (replicated) and ExplosiveStats to compute radial knockback.
    /// Runs Server|Local only (matching ExplosiveDetonationSystem).
    ///
    /// Note: Projectile/grenade explosions are handled by ModifierExplosionSystem (already wired).
    /// This system only handles placed explosives (C4, mines, etc.) that go through ExplosiveDetonationSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ExplosionKnockbackSystem : SystemBase
    {
        private EntityQuery _explosionEventQuery;
        private uint _lastProcessedTick;

        protected override void OnCreate()
        {
            _explosionEventQuery = GetEntityQuery(
                ComponentType.ReadOnly<ExplosionEvent>()
            );
            RequireForUpdate(_explosionEventQuery);
        }

        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            // Skip if we already processed this tick (prevents double-processing)
            if (currentTick == _lastProcessedTick && currentTick != 0) return;
            _lastProcessedTick = currentTick;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var knockbackStateLookup = SystemAPI.GetComponentLookup<KnockbackState>(true);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var hitboxOwnerLinkLookup = SystemAPI.GetComponentLookup<HitboxOwnerLink>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Unity.Transforms.Parent>(true);

            foreach (var (explosionEvent, entity) in
                SystemAPI.Query<RefRO<ExplosionEvent>>().WithEntityAccess())
            {
                var explosion = explosionEvent.ValueRO;

                // Only process events from this tick (ExplosionEvents persist for 10 ticks)
                if (explosion.Tick.IsValid && explosion.Tick.TickIndexForValidTick != currentTick)
                    continue;

                // Use explosion's PhysicsForce — read from ExplosionEvent if available, otherwise use default
                float physicsForce = explosion.BlastRadius * 200f; // Approximate: radius * 200 = force in Newtons

                if (physicsForce <= 0f) continue;

                // OverlapSphere to find all entities in blast radius
                var hits = new NativeList<DistanceHit>(Allocator.Temp);
                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                };

                if (physicsWorld.OverlapSphere(explosion.Position, explosion.BlastRadius, ref hits, filter))
                {
                    var processedEntities = new NativeHashSet<Entity>(hits.Length, Allocator.Temp);

                    for (int i = 0; i < hits.Length; i++)
                    {
                        Entity hitEntity = hits[i].Entity;

                        // Resolve to knockback-capable entity
                        Entity actualTarget = hitEntity;

                        // Redirect compound collider hits ROOT → CHILD via HitboxOwnerLink
                        if (hitboxOwnerLinkLookup.HasComponent(actualTarget))
                            actualTarget = hitboxOwnerLinkLookup[actualTarget].HitboxOwner;

                        // Walk parent chain if needed
                        if (!knockbackStateLookup.HasComponent(actualTarget))
                        {
                            Entity current = hitEntity;
                            int maxDepth = 10;
                            while (maxDepth-- > 0 && parentLookup.HasComponent(current))
                            {
                                current = parentLookup[current].Value;
                                if (knockbackStateLookup.HasComponent(current))
                                {
                                    actualTarget = current;
                                    break;
                                }
                            }
                        }

                        // Must have KnockbackState to receive knockback
                        if (!knockbackStateLookup.HasComponent(actualTarget)) continue;

                        // Skip duplicates
                        if (processedEntities.Contains(actualTarget)) continue;
                        processedEntities.Add(actualTarget);

                        // Compute direction and distance
                        float3 targetPos;
                        if (transformLookup.HasComponent(actualTarget))
                            targetPos = transformLookup[actualTarget].Position;
                        else
                            continue;

                        float distance = math.length(targetPos - explosion.Position);
                        float3 direction = math.normalizesafe(targetPos - explosion.Position, new float3(0, 1, 0));

                        // Create knockback request
                        var kbEntity = ecb.CreateEntity();
                        ecb.AddComponent(kbEntity, new KnockbackRequest
                        {
                            TargetEntity = actualTarget,
                            SourceEntity = Entity.Null, // Placed explosive — source is environmental
                            Direction = direction,
                            Force = physicsForce,
                            Type = KnockbackType.Push,
                            Falloff = KnockbackFalloff.Quadratic,
                            Distance = distance,
                            MaxRadius = explosion.BlastRadius,
                            Easing = KnockbackEasing.EaseOut,
                            TriggersInterrupt = true
                        });
                    }

                    processedEntities.Dispose();
                }

                hits.Dispose();
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
