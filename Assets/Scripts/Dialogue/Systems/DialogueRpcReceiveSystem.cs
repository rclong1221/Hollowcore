using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Validation;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Receives DialogueChoiceRpc and DialogueSkipRpc from clients.
    /// Resolves ghost IDs to NPC entities and queues the input for
    /// DialogueAdvanceSystem / DialogueEndSystem to process.
    /// Uses NativeParallelHashMap for O(1) ghost resolution instead of O(N) scan.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DialogueInitiationSystem))]
    public partial class DialogueRpcReceiveSystem : SystemBase
    {
        private EntityQuery _choiceRpcQuery;
        private EntityQuery _skipRpcQuery;
        private EntityQuery _ghostQuery;

        // Queued inputs for other systems to process
        public NativeList<DialogueChoiceInput> PendingChoices;
        public NativeList<DialogueSkipInput> PendingSkips;

        public struct DialogueChoiceInput
        {
            public Entity NpcEntity;
            public Entity SenderEntity;
            public int ChoiceIndex;
            public int CurrentNodeId;
        }

        public struct DialogueSkipInput
        {
            public Entity NpcEntity;
            public Entity SenderEntity;
        }

        protected override void OnCreate()
        {
            _choiceRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<DialogueChoiceRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _skipRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<DialogueSkipRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _ghostQuery = GetEntityQuery(ComponentType.ReadOnly<GhostInstance>());

            PendingChoices = new NativeList<DialogueChoiceInput>(4, Allocator.Persistent);
            PendingSkips = new NativeList<DialogueSkipInput>(4, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (PendingChoices.IsCreated) PendingChoices.Dispose();
            if (PendingSkips.IsCreated) PendingSkips.Dispose();
        }

        protected override void OnUpdate()
        {
            PendingChoices.Clear();
            PendingSkips.Clear();

            bool hasChoices = !_choiceRpcQuery.IsEmptyIgnoreFilter;
            bool hasSkips = !_skipRpcQuery.IsEmptyIgnoreFilter;
            if (!hasChoices && !hasSkips) return;

            // Build ghost ID → Entity map once per frame (O(N) build, O(1) per lookup)
            var ghostEntities = _ghostQuery.ToEntityArray(Allocator.Temp);
            var ghosts = _ghostQuery.ToComponentDataArray<GhostInstance>(Allocator.Temp);
            var ghostMap = new NativeParallelHashMap<int, Entity>(ghostEntities.Length, Allocator.Temp);
            for (int g = 0; g < ghostEntities.Length; g++)
                ghostMap.TryAdd(ghosts[g].ghostId, ghostEntities[g]);
            ghostEntities.Dispose();
            ghosts.Dispose();

            // Process choice RPCs
            if (hasChoices)
            {
                var rpcEntities = _choiceRpcQuery.ToEntityArray(Allocator.Temp);
                var rpcs = _choiceRpcQuery.ToComponentDataArray<DialogueChoiceRpc>(Allocator.Temp);
                var receivers = _choiceRpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

                for (int i = 0; i < rpcs.Length; i++)
                {
                    // --- ANTI-CHEAT: Rate limit check ---
                    Entity senderPlayer = Entity.Null;
                    if (SystemAPI.HasComponent<CommandTarget>(receivers[i].SourceConnection))
                        senderPlayer = SystemAPI.GetComponent<CommandTarget>(receivers[i].SourceConnection).targetEntity;
                    if (senderPlayer != Entity.Null && EntityManager.HasComponent<ValidationLink>(senderPlayer))
                    {
                        var valChild = EntityManager.GetComponentData<ValidationLink>(senderPlayer).ValidationChild;
                        if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.DIALOGUE_CHOICE))
                        {
                            RateLimitHelper.CreateViolation(EntityManager, senderPlayer,
                                ViolationType.RateLimit, 0.3f, RpcTypeIds.DIALOGUE_CHOICE, 0);
                            EntityManager.DestroyEntity(rpcEntities[i]);
                            continue;
                        }
                    }
                    // --- END ANTI-CHEAT ---

                    if (ghostMap.TryGetValue(rpcs[i].NpcGhostId, out var npcEntity))
                    {
                        PendingChoices.Add(new DialogueChoiceInput
                        {
                            NpcEntity = npcEntity,
                            SenderEntity = receivers[i].SourceConnection,
                            ChoiceIndex = rpcs[i].ChoiceIndex,
                            CurrentNodeId = rpcs[i].CurrentNodeId
                        });
                    }
                    EntityManager.DestroyEntity(rpcEntities[i]);
                }

                rpcEntities.Dispose();
                rpcs.Dispose();
                receivers.Dispose();
            }

            // Process skip RPCs
            if (hasSkips)
            {
                var rpcEntities = _skipRpcQuery.ToEntityArray(Allocator.Temp);
                var rpcs = _skipRpcQuery.ToComponentDataArray<DialogueSkipRpc>(Allocator.Temp);
                var receivers = _skipRpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

                for (int i = 0; i < rpcs.Length; i++)
                {
                    // --- ANTI-CHEAT: Rate limit check ---
                    Entity skipPlayer = Entity.Null;
                    if (SystemAPI.HasComponent<CommandTarget>(receivers[i].SourceConnection))
                        skipPlayer = SystemAPI.GetComponent<CommandTarget>(receivers[i].SourceConnection).targetEntity;
                    if (skipPlayer != Entity.Null && EntityManager.HasComponent<ValidationLink>(skipPlayer))
                    {
                        var valChild = EntityManager.GetComponentData<ValidationLink>(skipPlayer).ValidationChild;
                        if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.DIALOGUE_SKIP))
                        {
                            RateLimitHelper.CreateViolation(EntityManager, skipPlayer,
                                ViolationType.RateLimit, 0.3f, RpcTypeIds.DIALOGUE_SKIP, 0);
                            EntityManager.DestroyEntity(rpcEntities[i]);
                            continue;
                        }
                    }
                    // --- END ANTI-CHEAT ---

                    if (ghostMap.TryGetValue(rpcs[i].NpcGhostId, out var npcEntity))
                    {
                        PendingSkips.Add(new DialogueSkipInput
                        {
                            NpcEntity = npcEntity,
                            SenderEntity = receivers[i].SourceConnection
                        });
                    }
                    EntityManager.DestroyEntity(rpcEntities[i]);
                }

                rpcEntities.Dispose();
                rpcs.Dispose();
                receivers.Dispose();
            }

            ghostMap.Dispose();
        }
    }
}
