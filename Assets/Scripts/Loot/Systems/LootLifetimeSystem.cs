using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Loot.Components;

namespace DIG.Loot.Systems
{
    /// <summary>
    /// EPIC 16.6: Destroys loot entities after their lifetime expires.
    /// Prevents loot from accumulating indefinitely in the world.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct LootLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (lifetime, entity) in
                     SystemAPI.Query<RefRO<LootLifetimeECS>>()
                     .WithAll<LootEntity>()
                     .WithEntityAccess())
            {
                if (currentTime - lifetime.ValueRO.SpawnTime > lifetime.ValueRO.Lifetime)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
