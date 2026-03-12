using Unity.Collections;
using Unity.Entities;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Checks if all required objectives are done and transitions Active → Completed.
    /// For AutoComplete quests, also handles immediate turn-in.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(QuestEvaluationSystemGroup))]
    [UpdateAfter(typeof(QuestObjectiveEvaluationSystem))]
    public partial class QuestCompletionSystem : SystemBase
    {
        private EntityQuery _activeQuestQuery;

        protected override void OnCreate()
        {
            _activeQuestQuery = GetEntityQuery(
                ComponentType.ReadWrite<QuestProgress>(),
                ComponentType.ReadOnly<QuestPlayerLink>(),
                ComponentType.ReadOnly<ObjectiveProgress>()
            );
            RequireForUpdate<QuestRegistryManaged>();
        }

        protected override void OnUpdate()
        {
            var registry = SystemAPI.ManagedAPI.GetSingleton<QuestRegistryManaged>();

            var entities = _activeQuestQuery.ToEntityArray(Allocator.Temp);
            var progressArray = _activeQuestQuery.ToComponentDataArray<QuestProgress>(Allocator.Temp);
            var links = _activeQuestQuery.ToComponentDataArray<QuestPlayerLink>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var progress = progressArray[i];
                if (progress.State != QuestState.Active) continue;

                var objectives = EntityManager.GetBuffer<ObjectiveProgress>(entities[i], true);
                bool allRequiredDone = true;

                for (int o = 0; o < objectives.Length; o++)
                {
                    var obj = objectives[o];
                    if (obj.IsOptional) continue;
                    if (obj.State != ObjectiveState.Completed)
                    {
                        allRequiredDone = false;
                        break;
                    }
                }

                if (!allRequiredDone) continue;

                var def = registry.Database.GetQuest(progress.QuestId);
                if (def == null) continue;

                if (def.AutoComplete || def.TurnInInteractableId == 0)
                {
                    // Auto-complete: go straight to TurnedIn
                    progress.State = QuestState.TurnedIn;
                    EntityManager.SetComponentData(entities[i], progress);

                    QuestEventQueue.Enqueue(new QuestUIEvent
                    {
                        Type = QuestUIEventType.QuestTurnedIn,
                        QuestId = progress.QuestId
                    });
                }
                else
                {
                    // Requires manual turn-in at NPC
                    progress.State = QuestState.Completed;
                    EntityManager.SetComponentData(entities[i], progress);

                    QuestEventQueue.Enqueue(new QuestUIEvent
                    {
                        Type = QuestUIEventType.QuestCompleted,
                        QuestId = progress.QuestId
                    });
                }
            }

            entities.Dispose();
            progressArray.Dispose();
            links.Dispose();
        }
    }
}
