using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Component requesting a tool to be spawned for a player.
    /// Add this to player entity to request tool spawning.
    /// </summary>
    public struct SpawnToolRequest : IBufferElementData
    {
        /// <summary>
        /// The tool prefab entity to instantiate.
        /// </summary>
        public Entity ToolPrefab;

        /// <summary>
        /// The slot to place the tool in (0-4).
        /// </summary>
        public int SlotIndex;
    }

    /// <summary>
    /// Spawns tool entities for players that have pending spawn requests.
    /// Runs on server only (authoritative spawning).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ToolSpawnSystem : ISystem
    {
        private BufferLookup<SpawnToolRequest> _spawnRequestLookup;
        private BufferLookup<ToolOwnership> _ownershipLookup;
        private EntityQuery _playersWithRequestsQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _spawnRequestLookup = state.GetBufferLookup<SpawnToolRequest>();
            _ownershipLookup = state.GetBufferLookup<ToolOwnership>();

            _playersWithRequestsQuery = state.GetEntityQuery(
                ComponentType.ReadWrite<SpawnToolRequest>(),
                ComponentType.ReadWrite<ToolOwnership>()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            _spawnRequestLookup.Update(ref state);
            _ownershipLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = _playersWithRequestsQuery.ToEntityArray(Allocator.Temp);

            foreach (var playerEntity in entities)
            {
                var spawnRequests = _spawnRequestLookup[playerEntity];
                var ownership = _ownershipLookup[playerEntity];

                // Skip if no requests
                if (spawnRequests.Length == 0)
                    continue;

                // Process each spawn request
                for (int i = spawnRequests.Length - 1; i >= 0; i--)
                {
                    var request = spawnRequests[i];

                    if (request.ToolPrefab == Entity.Null)
                        continue;

                    // Spawn the tool entity
                    var toolEntity = ecb.Instantiate(request.ToolPrefab);

                    // Set the tool owner
                    ecb.SetComponent(toolEntity, new ToolOwner
                    {
                        OwnerEntity = playerEntity
                    });

                    // Update ownership buffer to reference the new tool
                    // Find existing slot or add new
                    bool foundSlot = false;
                    for (int j = 0; j < ownership.Length; j++)
                    {
                        if (ownership[j].SlotIndex == request.SlotIndex)
                        {
                            var ownershipEntry = ownership[j];
                            ownershipEntry.ToolEntity = toolEntity;
                            ownership[j] = ownershipEntry;
                            foundSlot = true;
                            break;
                        }
                    }

                    if (!foundSlot && ownership.Length < 5)
                    {
                        ownership.Add(new ToolOwnership
                        {
                            ToolEntity = toolEntity,
                            SlotIndex = request.SlotIndex
                        });
                    }
                }

                // Clear processed requests
                spawnRequests.Clear();
            }

            entities.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Cleans up tool entities when their owner is destroyed.
    /// Runs on server to properly despawn tools.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ToolCleanupSystem : ISystem
    {
        private EntityQuery _destroyedOwnersQuery;

        public void OnCreate(ref SystemState state)
        {
            // This would track destroyed players and cleanup their tools
            // Implementation depends on how player destruction is handled
        }

        public void OnUpdate(ref SystemState state)
        {
            // Cleanup orphaned tools (tools whose owner no longer exists)
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (owner, entity) in
                     SystemAPI.Query<RefRO<ToolOwner>>()
                     .WithAll<Tool>()
                     .WithEntityAccess())
            {
                // Check if owner still exists
                if (owner.ValueRO.OwnerEntity == Entity.Null ||
                    !state.EntityManager.Exists(owner.ValueRO.OwnerEntity))
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
