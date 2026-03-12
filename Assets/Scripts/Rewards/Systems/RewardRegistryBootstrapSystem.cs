using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Managed singleton holding reward/event pools loaded from Resources/.
    /// Access via SystemAPI.ManagedAPI.GetSingleton&lt;RewardRegistryManaged&gt;().
    /// </summary>
    public class RewardRegistryManaged : IComponentData
    {
        public RewardPoolSO DefaultRewardPool;
        public EventPoolSO DefaultEventPool;
        public Dictionary<int, RewardDefinitionSO> RewardById = new();
    }

    /// <summary>
    /// EPIC 23.5: Loads reward/event pools from Resources/. Populates managed registry.
    /// Ensures RunState entity has PendingRewardChoice and ShopInventoryEntry buffers.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(RunConfigBootstrapSystem))]
    public partial class RewardRegistryBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            if (_initialized) return;

            // Load pools from Resources/
            var rewardPool = Resources.Load<RewardPoolSO>("RewardPool");
            var eventPool = Resources.Load<EventPoolSO>("EventPool");

            var registry = new RewardRegistryManaged
            {
                DefaultRewardPool = rewardPool,
                DefaultEventPool = eventPool
            };

            // Build reward lookup from pool entries
            if (rewardPool != null && rewardPool.Entries != null)
            {
                foreach (var entry in rewardPool.Entries)
                {
                    if (entry.Reward != null && !registry.RewardById.ContainsKey(entry.Reward.RewardId))
                        registry.RewardById[entry.Reward.RewardId] = entry.Reward;
                }
            }

            // Add from event choices too
            if (eventPool != null && eventPool.Events != null)
            {
                foreach (var evt in eventPool.Events)
                {
                    if (evt.Choices == null) continue;
                    foreach (var choice in evt.Choices)
                    {
                        if (choice.Reward != null && !registry.RewardById.ContainsKey(choice.Reward.RewardId))
                            registry.RewardById[choice.Reward.RewardId] = choice.Reward;
                    }
                }
            }

            var singletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentObject(singletonEntity, registry);

            // Ensure RunState entity has reward buffers
            if (SystemAPI.TryGetSingletonEntity<RunState>(out var runEntity))
            {
                if (!EntityManager.HasBuffer<PendingRewardChoice>(runEntity))
                    EntityManager.AddBuffer<PendingRewardChoice>(runEntity);
                if (!EntityManager.HasBuffer<ShopInventoryEntry>(runEntity))
                    EntityManager.AddBuffer<ShopInventoryEntry>(runEntity);
            }

            _initialized = true;
        }
    }
}
