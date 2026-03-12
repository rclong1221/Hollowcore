using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Resources
{
    /// <summary>
    /// Respawns depleted resource nodes after their respawn timer expires.
    /// Server-authoritative.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ResourceNodeRespawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (node, depleted, entity) in
                     SystemAPI.Query<RefRW<ResourceNode>, RefRW<ResourceNodeDepleted>>()
                     .WithEntityAccess())
            {
                ref var dep = ref depleted.ValueRW;
                ref var n = ref node.ValueRW;

                // Skip if no respawn configured
                if (n.RespawnTime <= 0f)
                    continue;

                // Update depletion timer
                dep.TimeSinceDepletion += deltaTime;

                // Check if respawn time reached
                if (dep.TimeSinceDepletion >= n.RespawnTime)
                {
                    // Respawn the node
                    n.Amount = n.MaxAmount;

                    // Remove depleted tag
                    ecb.RemoveComponent<ResourceNodeDepleted>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
