using Unity.Entities;
using Unity.NetCode;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Observes quest completions and other significant events,
    /// creating SaveRequest(Checkpoint) with deduplication cooldown.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SaveSystem))]
    public partial class CheckpointTriggerSystem : SystemBase
    {
        private EntityQuery _questCompletionQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<SaveManagerSingleton>();
            // Look for quests that just transitioned to Completed
            _questCompletionQuery = GetEntityQuery(
                ComponentType.ReadOnly<DIG.Quest.QuestProgress>(),
                ComponentType.Exclude<DIG.Quest.QuestRewardedTag>());
        }

        protected override void OnUpdate()
        {
            var manager = SystemAPI.ManagedAPI.GetSingleton<SaveManagerSingleton>();
            if (!manager.IsInitialized || !manager.Config.EnableCheckpointSave) return;

            // Cooldown check
            if (manager.TimeSinceLastCheckpoint < manager.Config.CheckpointCooldown)
                return;

            bool shouldCheckpoint = false;

            // Check for quest completions
            if (_questCompletionQuery.CalculateEntityCount() > 0)
            {
                foreach (var progress in SystemAPI.Query<RefRO<DIG.Quest.QuestProgress>>()
                    .WithNone<DIG.Quest.QuestRewardedTag>())
                {
                    if (progress.ValueRO.State == DIG.Quest.QuestState.Completed)
                    {
                        shouldCheckpoint = true;
                        break;
                    }
                }
            }

            if (shouldCheckpoint)
            {
                manager.TimeSinceLastCheckpoint = 0f;
                var requestEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(requestEntity, new SaveRequest
                {
                    SlotIndex = manager.Config.AutosaveSlot,
                    TriggerSource = SaveTriggerSource.Checkpoint
                });
            }
        }
    }
}
