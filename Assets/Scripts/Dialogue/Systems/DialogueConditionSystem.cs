using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Evaluates conditions on the current dialogue node.
    /// For PlayerChoice nodes: writes ValidChoicesMask (which choices the player can see).
    /// For Condition nodes: auto-branches to TrueNodeId or FalseNodeId.
    /// Uses cached ComponentLookup for O(1) random-access component reads.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DialogueRpcReceiveSystem))]
    public partial class DialogueConditionSystem : SystemBase
    {
        private EntityQuery _questProgressQuery;
        private ComponentLookup<DIG.Economy.CurrencyInventory> _currencyLookup;
        private ComponentLookup<DIG.Combat.Components.CharacterAttributes> _attributesLookup;
        private ComponentLookup<DIG.Aggro.Components.AlertState> _alertLookup;
        private BufferLookup<DialogueFlag> _flagLookup;
        private BufferLookup<DIG.Quest.CompletedQuestEntry> _completedQuestLookup;
        private BufferLookup<DIG.Shared.InventoryItem> _inventoryLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<DialogueConfig>();
            _questProgressQuery = GetEntityQuery(
                ComponentType.ReadOnly<DIG.Quest.QuestProgress>(),
                ComponentType.ReadOnly<DIG.Quest.QuestPlayerLink>());
            _currencyLookup = GetComponentLookup<DIG.Economy.CurrencyInventory>(true);
            _attributesLookup = GetComponentLookup<DIG.Combat.Components.CharacterAttributes>(true);
            _alertLookup = GetComponentLookup<DIG.Aggro.Components.AlertState>(true);
            _flagLookup = GetBufferLookup<DialogueFlag>(true);
            _completedQuestLookup = GetBufferLookup<DIG.Quest.CompletedQuestEntry>(true);
            _inventoryLookup = GetBufferLookup<DIG.Shared.InventoryItem>(true);
        }

        protected override void OnUpdate()
        {
            CompleteDependency();

            // Refresh lookups
            _currencyLookup.Update(this);
            _attributesLookup.Update(this);
            _alertLookup.Update(this);
            _flagLookup.Update(this);
            _completedQuestLookup.Update(this);
            _inventoryLookup.Update(this);

            if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry)) return;

            foreach (var (session, entity) in
                SystemAPI.Query<RefRW<DialogueSessionState>>().WithEntityAccess())
            {
                if (!session.ValueRO.IsActive) continue;

                var tree = registry.GetTree(session.ValueRO.CurrentTreeId);
                if (tree == null) continue;

                int nodeIndex = tree.FindNodeIndex(session.ValueRO.CurrentNodeId);
                if (nodeIndex < 0) continue;

                ref var node = ref tree.Nodes[nodeIndex];

                switch (node.NodeType)
                {
                    case DialogueNodeType.PlayerChoice:
                        session.ValueRW.ValidChoicesMask = EvaluateChoices(
                            ref node, session.ValueRO.InteractingPlayer, entity);
                        break;

                    case DialogueNodeType.Condition:
                        bool result = EvaluateCondition(
                            node.ConditionType, node.ConditionValue,
                            session.ValueRO.InteractingPlayer, entity);
                        session.ValueRW.CurrentNodeId = result ? node.TrueNodeId : node.FalseNodeId;
                        break;
                }
            }
        }

        private byte EvaluateChoices(ref DialogueNode node, Entity playerEntity, Entity npcEntity)
        {
            byte mask = 0;
            if (node.Choices == null) return mask;

            for (int i = 0; i < node.Choices.Length && i < 8; i++)
            {
                if (EvaluateCondition(
                    node.Choices[i].ConditionType, node.Choices[i].ConditionValue,
                    playerEntity, npcEntity))
                {
                    mask |= (byte)(1 << i);
                }
            }
            return mask;
        }

        private bool EvaluateCondition(DialogueConditionType type, int value,
            Entity playerEntity, Entity npcEntity)
        {
            switch (type)
            {
                case DialogueConditionType.None:
                    return true;

                case DialogueConditionType.QuestCompleted:
                    return CheckQuestCompleted(playerEntity, value);

                case DialogueConditionType.QuestActive:
                    return CheckQuestActive(playerEntity, value);

                case DialogueConditionType.HasItem:
                    return CheckHasItem(playerEntity, value);

                case DialogueConditionType.HasCurrency:
                    if (_currencyLookup.HasComponent(playerEntity))
                        return _currencyLookup[playerEntity].Gold >= value;
                    return false;

                case DialogueConditionType.PlayerLevel:
                    if (_attributesLookup.HasComponent(playerEntity))
                        return _attributesLookup[playerEntity].Level >= value;
                    return false;

                case DialogueConditionType.DialogueFlag:
                    return HasDialogueFlag(npcEntity, value);

                case DialogueConditionType.DialogueFlagClear:
                    return !HasDialogueFlag(npcEntity, value);

                case DialogueConditionType.Reputation:
                    return true; // Future EPIC hook

                case DialogueConditionType.AlertLevelBelow:
                    if (_alertLookup.HasComponent(npcEntity))
                        return _alertLookup[npcEntity].AlertLevel < value;
                    return true;

                default:
                    return false;
            }
        }

        private bool HasDialogueFlag(Entity npcEntity, int flagId)
        {
            if (!_flagLookup.HasBuffer(npcEntity)) return false;
            var flags = _flagLookup[npcEntity];
            for (int i = 0; i < flags.Length; i++)
                if (flags[i].FlagId == flagId) return true;
            return false;
        }

        private bool CheckQuestCompleted(Entity playerEntity, int questId)
        {
            if (!_completedQuestLookup.HasBuffer(playerEntity)) return false;
            var completed = _completedQuestLookup[playerEntity];
            for (int i = 0; i < completed.Length; i++)
                if (completed[i].QuestId == questId) return true;
            return false;
        }

        private bool CheckQuestActive(Entity playerEntity, int questId)
        {
            if (_questProgressQuery.IsEmptyIgnoreFilter) return false;

            var progresses = _questProgressQuery.ToComponentDataArray<DIG.Quest.QuestProgress>(Allocator.Temp);
            var links = _questProgressQuery.ToComponentDataArray<DIG.Quest.QuestPlayerLink>(Allocator.Temp);
            bool found = false;
            for (int i = 0; i < progresses.Length; i++)
            {
                if (links[i].PlayerEntity == playerEntity &&
                    progresses[i].QuestId == questId &&
                    progresses[i].State == DIG.Quest.QuestState.Active)
                {
                    found = true;
                    break;
                }
            }
            progresses.Dispose();
            links.Dispose();
            return found;
        }

        private bool CheckHasItem(Entity playerEntity, int resourceTypeValue)
        {
            if (!_inventoryLookup.HasBuffer(playerEntity)) return false;
            var items = _inventoryLookup[playerEntity];
            for (int i = 0; i < items.Length; i++)
            {
                if ((int)items[i].ResourceType == resourceTypeValue && items[i].Quantity > 0)
                    return true;
            }
            return false;
        }
    }
}
