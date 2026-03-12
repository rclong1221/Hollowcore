using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Economy;
using DIG.Shared;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Distributes rewards when a quest transitions to TurnedIn state.
    /// Processes each quest exactly once by tracking which have been rewarded.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(QuestEvaluationSystemGroup))]
    [UpdateAfter(typeof(QuestCompletionSystem))]
    public partial class QuestRewardSystem : SystemBase
    {
        private EntityQuery _turnedInQuery;

        protected override void OnCreate()
        {
            _turnedInQuery = GetEntityQuery(
                ComponentType.ReadOnly<QuestProgress>(),
                ComponentType.ReadOnly<QuestPlayerLink>(),
                ComponentType.Exclude<QuestRewardedTag>()
            );
            RequireForUpdate<QuestRegistryManaged>();
        }

        protected override void OnUpdate()
        {
            var registry = SystemAPI.ManagedAPI.GetSingleton<QuestRegistryManaged>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            var entities = _turnedInQuery.ToEntityArray(Allocator.Temp);
            var progressArray = _turnedInQuery.ToComponentDataArray<QuestProgress>(Allocator.Temp);
            var links = _turnedInQuery.ToComponentDataArray<QuestPlayerLink>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var progress = progressArray[i];
                if (progress.State != QuestState.TurnedIn) continue;

                var def = registry.Database.GetQuest(progress.QuestId);
                if (def == null) continue;

                var playerEntity = links[i].PlayerEntity;

                // Distribute rewards
                foreach (var reward in def.Rewards)
                {
                    switch (reward.Type)
                    {
                        case QuestRewardType.Currency:
                            if (SystemAPI.HasBuffer<CurrencyTransaction>(playerEntity))
                            {
                                var transactions = SystemAPI.GetBuffer<CurrencyTransaction>(playerEntity);
                                transactions.Add(new CurrencyTransaction
                                {
                                    Type = reward.CurrencyType,
                                    Amount = reward.Value,
                                    Source = entities[i]
                                });
                            }
                            break;

                        case QuestRewardType.Item:
                            if (SystemAPI.HasBuffer<InventoryItem>(playerEntity))
                            {
                                var inventory = SystemAPI.GetBuffer<InventoryItem>(playerEntity);
                                // Try to stack with existing
                                bool stacked = false;
                                for (int inv = 0; inv < inventory.Length; inv++)
                                {
                                    if ((int)inventory[inv].ResourceType == reward.Value)
                                    {
                                        var item = inventory[inv];
                                        item.Quantity += reward.Quantity;
                                        inventory[inv] = item;
                                        stacked = true;
                                        break;
                                    }
                                }
                                if (!stacked)
                                {
                                    inventory.Add(new InventoryItem
                                    {
                                        ResourceType = (ResourceType)reward.Value,
                                        Quantity = reward.Quantity
                                    });
                                }
                            }
                            break;

                        case QuestRewardType.Experience:
                            // EPIC 16.14: Grant quest XP via XPGrantAPI
                            DIG.Progression.XPGrantAPI.GrantXP(EntityManager, playerEntity,
                                reward.Value, DIG.Progression.XPSourceType.Quest);
                            break;

                        case QuestRewardType.RecipeUnlock:
                            // EPIC 16.13: Write recipe unlock to player's crafting knowledge child
                            if (SystemAPI.HasComponent<DIG.Crafting.CraftingKnowledgeLink>(playerEntity))
                            {
                                var craftLink = SystemAPI.GetComponent<DIG.Crafting.CraftingKnowledgeLink>(playerEntity);
                                if (craftLink.KnowledgeEntity != Entity.Null
                                    && SystemAPI.HasBuffer<DIG.Crafting.RecipeUnlockRequest>(craftLink.KnowledgeEntity))
                                {
                                    var unlockBuffer = SystemAPI.GetBuffer<DIG.Crafting.RecipeUnlockRequest>(craftLink.KnowledgeEntity);
                                    unlockBuffer.Add(new DIG.Crafting.RecipeUnlockRequest { RecipeId = reward.Value });
                                }
                            }
                            break;
                    }
                }

                // Record completion on player
                if (SystemAPI.HasBuffer<CompletedQuestEntry>(playerEntity))
                {
                    var completed = SystemAPI.GetBuffer<CompletedQuestEntry>(playerEntity);
                    completed.Add(new CompletedQuestEntry
                    {
                        QuestId = progress.QuestId,
                        CompletedAtTick = tick
                    });
                }

                // Mark as rewarded so we don't process again
                ecb.AddComponent<QuestRewardedTag>(entities[i]);
            }

            entities.Dispose();
            progressArray.Dispose();
            links.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// EPIC 16.12: Tag to mark quest instances that have already been rewarded.
    /// Prevents double-reward on subsequent frames.
    /// </summary>
    public struct QuestRewardedTag : IComponentData { }
}
