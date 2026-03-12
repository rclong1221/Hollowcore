using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Validation;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Receives StatAllocationRpc from clients on server.
    /// Resolves player entity via CommandTarget, writes StatAllocationRequest buffer.
    /// Server-only — clients send RPCs, server validates and processes.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(StatAllocationSystem))]
    public partial class StatAllocationRpcReceiveSystem : SystemBase
    {
        private EntityQuery _rpcQuery;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<StatAllocationRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var commandTargetLookup = GetComponentLookup<CommandTarget>(true);
            var progressionLookup = GetComponentLookup<PlayerProgression>(true);

            var entities = _rpcQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _rpcQuery.ToComponentDataArray<StatAllocationRpc>(Allocator.Temp);
            var receives = _rpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var rpc = rpcs[i];
                var connection = receives[i].SourceConnection;

                // Resolve player entity from connection
                if (connection == Entity.Null || !commandTargetLookup.HasComponent(connection))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                var playerEntity = commandTargetLookup[connection].targetEntity;
                if (playerEntity == Entity.Null || !progressionLookup.HasComponent(playerEntity))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                // --- ANTI-CHEAT: Rate limit check ---
                if (EntityManager.HasComponent<ValidationLink>(playerEntity))
                {
                    var valChild = EntityManager.GetComponentData<ValidationLink>(playerEntity).ValidationChild;
                    if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.STAT_ALLOCATION))
                    {
                        RateLimitHelper.CreateViolation(EntityManager, playerEntity,
                            ViolationType.RateLimit, 0.7f, RpcTypeIds.STAT_ALLOCATION, 0);
                        ecb.DestroyEntity(entities[i]);
                        continue;
                    }
                }
                // --- END ANTI-CHEAT ---

                // Validate attribute type
                if (rpc.Attribute > (byte)StatAttributeType.Vitality || rpc.Points <= 0)
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                // Validate points available
                var prog = progressionLookup[playerEntity];
                if (rpc.Points > prog.UnspentStatPoints)
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                // Write to request buffer
                if (SystemAPI.HasBuffer<StatAllocationRequest>(playerEntity))
                {
                    var buffer = SystemAPI.GetBuffer<StatAllocationRequest>(playerEntity);
                    buffer.Add(new StatAllocationRequest
                    {
                        Attribute = (StatAttributeType)rpc.Attribute,
                        Points = rpc.Points
                    });
                }

                ecb.DestroyEntity(entities[i]);
            }

            entities.Dispose();
            rpcs.Dispose();
            receives.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
