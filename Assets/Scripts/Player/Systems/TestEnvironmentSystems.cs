using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;
using DIG.Weapons;

namespace Player.Systems
{
    // T2/T8: Turret Logic
    // Fires damage at nearest player periodically
    // Also ensures we can track attribution (SourceEntity = Turret Entity)
    // DISABLED: Test-only system. Enable manually via World.GetExistingSystem if needed.
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct TestTurretSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Complete dependencies before accessing Lookup on main thread
            state.Dependency.Complete();

            float deltaTime = SystemAPI.Time.DeltaTime;
            var damageBufferLookup = SystemAPI.GetBufferLookup<DamageEvent>(false);
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 0;

            foreach (var (turret, transform, entity) in SystemAPI.Query<RefRW<TestDamageSource>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                turret.ValueRW.Timer += deltaTime;
                if (turret.ValueRW.Timer >= turret.ValueRW.Interval)
                {
                    turret.ValueRW.Timer = 0;
                    float rangeSq = turret.ValueRW.Range * turret.ValueRW.Range;
                    float3 turretPos = transform.ValueRO.Position;

                    // Find nearest player
                    Entity nearestTarget = Entity.Null;
                    float nearestDistSq = float.MaxValue;

                    foreach (var (health, targetTransform, targetEntity) in SystemAPI.Query<RefRO<Health>, RefRO<LocalTransform>>().WithAll<PlayerTag>().WithEntityAccess())
                    {
                        if (health.ValueRO.Current <= 0) continue;

                        float dSq = math.distancesq(targetTransform.ValueRO.Position, turretPos);
                        if (dSq <= rangeSq && dSq < nearestDistSq)
                        {
                            nearestDistSq = dSq;
                            nearestTarget = targetEntity;
                        }
                    }

                    // Apply Damage
                    if (nearestTarget != Entity.Null)
                    {
                        if (damageBufferLookup.HasBuffer(nearestTarget))
                        {
                            var buffer = damageBufferLookup[nearestTarget];
                            buffer.Add(new DamageEvent
                            {
                                Amount = turret.ValueRO.DamageAmount,
                                SourceEntity = entity, // Self is source for attribution
                                HitPosition = turretPos,
                                ServerTick = currentTick,
                                Type = turret.ValueRO.Type
                            });
                            
                            // Debug
                            // UnityEngine.Debug.Log($"Turret {entity.Index} shot {nearestTarget.Index}");
                        }
                    }
                }
            }
        }
    }

    // T3: Damage Zone Logic
    // Applies DOT to players inside
    // DISABLED: Test-only system. Enable manually via World.GetExistingSystem if needed.
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct TestDamageZoneSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Complete dependencies before accessing Lookup on main thread
            state.Dependency.Complete();

            float deltaTime = SystemAPI.Time.DeltaTime;
            var damageBufferLookup = SystemAPI.GetBufferLookup<DamageEvent>(false);
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 0;

            foreach (var (zone, transform, entity) in SystemAPI.Query<RefRO<TestDamageZone>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                float radiusSq = zone.ValueRO.Radius * zone.ValueRO.Radius;
                float3 zonePos = transform.ValueRO.Position;
                float damageThisFrame = zone.ValueRO.DamagePerSecond * deltaTime;

                foreach (var (health, targetTransform, targetEntity) in SystemAPI.Query<RefRO<Health>, RefRO<LocalTransform>>().WithAll<PlayerTag>().WithEntityAccess())
                {
                    if (math.distancesq(targetTransform.ValueRO.Position, zonePos) <= radiusSq)
                    {
                        if (damageBufferLookup.HasBuffer(targetEntity))
                        {
                            var buffer = damageBufferLookup[targetEntity];
                            // Note: Adding micro-damage every frame can overflow buffers or spam events.
                            // Better: Accumulate? Or just add with low value.
                            // For test environment, constant stream is fine.
                            
                            buffer.Add(new DamageEvent
                            {
                                Amount = damageThisFrame,
                                SourceEntity = entity,
                                HitPosition = zonePos,
                                ServerTick = currentTick,
                                Type = zone.ValueRO.Type
                            });
                        }
                    }
                }
            }
        }
    }
}
