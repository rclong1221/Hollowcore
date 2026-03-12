using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Validation;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Server-only system that validates TalentAllocationRpc and TalentRespecRpc.
    /// Resolves player entity via CommandTarget, validates prerequisites/points,
    /// writes to TalentAllocationRequest buffer. Follows StatAllocationRpcReceiveSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TalentAllocationSystem))]
    public partial class TalentRpcReceiveSystem : SystemBase
    {
        private EntityQuery _allocRpcQuery;
        private EntityQuery _respecRpcQuery;

        protected override void OnCreate()
        {
            _allocRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<TalentAllocationRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _respecRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<TalentRespecRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
        }

        protected override void OnUpdate()
        {
            if (!_allocRpcQuery.IsEmptyIgnoreFilter)
                ProcessAllocationRpcs();

            if (!_respecRpcQuery.IsEmptyIgnoreFilter)
                ProcessRespecRpcs();
        }

        private void ProcessAllocationRpcs()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var commandTargetLookup = GetComponentLookup<CommandTarget>(true);
            var talentLinkLookup = GetComponentLookup<TalentLink>(true);
            var talentStateLookup = GetComponentLookup<TalentState>(true);

            var entities = _allocRpcQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _allocRpcQuery.ToComponentDataArray<TalentAllocationRpc>(Allocator.Temp);
            var receives = _allocRpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

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
                if (playerEntity == Entity.Null || !talentLinkLookup.HasComponent(playerEntity))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                // --- ANTI-CHEAT: Rate limit check ---
                if (EntityManager.HasComponent<ValidationLink>(playerEntity))
                {
                    var valChild = EntityManager.GetComponentData<ValidationLink>(playerEntity).ValidationChild;
                    if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.TALENT_ALLOCATION))
                    {
                        RateLimitHelper.CreateViolation(EntityManager, playerEntity,
                            ViolationType.RateLimit, 0.7f, RpcTypeIds.TALENT_ALLOCATION, 0);
                        ecb.DestroyEntity(entities[i]);
                        continue;
                    }
                }
                // --- END ANTI-CHEAT ---

                var link = talentLinkLookup[playerEntity];
                if (link.TalentChild == Entity.Null || !talentStateLookup.HasComponent(link.TalentChild))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                // Validate node exists in blob
                if (!SystemAPI.HasSingleton<SkillTreeRegistrySingleton>())
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                var registry = SystemAPI.GetSingleton<SkillTreeRegistrySingleton>();
                if (!FindNodeInBlob(ref registry.Registry.Value, rpc.TreeId, rpc.NodeId))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                // Write request to buffer
                if (SystemAPI.HasBuffer<TalentAllocationRequest>(link.TalentChild))
                {
                    var reqBuffer = SystemAPI.GetBuffer<TalentAllocationRequest>(link.TalentChild);
                    reqBuffer.Add(new TalentAllocationRequest
                    {
                        TreeId = rpc.TreeId,
                        NodeId = rpc.NodeId,
                        RequestType = TalentRequestType.Allocate
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

        private void ProcessRespecRpcs()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var commandTargetLookup = GetComponentLookup<CommandTarget>(true);
            var talentLinkLookup = GetComponentLookup<TalentLink>(true);

            var entities = _respecRpcQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _respecRpcQuery.ToComponentDataArray<TalentRespecRpc>(Allocator.Temp);
            var receives = _respecRpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var rpc = rpcs[i];
                var connection = receives[i].SourceConnection;

                if (connection == Entity.Null || !commandTargetLookup.HasComponent(connection))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                var playerEntity = commandTargetLookup[connection].targetEntity;
                if (playerEntity == Entity.Null || !talentLinkLookup.HasComponent(playerEntity))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                // --- ANTI-CHEAT: Rate limit check ---
                if (EntityManager.HasComponent<ValidationLink>(playerEntity))
                {
                    var valChild = EntityManager.GetComponentData<ValidationLink>(playerEntity).ValidationChild;
                    if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.TALENT_RESPEC))
                    {
                        RateLimitHelper.CreateViolation(EntityManager, playerEntity,
                            ViolationType.RateLimit, 0.9f, RpcTypeIds.TALENT_RESPEC, 0);
                        ecb.DestroyEntity(entities[i]);
                        continue;
                    }
                }
                // --- END ANTI-CHEAT ---

                var link = talentLinkLookup[playerEntity];
                if (link.TalentChild == Entity.Null)
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                // Write respec request to buffer
                if (SystemAPI.HasBuffer<TalentAllocationRequest>(link.TalentChild))
                {
                    var reqBuffer = SystemAPI.GetBuffer<TalentAllocationRequest>(link.TalentChild);
                    reqBuffer.Add(new TalentAllocationRequest
                    {
                        TreeId = rpc.TreeId,
                        NodeId = 0,
                        RequestType = TalentRequestType.Respec
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

        private static bool FindNodeInBlob(ref SkillTreeRegistryBlob blob, ushort treeId, ushort nodeId)
        {
            for (int t = 0; t < blob.Trees.Length; t++)
            {
                if (blob.Trees[t].TreeId != treeId) continue;
                for (int n = 0; n < blob.Trees[t].Nodes.Length; n++)
                {
                    if (blob.Trees[t].Nodes[n].NodeId == nodeId)
                        return true;
                }
                return false;
            }
            return false;
        }
    }
}
