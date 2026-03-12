using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Validation;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Receives crafting RPCs on server, resolves ghost IDs to station entities,
    /// and writes into transient request buffers on the station.
    /// Follows VoxelDamageRpcReceiveSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CraftRpcReceiveSystem : SystemBase
    {
        private EntityQuery _craftRequestRpcQuery;
        private EntityQuery _collectRpcQuery;
        private EntityQuery _cancelRpcQuery;

        protected override void OnCreate()
        {
            _craftRequestRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<CraftRequestRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _collectRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<CollectCraftRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _cancelRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<CancelCraftRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process CraftRequestRpc
            var craftEntities = _craftRequestRpcQuery.ToEntityArray(Allocator.Temp);
            var craftRpcs = _craftRequestRpcQuery.ToComponentDataArray<CraftRequestRpc>(Allocator.Temp);
            var craftReceives = _craftRequestRpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < craftEntities.Length; i++)
            {
                var playerEntity = ResolvePlayer(craftReceives[i].SourceConnection);

                // --- ANTI-CHEAT: Rate limit check ---
                if (playerEntity != Entity.Null && EntityManager.HasComponent<ValidationLink>(playerEntity))
                {
                    var valChild = EntityManager.GetComponentData<ValidationLink>(playerEntity).ValidationChild;
                    if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.CRAFT_REQUEST))
                    {
                        RateLimitHelper.CreateViolation(EntityManager, playerEntity,
                            ViolationType.RateLimit, 0.5f, RpcTypeIds.CRAFT_REQUEST, 0);
                        ecb.DestroyEntity(craftEntities[i]);
                        continue;
                    }
                }
                // --- END ANTI-CHEAT ---

                var stationEntity = ResolveGhostEntity(craftRpcs[i].StationGhostId);

                if (playerEntity != Entity.Null && stationEntity != Entity.Null
                    && EntityManager.HasBuffer<CraftRequest>(stationEntity))
                {
                    var buffer = EntityManager.GetBuffer<CraftRequest>(stationEntity);
                    buffer.Add(new CraftRequest
                    {
                        RecipeId = craftRpcs[i].RecipeId,
                        RequestingPlayer = playerEntity
                    });
                }

                ecb.DestroyEntity(craftEntities[i]);
            }

            craftEntities.Dispose();
            craftRpcs.Dispose();
            craftReceives.Dispose();

            // Process CollectCraftRpc
            var collectEntities = _collectRpcQuery.ToEntityArray(Allocator.Temp);
            var collectRpcs = _collectRpcQuery.ToComponentDataArray<CollectCraftRpc>(Allocator.Temp);
            var collectReceives = _collectRpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < collectEntities.Length; i++)
            {
                var playerEntity = ResolvePlayer(collectReceives[i].SourceConnection);
                var stationEntity = ResolveGhostEntity(collectRpcs[i].StationGhostId);

                if (playerEntity != Entity.Null && stationEntity != Entity.Null
                    && EntityManager.HasBuffer<CollectCraftRequest>(stationEntity))
                {
                    var buffer = EntityManager.GetBuffer<CollectCraftRequest>(stationEntity);
                    buffer.Add(new CollectCraftRequest
                    {
                        OutputIndex = collectRpcs[i].OutputIndex,
                        RequestingPlayer = playerEntity
                    });
                }

                ecb.DestroyEntity(collectEntities[i]);
            }

            collectEntities.Dispose();
            collectRpcs.Dispose();
            collectReceives.Dispose();

            // Process CancelCraftRpc
            var cancelEntities = _cancelRpcQuery.ToEntityArray(Allocator.Temp);
            var cancelRpcs = _cancelRpcQuery.ToComponentDataArray<CancelCraftRpc>(Allocator.Temp);
            var cancelReceives = _cancelRpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < cancelEntities.Length; i++)
            {
                var playerEntity = ResolvePlayer(cancelReceives[i].SourceConnection);
                var stationEntity = ResolveGhostEntity(cancelRpcs[i].StationGhostId);

                if (playerEntity != Entity.Null && stationEntity != Entity.Null
                    && EntityManager.HasBuffer<CancelCraftRequest>(stationEntity))
                {
                    var buffer = EntityManager.GetBuffer<CancelCraftRequest>(stationEntity);
                    buffer.Add(new CancelCraftRequest
                    {
                        QueueIndex = cancelRpcs[i].QueueIndex,
                        RequestingPlayer = playerEntity
                    });
                }

                ecb.DestroyEntity(cancelEntities[i]);
            }

            cancelEntities.Dispose();
            cancelRpcs.Dispose();
            cancelReceives.Dispose();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private Entity ResolvePlayer(Entity sourceConnection)
        {
            if (sourceConnection == Entity.Null) return Entity.Null;
            if (!SystemAPI.HasComponent<CommandTarget>(sourceConnection)) return Entity.Null;
            return SystemAPI.GetComponent<CommandTarget>(sourceConnection).targetEntity;
        }

        private Entity ResolveGhostEntity(int ghostId)
        {
            foreach (var (ghost, entity) in SystemAPI.Query<RefRO<GhostInstance>>().WithEntityAccess())
            {
                if (ghost.ValueRO.ghostId == ghostId)
                    return entity;
            }
            return Entity.Null;
        }
    }
}
