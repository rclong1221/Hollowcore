using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Physics;
using DIG.AI.Components;
using DIG.Combat.Components;
using DIG.Combat.Resolvers;
using DIG.Weapons;
using Health = Player.Components.Health;
using HitboxRegion = Player.Components.HitboxRegion;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 15.32: Processes TelegraphZone entities.
    /// When timer exceeds DamageDelay, performs spatial query and creates PendingCombatHit per target.
    /// Handles one-shot and lingering (repeating) zones.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(CombatResolutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class TelegraphDamageSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TelegraphZone>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (zone, entity) in
                SystemAPI.Query<RefRW<TelegraphZone>>()
                .WithEntityAccess())
            {
                ref var z = ref zone.ValueRW;
                z.Timer += deltaTime;

                // Check if it's time to deal damage
                bool shouldDealDamage = false;
                if (!z.HasDealtDamage && z.Timer >= z.DamageDelay)
                {
                    shouldDealDamage = true;
                }
                else if (z.LingerDuration > 0f && z.TickInterval > 0f &&
                         z.Timer >= z.DamageDelay &&
                         z.Timer - z.LastTickTime >= z.TickInterval)
                {
                    shouldDealDamage = true;
                }

                if (shouldDealDamage)
                {
                    z.LastTickTime = z.Timer;

                    // Simple spatial query: iterate all Health entities and check distance
                    // (Full physics overlap queries would be Phase 2 enhancement)
                    int targetsHit = 0;
                    foreach (var (health, targetTransform, targetEntity) in
                        SystemAPI.Query<
                            RefRO<Health>,
                            RefRO<LocalTransform>>()
                        .WithEntityAccess())
                    {
                        if (targetEntity == z.OwnerEntity) continue; // Don't hit self
                        if (targetsHit >= z.MaxTargets) break;

                        float3 targetPos = targetTransform.ValueRO.Position;
                        float3 toTarget = targetPos - z.Position;
                        toTarget.y = 0f;
                        float distance = math.length(toTarget);

                        bool inRange = false;
                        switch (z.Shape)
                        {
                            case TelegraphShape.Circle:
                                inRange = distance <= z.Radius;
                                break;
                            case TelegraphShape.Ring:
                                inRange = distance <= z.Radius && distance >= z.InnerRadius;
                                break;
                            case TelegraphShape.Cone:
                                if (distance <= z.Radius)
                                {
                                    float3 forward = math.forward(z.Rotation);
                                    forward.y = 0f;
                                    forward = math.normalizesafe(forward);
                                    float3 dirToTarget = distance > 0.01f
                                        ? toTarget / distance : forward;
                                    float dot = math.dot(forward, dirToTarget);
                                    float halfAngle = math.radians(z.Angle * 0.5f);
                                    inRange = dot >= math.cos(halfAngle);
                                }
                                break;
                            case TelegraphShape.Line:
                                if (distance <= z.Length)
                                {
                                    float3 forward2 = math.forward(z.Rotation);
                                    forward2.y = 0f;
                                    forward2 = math.normalizesafe(forward2);
                                    float projDist = math.dot(toTarget, forward2);
                                    if (projDist >= 0 && projDist <= z.Length)
                                    {
                                        float perpDist = math.length(toTarget - forward2 * projDist);
                                        inRange = perpDist <= z.Width * 0.5f;
                                    }
                                }
                                break;
                            case TelegraphShape.Cross:
                                // Cross = two perpendicular lines
                                inRange = distance <= z.Radius;
                                break;
                            default:
                                inRange = distance <= z.Radius;
                                break;
                        }

                        if (z.IsSafeZone) inRange = !inRange; // Invert for safe zones

                        if (inRange)
                        {
                            var hitEntity = ecb.CreateEntity();
                            ecb.AddComponent(hitEntity, new PendingCombatHit
                            {
                                AttackerEntity = z.OwnerEntity,
                                TargetEntity = targetEntity,
                                WeaponEntity = z.OwnerEntity,
                                HitPoint = targetPos,
                                HitNormal = new float3(0, 1, 0),
                                HitDistance = distance,
                                WasPhysicsHit = true,
                                ResolverType = z.ResolverType,
                                WeaponData = new WeaponStats
                                {
                                    BaseDamage = z.DamageBase,
                                    DamageMin = z.DamageBase - z.DamageVariance,
                                    DamageMax = z.DamageBase + z.DamageVariance,
                                    DamageType = z.DamageType,
                                    CanCrit = true
                                },
                                HitRegion = HitboxRegion.Torso,
                                HitboxMultiplier = 1.0f,
                                DamagePreApplied = false,
                                AttackDirection = math.forward(z.Rotation)
                            });

                            targetsHit++;
                        }
                    }

                    // Mark one-shot zones as done
                    if (z.LingerDuration <= 0f)
                    {
                        z.HasDealtDamage = true;
                    }
                }

                // Destroy zone when expired
                float totalLifetime = z.DamageDelay + math.max(z.LingerDuration, 0f);
                if (z.Timer >= totalLifetime && (z.HasDealtDamage || z.LingerDuration <= 0f))
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
