using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Executes dialogue actions from Action nodes.
    /// Handles 11 action types: quest accepts, item/currency grants, flags, shop/crafting opens,
    /// encounter triggers, and voice line playback. Server-authoritative.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DialogueAdvanceSystem))]
    public partial class DialogueActionSystem : SystemBase
    {
        private EntityQuery _pendingQuery;

        protected override void OnCreate()
        {
            _pendingQuery = GetEntityQuery(
                ComponentType.ReadOnly<DialogueActionPending>(),
                ComponentType.ReadOnly<DialogueSessionState>());
        }

        protected override void OnUpdate()
        {
            if (_pendingQuery.IsEmptyIgnoreFilter) return;
            if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry)) return;

            var entities = _pendingQuery.ToEntityArray(Allocator.Temp);
            var pendings = _pendingQuery.ToComponentDataArray<DialogueActionPending>(Allocator.Temp);
            var sessions = _pendingQuery.ToComponentDataArray<DialogueSessionState>(Allocator.Temp);
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;

            for (int i = 0; i < entities.Length; i++)
            {
                var tree = registry.GetTree(pendings[i].TreeId);
                if (tree == null) continue;

                int nodeIndex = pendings[i].ActionNodeIndex;
                if (nodeIndex < 0 || nodeIndex >= tree.Nodes.Length) continue;

                ref var node = ref tree.Nodes[nodeIndex];
                if (node.Actions == null) continue;

                for (int a = 0; a < node.Actions.Length; a++)
                {
                    ExecuteAction(node.Actions[a], sessions[i].InteractingPlayer,
                        entities[i], (uint)tick);
                }

                EntityManager.RemoveComponent<DialogueActionPending>(entities[i]);
            }

            entities.Dispose();
            pendings.Dispose();
            sessions.Dispose();
        }

        private void ExecuteAction(DialogueAction action, Entity playerEntity,
            Entity npcEntity, uint tick)
        {
            switch (action.ActionType)
            {
                case DialogueActionType.AcceptQuest:
                    AcceptQuest(action.IntValue, playerEntity);
                    break;

                case DialogueActionType.GiveItem:
                    GiveItem(playerEntity, action.IntValue, action.IntValue2);
                    break;

                case DialogueActionType.TakeItem:
                    TakeItem(playerEntity, action.IntValue, action.IntValue2);
                    break;

                case DialogueActionType.GiveCurrency:
                    WriteCurrencyTransaction(playerEntity, action.IntValue);
                    break;

                case DialogueActionType.TakeCurrency:
                    if (EntityManager.HasComponent<DIG.Economy.CurrencyInventory>(playerEntity))
                    {
                        var inv = EntityManager.GetComponentData<DIG.Economy.CurrencyInventory>(playerEntity);
                        if (inv.Gold >= action.IntValue)
                            WriteCurrencyTransaction(playerEntity, -action.IntValue);
                    }
                    break;

                case DialogueActionType.SetFlag:
                    SetFlag(npcEntity, action.IntValue, tick);
                    break;

                case DialogueActionType.ClearFlag:
                    ClearFlag(npcEntity, action.IntValue);
                    break;

                case DialogueActionType.OpenShop:
                    OpenStation(playerEntity, npcEntity);
                    break;

                case DialogueActionType.OpenCrafting:
                    OpenStation(playerEntity, npcEntity);
                    break;

                case DialogueActionType.TriggerEncounter:
                    TriggerEncounter(action.IntValue);
                    break;

                case DialogueActionType.PlayVoiceLine:
                    PlayVoiceLine(npcEntity);
                    break;
            }
        }

        private void AcceptQuest(int questId, Entity playerEntity)
        {
            // Directly create quest instance entity (mirrors QuestAcceptSystem pattern)
            if (!SystemAPI.ManagedAPI.TryGetSingleton<DIG.Quest.QuestRegistryManaged>(out var questRegistry)) return;
            var def = questRegistry.Database?.GetQuest(questId);
            if (def == null) return;

            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;
            var instance = EntityManager.CreateEntity();
            EntityManager.AddComponentData(instance, new DIG.Quest.QuestProgress
            {
                QuestId = questId,
                State = DIG.Quest.QuestState.Active,
                TimeRemaining = def.TimeLimit,
                AcceptedAtTick = tick
            });
            EntityManager.AddComponentData(instance, new DIG.Quest.QuestPlayerLink
            {
                PlayerEntity = playerEntity
            });

            var objectives = EntityManager.AddBuffer<DIG.Quest.ObjectiveProgress>(instance);
            for (int o = 0; o < def.Objectives.Length; o++)
            {
                var objDef = def.Objectives[o];
                objectives.Add(new DIG.Quest.ObjectiveProgress
                {
                    ObjectiveId = objDef.ObjectiveId,
                    State = objDef.UnlockAfterObjectiveId == 0
                        ? DIG.Quest.ObjectiveState.Active
                        : DIG.Quest.ObjectiveState.Locked,
                    Type = objDef.Type,
                    TargetId = objDef.TargetId,
                    CurrentCount = 0,
                    RequiredCount = objDef.RequiredCount,
                    IsOptional = objDef.IsOptional,
                    IsHidden = objDef.IsHidden,
                    UnlockAfterObjectiveId = objDef.UnlockAfterObjectiveId
                });
            }

            DIG.Quest.QuestEventQueue.Enqueue(new DIG.Quest.QuestUIEvent
            {
                Type = DIG.Quest.QuestUIEventType.QuestAccepted,
                QuestId = questId
            });
        }

        private void GiveItem(Entity playerEntity, int resourceType, int quantity)
        {
            if (!EntityManager.HasBuffer<DIG.Shared.InventoryItem>(playerEntity)) return;
            var buffer = EntityManager.GetBuffer<DIG.Shared.InventoryItem>(playerEntity);

            // Try to stack with existing
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((int)buffer[i].ResourceType == resourceType)
                {
                    var item = buffer[i];
                    item.Quantity += quantity;
                    buffer[i] = item;
                    return;
                }
            }

            // Add new entry
            buffer.Add(new DIG.Shared.InventoryItem
            {
                ResourceType = (DIG.Shared.ResourceType)resourceType,
                Quantity = quantity
            });
        }

        private void TakeItem(Entity playerEntity, int resourceType, int quantity)
        {
            if (!EntityManager.HasBuffer<DIG.Shared.InventoryItem>(playerEntity)) return;
            var buffer = EntityManager.GetBuffer<DIG.Shared.InventoryItem>(playerEntity);

            for (int i = 0; i < buffer.Length; i++)
            {
                if ((int)buffer[i].ResourceType == resourceType && buffer[i].Quantity >= quantity)
                {
                    var item = buffer[i];
                    item.Quantity -= quantity;
                    if (item.Quantity <= 0)
                        buffer.RemoveAt(i);
                    else
                        buffer[i] = item;
                    return;
                }
            }
        }

        private void WriteCurrencyTransaction(Entity playerEntity, int amount)
        {
            if (!EntityManager.HasBuffer<DIG.Economy.CurrencyTransaction>(playerEntity)) return;
            var buffer = EntityManager.GetBuffer<DIG.Economy.CurrencyTransaction>(playerEntity);
            buffer.Add(new DIG.Economy.CurrencyTransaction
            {
                Type = DIG.Economy.CurrencyType.Gold,
                Amount = amount,
                Source = Entity.Null
            });
        }

        private void SetFlag(Entity npcEntity, int flagId, uint tick)
        {
            if (!EntityManager.HasBuffer<DialogueFlag>(npcEntity)) return;
            var flags = EntityManager.GetBuffer<DialogueFlag>(npcEntity);

            // Check if already set
            for (int i = 0; i < flags.Length; i++)
                if (flags[i].FlagId == flagId) return;

            flags.Add(new DialogueFlag { FlagId = flagId, SetAtTick = tick });
        }

        private void ClearFlag(Entity npcEntity, int flagId)
        {
            if (!EntityManager.HasBuffer<DialogueFlag>(npcEntity)) return;
            var flags = EntityManager.GetBuffer<DialogueFlag>(npcEntity);
            for (int i = flags.Length - 1; i >= 0; i--)
            {
                if (flags[i].FlagId == flagId)
                {
                    flags.RemoveAt(i);
                    return;
                }
            }
        }

        private void OpenStation(Entity playerEntity, Entity stationEntity)
        {
            if (!EntityManager.HasComponent<DIG.Interaction.StationSessionState>(playerEntity)) return;
            if (!EntityManager.HasComponent<DIG.Interaction.InteractionSession>(stationEntity)) return;

            var stationSession = EntityManager.GetComponentData<DIG.Interaction.InteractionSession>(stationEntity);
            EntityManager.SetComponentData(playerEntity, new DIG.Interaction.StationSessionState
            {
                SessionEntity = stationEntity,
                IsInSession = true
            });
            stationSession.IsOccupied = true;
            stationSession.OccupantEntity = playerEntity;
            EntityManager.SetComponentData(stationEntity, stationSession);
        }

        private void TriggerEncounter(int encounterId)
        {
            // Create transient trigger entity for EncounterTriggerSystem
            var e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new PlayDialogueTrigger
            {
                BossEntity = Entity.Null,
                DialogueIdOrBarkId = encounterId
            });
        }

        private void PlayVoiceLine(Entity npcEntity)
        {
            if (!EntityManager.HasComponent<Unity.Transforms.LocalToWorld>(npcEntity)) return;
            var ltw = EntityManager.GetComponentData<Unity.Transforms.LocalToWorld>(npcEntity);

            var e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new DIG.Aggro.Components.SoundEventRequest
            {
                Position = ltw.Position,
                SourceEntity = npcEntity,
                Loudness = 0.5f,
                MaxRange = 15f,
                Category = DIG.Aggro.Components.SoundCategory.Combat
            });
        }
    }
}
