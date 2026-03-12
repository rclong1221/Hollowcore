using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Interaction;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: When a player interacts with a QuestGiver NPC, creates QuestInstance entities
    /// for available quests. Also handles turn-in when quest state is Completed.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(QuestEvaluationSystemGroup), OrderFirst = true)]
    public partial class QuestAcceptSystem : SystemBase
    {
        private EntityQuery _eventQuery;
        private EntityQuery _questInstanceQuery;

        protected override void OnCreate()
        {
            _eventQuery = GetEntityQuery(ComponentType.ReadOnly<InteractionCompleteEvent>());
            _questInstanceQuery = GetEntityQuery(
                ComponentType.ReadOnly<QuestProgress>(),
                ComponentType.ReadOnly<QuestPlayerLink>()
            );
            RequireForUpdate<QuestRegistryManaged>();
        }

        protected override void OnUpdate()
        {
            if (_eventQuery.IsEmpty) return;

            var registry = SystemAPI.ManagedAPI.GetSingleton<QuestRegistryManaged>();
            var giverLookup = GetComponentLookup<QuestGiverData>(true);
            var contextLookup = GetComponentLookup<InteractableContext>(true);
            var interactableLookup = GetComponentLookup<Interactable>(true);

            // Gather existing quest instances for duplicate/prerequisite checks
            var existingEntities = _questInstanceQuery.ToEntityArray(Allocator.Temp);
            var existingProgress = _questInstanceQuery.ToComponentDataArray<QuestProgress>(Allocator.Temp);
            var existingLinks = _questInstanceQuery.ToComponentDataArray<QuestPlayerLink>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            foreach (var (evt, eventEntity) in SystemAPI.Query<RefRO<InteractionCompleteEvent>>().WithEntityAccess())
            {
                var interactableEntity = evt.ValueRO.InteractableEntity;
                var playerEntity = evt.ValueRO.InteractorEntity;

                // Only process Talk interactions with QuestGiverData
                if (!giverLookup.HasComponent(interactableEntity))
                    continue;

                // Optional: only respond to Talk verb
                if (contextLookup.HasComponent(interactableEntity))
                {
                    if (contextLookup[interactableEntity].Verb != InteractionVerb.Talk)
                        continue;
                }

                ref var questBlob = ref giverLookup[interactableEntity].AvailableQuests.Value;

                // Check turn-in first: if player has a Completed quest matching this NPC's InteractableID
                int npcInteractableId = 0;
                if (interactableLookup.HasComponent(interactableEntity))
                    npcInteractableId = interactableLookup[interactableEntity].InteractableID;

                for (int q = 0; q < existingEntities.Length; q++)
                {
                    if (existingLinks[q].PlayerEntity != playerEntity) continue;
                    var progress = existingProgress[q];
                    if (progress.State != QuestState.Completed) continue;

                    var def = registry.Database.GetQuest(progress.QuestId);
                    if (def == null) continue;
                    if (def.TurnInInteractableId != npcInteractableId) continue;

                    // Turn in: transition to TurnedIn
                    ecb.SetComponent(existingEntities[q], new QuestProgress
                    {
                        QuestId = progress.QuestId,
                        State = QuestState.TurnedIn,
                        TimeRemaining = 0,
                        AcceptedAtTick = progress.AcceptedAtTick
                    });
                }

                // Offer available quests
                for (int i = 0; i < questBlob.QuestIds.Length; i++)
                {
                    int questId = questBlob.QuestIds[i];
                    var def = registry.Database.GetQuest(questId);
                    if (def == null) continue;

                    if (IsQuestActiveOrCompleted(questId, playerEntity, existingProgress, existingLinks, def.IsRepeatable))
                        continue;

                    if (!ArePrerequisitesMet(def, playerEntity))
                        continue;

                    // Create QuestInstance entity
                    var instance = ecb.CreateEntity();
                    ecb.AddComponent(instance, new QuestProgress
                    {
                        QuestId = questId,
                        State = QuestState.Active,
                        TimeRemaining = def.TimeLimit,
                        AcceptedAtTick = tick
                    });
                    ecb.AddComponent(instance, new QuestPlayerLink { PlayerEntity = playerEntity });

                    var objectives = ecb.AddBuffer<ObjectiveProgress>(instance);
                    for (int o = 0; o < def.Objectives.Length; o++)
                    {
                        ref var objDef = ref def.Objectives[o];
                        objectives.Add(new ObjectiveProgress
                        {
                            ObjectiveId = objDef.ObjectiveId,
                            State = objDef.UnlockAfterObjectiveId == 0 ? ObjectiveState.Active : ObjectiveState.Locked,
                            Type = objDef.Type,
                            TargetId = objDef.TargetId,
                            CurrentCount = 0,
                            RequiredCount = objDef.RequiredCount,
                            IsOptional = objDef.IsOptional,
                            IsHidden = objDef.IsHidden,
                            UnlockAfterObjectiveId = objDef.UnlockAfterObjectiveId
                        });
                    }

                    QuestEventQueue.Enqueue(new QuestUIEvent
                    {
                        Type = QuestUIEventType.QuestAccepted,
                        QuestId = questId
                    });
                }
            }

            existingEntities.Dispose();
            existingProgress.Dispose();
            existingLinks.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private bool IsQuestActiveOrCompleted(int questId, Entity player,
            NativeArray<QuestProgress> progress, NativeArray<QuestPlayerLink> links, bool isRepeatable)
        {
            for (int i = 0; i < progress.Length; i++)
            {
                if (links[i].PlayerEntity != player) continue;
                if (progress[i].QuestId != questId) continue;

                // Active or Completed (not yet turned in) = can't accept again
                if (progress[i].State == QuestState.Active || progress[i].State == QuestState.Completed)
                    return true;

                // TurnedIn and not repeatable = can't accept again
                if (progress[i].State == QuestState.TurnedIn && !isRepeatable)
                    return true;
            }
            return false;
        }

        private bool ArePrerequisitesMet(QuestDefinitionSO def, Entity player)
        {
            if (def.PrerequisiteQuestIds == null || def.PrerequisiteQuestIds.Length == 0)
                return true;

            if (!SystemAPI.HasBuffer<CompletedQuestEntry>(player))
                return false;

            var completed = SystemAPI.GetBuffer<CompletedQuestEntry>(player);

            foreach (var prereqId in def.PrerequisiteQuestIds)
            {
                bool found = false;
                for (int i = 0; i < completed.Length; i++)
                {
                    if (completed[i].QuestId == prereqId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }
    }
}
