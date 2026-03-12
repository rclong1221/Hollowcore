using Unity.Entities;
using Unity.NetCode;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Advances dialogue state when a validated player choice is received.
    /// Handles node chaining: Condition->branch, Random->weighted pick, Action->dispatch,
    /// End->queue to DialogueEndSystem. Server-authoritative.
    /// Uses cached system reference and ComponentLookup for efficient access.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DialogueConditionSystem))]
    public partial class DialogueAdvanceSystem : SystemBase
    {
        private uint _randomSeed;
        private DialogueRpcReceiveSystem _rpcSystem;
        private ComponentLookup<DialogueSessionState> _sessionLookup;
        private ComponentLookup<DialogueActionPending> _actionPendingLookup;
        private ComponentLookup<DIG.Combat.Components.CharacterAttributes> _attributesLookup;
        private ComponentLookup<DIG.Economy.CurrencyInventory> _currencyLookup;
        private ComponentLookup<DIG.Aggro.Components.AlertState> _alertLookup;
        private BufferLookup<DialogueFlag> _flagLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<DialogueConfig>();
            _randomSeed = 12345;
            _sessionLookup = GetComponentLookup<DialogueSessionState>(false);
            _actionPendingLookup = GetComponentLookup<DialogueActionPending>(false);
            _attributesLookup = GetComponentLookup<DIG.Combat.Components.CharacterAttributes>(true);
            _currencyLookup = GetComponentLookup<DIG.Economy.CurrencyInventory>(true);
            _alertLookup = GetComponentLookup<DIG.Aggro.Components.AlertState>(true);
            _flagLookup = GetBufferLookup<DialogueFlag>(true);
        }

        protected override void OnUpdate()
        {
            // Lazy-init cached system reference (may not exist at OnCreate time on client)
            if (_rpcSystem == null)
                _rpcSystem = World.GetExistingSystemManaged<DialogueRpcReceiveSystem>();

            // Refresh lookups
            _sessionLookup.Update(this);
            _actionPendingLookup.Update(this);
            _attributesLookup.Update(this);
            _currencyLookup.Update(this);
            _alertLookup.Update(this);
            _flagLookup.Update(this);

            // Process pending choices from RPC receive system
            if (_rpcSystem != null && _rpcSystem.PendingChoices.IsCreated)
            {
                ProcessChoices(_rpcSystem);
            }

            // Also handle Speech nodes with auto-advance (Duration > 0)
            if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry)) return;
            var config = SystemAPI.GetSingleton<DialogueConfig>();

            if (!config.AutoAdvanceEnabled) return;

            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;

            foreach (var (session, entity) in
                SystemAPI.Query<RefRW<DialogueSessionState>>().WithEntityAccess())
            {
                if (!session.ValueRO.IsActive) continue;

                var tree = registry.GetTree(session.ValueRO.CurrentTreeId);
                if (tree == null) continue;

                int nodeIndex = tree.FindNodeIndex(session.ValueRO.CurrentNodeId);
                if (nodeIndex < 0) continue;

                ref var node = ref tree.Nodes[nodeIndex];
                if (node.NodeType != DialogueNodeType.Speech) continue;
                if (node.Duration <= 0f) continue;

                // Check if duration elapsed (rough tick-based timing)
                uint durationTicks = (uint)(node.Duration * 30f); // ~30Hz tick rate
                if (tick - session.ValueRO.SessionStartTick < durationTicks) continue;

                // Auto-advance to next node
                AdvanceToNode(ref session.ValueRW, node.NextNodeId, tree, entity);
            }
        }

        private void ProcessChoices(DialogueRpcReceiveSystem rpcSystem)
        {
            if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry)) return;

            for (int i = 0; i < rpcSystem.PendingChoices.Length; i++)
            {
                var input = rpcSystem.PendingChoices[i];
                if (!_sessionLookup.HasComponent(input.NpcEntity)) continue;

                var session = _sessionLookup[input.NpcEntity];
                if (!session.IsActive) continue;
                if (session.CurrentNodeId != input.CurrentNodeId) continue; // Stale

                var tree = registry.GetTree(session.CurrentTreeId);
                if (tree == null) continue;

                int nodeIndex = tree.FindNodeIndex(session.CurrentNodeId);
                if (nodeIndex < 0) continue;

                ref var node = ref tree.Nodes[nodeIndex];

                // For Speech nodes, clicking advances to NextNodeId
                if (node.NodeType == DialogueNodeType.Speech)
                {
                    AdvanceToNode(ref session, node.NextNodeId, tree, input.NpcEntity);
                    _sessionLookup[input.NpcEntity] = session;
                    continue;
                }

                // For PlayerChoice, validate choice against mask
                if (node.NodeType != DialogueNodeType.PlayerChoice) continue;
                if (node.Choices == null || input.ChoiceIndex >= node.Choices.Length) continue;
                if ((session.ValidChoicesMask & (1 << input.ChoiceIndex)) == 0) continue; // Rejected

                int nextNodeId = node.Choices[input.ChoiceIndex].NextNodeId;
                AdvanceToNode(ref session, nextNodeId, tree, input.NpcEntity);
                _sessionLookup[input.NpcEntity] = session;
            }
        }

        private void AdvanceToNode(ref DialogueSessionState session, int nextNodeId,
            DialogueTreeSO tree, Entity npcEntity)
        {
            // Chain through invisible nodes (Condition, Random, Action) in a single frame
            int maxChain = 20;
            int nodeId = nextNodeId;

            while (maxChain-- > 0)
            {
                int idx = tree.FindNodeIndex(nodeId);
                if (idx < 0)
                {
                    // Invalid node — end session
                    session.IsActive = false;
                    return;
                }

                ref var node = ref tree.Nodes[idx];

                switch (node.NodeType)
                {
                    case DialogueNodeType.Condition:
                        bool result = EvaluateConditionSimple(
                            node.ConditionType, node.ConditionValue,
                            session.InteractingPlayer, npcEntity);
                        nodeId = result ? node.TrueNodeId : node.FalseNodeId;
                        continue;

                    case DialogueNodeType.Random:
                        nodeId = PickWeightedRandom(ref node);
                        continue;

                    case DialogueNodeType.Action:
                        // Dispatch to action system
                        if (!_actionPendingLookup.HasComponent(npcEntity))
                            EntityManager.AddComponent<DialogueActionPending>(npcEntity);
                        _actionPendingLookup[npcEntity] = new DialogueActionPending
                        {
                            ActionNodeIndex = idx,
                            TreeId = session.CurrentTreeId
                        };
                        // Advance past action node
                        nodeId = node.NextNodeId;
                        continue;

                    case DialogueNodeType.End:
                        session.IsActive = false;
                        session.CurrentNodeId = nodeId;
                        return;

                    case DialogueNodeType.Speech:
                    case DialogueNodeType.PlayerChoice:
                    case DialogueNodeType.Hub:
                        // Visible node — stop here, UI will display
                        session.CurrentNodeId = nodeId;
                        session.ValidChoicesMask = 0; // ConditionSystem will recalculate
                        return;
                }
            }

            // Chain depth exceeded — force end
            session.IsActive = false;
        }

        private bool EvaluateConditionSimple(DialogueConditionType type, int value,
            Entity playerEntity, Entity npcEntity)
        {
            switch (type)
            {
                case DialogueConditionType.None: return true;
                case DialogueConditionType.DialogueFlag:
                    return HasFlag(npcEntity, value);
                case DialogueConditionType.DialogueFlagClear:
                    return !HasFlag(npcEntity, value);
                case DialogueConditionType.PlayerLevel:
                    if (_attributesLookup.HasComponent(playerEntity))
                        return _attributesLookup[playerEntity].Level >= value;
                    return false;
                case DialogueConditionType.HasCurrency:
                    if (_currencyLookup.HasComponent(playerEntity))
                        return _currencyLookup[playerEntity].Gold >= value;
                    return false;
                case DialogueConditionType.AlertLevelBelow:
                    if (_alertLookup.HasComponent(npcEntity))
                        return _alertLookup[npcEntity].AlertLevel < value;
                    return true;
                case DialogueConditionType.Reputation:
                    return true;
                default: return false;
            }
        }

        private bool HasFlag(Entity npcEntity, int flagId)
        {
            if (!_flagLookup.HasBuffer(npcEntity)) return false;
            var flags = _flagLookup[npcEntity];
            for (int i = 0; i < flags.Length; i++)
                if (flags[i].FlagId == flagId) return true;
            return false;
        }

        private int PickWeightedRandom(ref DialogueNode node)
        {
            if (node.RandomEntries == null || node.RandomEntries.Length == 0)
                return node.NextNodeId;

            float totalWeight = 0f;
            for (int i = 0; i < node.RandomEntries.Length; i++)
                totalWeight += node.RandomEntries[i].Weight;

            _randomSeed = _randomSeed * 1103515245 + 12345;
            float roll = (_randomSeed & 0x7FFFFFFF) / (float)0x7FFFFFFF * totalWeight;

            float cumulative = 0f;
            for (int i = 0; i < node.RandomEntries.Length; i++)
            {
                cumulative += node.RandomEntries[i].Weight;
                if (roll <= cumulative)
                    return node.RandomEntries[i].NodeId;
            }
            return node.RandomEntries[node.RandomEntries.Length - 1].NodeId;
        }
    }
}
