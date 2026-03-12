using Unity.Collections;
using Unity.Entities;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Ticks TimeRemaining on timed quests. Fails quests when time expires.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(QuestEvaluationSystemGroup))]
    [UpdateAfter(typeof(QuestRewardSystem))]
    public partial class QuestTimerSystem : SystemBase
    {
        private EntityQuery _activeTimedQuery;

        protected override void OnCreate()
        {
            _activeTimedQuery = GetEntityQuery(
                ComponentType.ReadWrite<QuestProgress>(),
                ComponentType.ReadOnly<QuestPlayerLink>()
            );
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;

            var entities = _activeTimedQuery.ToEntityArray(Allocator.Temp);
            var progressArray = _activeTimedQuery.ToComponentDataArray<QuestProgress>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var progress = progressArray[i];
                if (progress.State != QuestState.Active) continue;
                if (progress.TimeRemaining <= 0f) continue; // 0 = no time limit

                progress.TimeRemaining -= dt;
                if (progress.TimeRemaining <= 0f)
                {
                    progress.TimeRemaining = 0f;
                    progress.State = QuestState.Failed;

                    QuestEventQueue.Enqueue(new QuestUIEvent
                    {
                        Type = QuestUIEventType.QuestFailed,
                        QuestId = progress.QuestId
                    });
                }

                EntityManager.SetComponentData(entities[i], progress);
            }

            entities.Dispose();
            progressArray.Dispose();
        }
    }
}
