using Unity.Entities;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Destroys all QuestEvent transient entities at end of frame.
    /// Runs last in QuestEvaluationSystemGroup to ensure all consumers have processed.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(QuestEvaluationSystemGroup), OrderLast = true)]
    public partial class QuestEventCleanupSystem : SystemBase
    {
        private EntityQuery _eventQuery;

        protected override void OnCreate()
        {
            _eventQuery = GetEntityQuery(
                ComponentType.ReadOnly<QuestEvent>(),
                ComponentType.ReadOnly<QuestEventTag>()
            );
        }

        protected override void OnUpdate()
        {
            EntityManager.DestroyEntity(_eventQuery);
        }
    }
}
