using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;

namespace DIG.Loot.Systems
{
    /// <summary>
    /// EPIC 16.6: Processes DeathSpawnElement buffer on dead entities.
    /// Instantiates death VFX/gibs from authored prefabs at corpse position.
    /// Completes the existing DeathSpawnElement pipeline that was authored but never consumed.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DeathLootSystem))]
    public partial class DeathSpawnProcessingSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (spawnBuffer, transform, entity) in
                     SystemAPI.Query<DynamicBuffer<DeathSpawnElement>, RefRO<LocalTransform>>()
                     .WithAll<DiedEvent>()
                     .WithEntityAccess())
            {
                if (spawnBuffer.Length == 0) continue;

                float3 basePos = transform.ValueRO.Position;

                for (int i = 0; i < spawnBuffer.Length; i++)
                {
                    var element = spawnBuffer[i];
                    if (element.Prefab == Entity.Null) continue;

                    var spawned = ecb.Instantiate(element.Prefab);
                    float3 spawnPos = basePos + element.PositionOffset;

                    ecb.SetComponent(spawned, LocalTransform.FromPosition(spawnPos));

                    if (element.ApplyExplosiveForce)
                    {
                        // Apply upward + radial scatter velocity
                        ecb.SetComponent(spawned, new PhysicsVelocity
                        {
                            Linear = new float3(0f, 5f, 0f), // Upward burst
                            Angular = new float3(
                                math.sin(entity.Index * 0.7f) * 3f,
                                0f,
                                math.cos(entity.Index * 0.7f) * 3f
                            )
                        });
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
