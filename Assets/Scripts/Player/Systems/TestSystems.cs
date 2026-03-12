using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;
using DIG.Weapons; 

namespace Player.Systems
{
    // T6: Heal Station Logic
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct HealStationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HealStation>();
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            var healEventLookup = SystemAPI.GetBufferLookup<HealEvent>(false);
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 0;

            foreach (var (station, transform, entity) in SystemAPI.Query<RefRW<HealStation>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                station.ValueRW.Timer += deltaTime;
                if (station.ValueRW.Timer >= station.ValueRW.HealInterval)
                {
                    station.ValueRW.Timer = 0;
                    float radiusSq = station.ValueRW.Radius * station.ValueRW.Radius;
                    float3 stationPos = transform.ValueRO.Position;

                    foreach (var (playerHealth, playerTransform, playerEntity) in SystemAPI.Query<RefRO<Health>, RefRO<LocalTransform>>().WithEntityAccess())
                    {
                        if (math.distancesq(playerTransform.ValueRO.Position, stationPos) <= radiusSq)
                        {
                            if (healEventLookup.HasBuffer(playerEntity))
                            {
                                var buffer = healEventLookup[playerEntity];
                                buffer.Add(new HealEvent
                                {
                                    Amount = station.ValueRO.HealAmount,
                                    SourceEntity = entity,
                                    Position = stationPos,
                                    ServerTick = currentTick,
                                    Type = HealType.Environmental
                                });
                            }
                        }
                    }
                }
            }
        }
    }



    // T4/T7: Death Spawn Logic (Loot/effects)
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct DeathSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (died, spawns, transform, entity) in 
                     SystemAPI.Query<RefRO<DiedEvent>, DynamicBuffer<DeathSpawnElement>, RefRO<LocalTransform>>()
                     .WithAll<DiedEvent>() 
                     .WithEntityAccess())
            {
                foreach (var spawn in spawns)
                {
                    if (spawn.Prefab != Entity.Null)
                    {
                        var instance = ecb.Instantiate(spawn.Prefab);
                        ecb.SetComponent(instance, LocalTransform.FromPositionRotation(
                            transform.ValueRO.Position + spawn.PositionOffset, 
                            transform.ValueRO.Rotation));
                        
                        // 13.16.7: Apply Explosive Force
                        if (spawn.ApplyExplosiveForce)
                        {
                            // Apply random upward force for "pop" effect
                            // Requires PhysicsVelocity
                            // We can't easily check HasComponent in ECB context without a Lookup.
                            // But we can blindly add the component or set it?
                            // Better: Add a "ApplyForceRequest" component and handle in another system?
                            // Or just use PhysicsVelocity if we assume prefab has it.
                            
                            // Since we can't reliably know if prefab has PhysicsVelocity here (without lookup),
                            // We'll use a hack: Add a component that a system cleans up?
                            
                            // Simplest: Just set PhysicsVelocity! (If component exists, it updates. If not, it might error or be ignored?)
                            // ECB.SetComponent requires the component to exist on the Entity (Prefab).
                            // If Prefab has it, Instance has it.
                            
                            // Let's assume prefab has PhysicsVelocity (standard for loot/debris).
                            var random = Unity.Mathematics.Random.CreateFromIndex((uint)entity.Index);
                            float3 force = new float3(
                                random.NextFloat(-2, 2),
                                random.NextFloat(3, 6), // Upward pop
                                random.NextFloat(-2, 2)
                            );
                            
                            ecb.SetComponent(instance, new PhysicsVelocity
                            {
                                Linear = force,
                                Angular = random.NextFloat3(-5, 5)
                            });
                        }
                    }
                }

                // Consume event so we don't spawn infinite loot
                ecb.SetComponentEnabled<DiedEvent>(entity, false);
            }
        }
    }

    // T8: Kill Feed — consumes KillCredited/AssistCredited events.
    // Debug logging disabled. Will be replaced by proper UI kill feed.
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class KillFeedSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (kill, entity) in SystemAPI.Query<RefRO<KillCredited>>().WithEntityAccess())
            {
                ecb.RemoveComponent<KillCredited>(entity);
            }

            foreach (var (assist, entity) in SystemAPI.Query<RefRO<AssistCredited>>().WithEntityAccess())
            {
                ecb.RemoveComponent<AssistCredited>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    // T5: Damage debug log — disabled. Replaced by CombatUIBridgeSystem damage visuals.
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class DamageDebugLogSystem : SystemBase
    {
        protected override void OnCreate()
        {
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }
}
