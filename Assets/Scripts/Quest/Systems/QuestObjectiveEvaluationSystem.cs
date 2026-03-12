using Unity.Collections;
using Unity.Entities;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Core matching — matches QuestEvent transient entities to ObjectiveProgress buffers.
    /// Uses manual EntityQuery iteration per MEMORY.md SystemAPI.Query issues.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(QuestEvaluationSystemGroup))]
    [UpdateAfter(typeof(QuestAcceptSystem))]
    public partial class QuestObjectiveEvaluationSystem : SystemBase
    {
        private EntityQuery _eventQuery;
        private EntityQuery _questQuery;

        protected override void OnCreate()
        {
            _eventQuery = GetEntityQuery(ComponentType.ReadOnly<QuestEvent>(), ComponentType.ReadOnly<QuestEventTag>());
            _questQuery = GetEntityQuery(
                ComponentType.ReadWrite<QuestProgress>(),
                ComponentType.ReadOnly<QuestPlayerLink>(),
                ComponentType.ReadWrite<ObjectiveProgress>()
            );
            RequireForUpdate(_eventQuery);
        }

        protected override void OnUpdate()
        {
            var events = _eventQuery.ToComponentDataArray<QuestEvent>(Allocator.Temp);
            var questEntities = _questQuery.ToEntityArray(Allocator.Temp);
            var questProgress = _questQuery.ToComponentDataArray<QuestProgress>(Allocator.Temp);
            var questLinks = _questQuery.ToComponentDataArray<QuestPlayerLink>(Allocator.Temp);

            for (int e = 0; e < events.Length; e++)
            {
                var evt = events[e];

                for (int q = 0; q < questEntities.Length; q++)
                {
                    // Only process active quests for the source player
                    if (questLinks[q].PlayerEntity != evt.SourcePlayer) continue;
                    if (questProgress[q].State != QuestState.Active) continue;

                    var objectives = EntityManager.GetBuffer<ObjectiveProgress>(questEntities[q]);
                    bool changed = false;

                    for (int o = 0; o < objectives.Length; o++)
                    {
                        var obj = objectives[o];

                        if (obj.State != ObjectiveState.Active) continue;
                        if (obj.Type != evt.EventType) continue;
                        if (obj.TargetId != evt.TargetId) continue;

                        obj.CurrentCount += evt.Count;
                        if (obj.CurrentCount >= obj.RequiredCount)
                        {
                            obj.CurrentCount = obj.RequiredCount;
                            obj.State = ObjectiveState.Completed;
                        }

                        // Unhide on first progress
                        if (obj.IsHidden && obj.CurrentCount > 0)
                            obj.IsHidden = false;

                        objectives[o] = obj;
                        changed = true;
                    }

                    // Unlock dependent objectives
                    if (changed)
                        UnlockDependentObjectives(objectives);
                }
            }

            events.Dispose();
            questEntities.Dispose();
            questProgress.Dispose();
            questLinks.Dispose();
        }

        private static void UnlockDependentObjectives(DynamicBuffer<ObjectiveProgress> objectives)
        {
            for (int i = 0; i < objectives.Length; i++)
            {
                var obj = objectives[i];
                if (obj.State != ObjectiveState.Locked) continue;
                if (obj.UnlockAfterObjectiveId == 0) continue;

                // Check if the prerequisite objective is completed
                for (int j = 0; j < objectives.Length; j++)
                {
                    if (objectives[j].ObjectiveId == obj.UnlockAfterObjectiveId &&
                        objectives[j].State == ObjectiveState.Completed)
                    {
                        obj.State = ObjectiveState.Active;
                        objectives[i] = obj;
                        break;
                    }
                }
            }
        }
    }
}
