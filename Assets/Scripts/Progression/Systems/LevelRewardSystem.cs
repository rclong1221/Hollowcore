using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Components;
using DIG.Economy;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Processes level-up rewards from LevelRewardsBlob.
    /// Reads LevelUpEvent, distributes gold, recipe unlocks, bonus stat points, etc.
    /// Disables LevelUpEvent after processing.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LevelUpSystem))]
    public partial class LevelRewardSystem : SystemBase
    {
        private EntityQuery _levelUpQuery;

        protected override void OnCreate()
        {
            _levelUpQuery = GetEntityQuery(
                ComponentType.ReadOnly<LevelUpEvent>(),
                ComponentType.ReadWrite<PlayerProgression>(),
                ComponentType.ReadOnly<CharacterAttributes>());
            RequireForUpdate<ProgressionConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<ProgressionConfigSingleton>();
            ref var rewardsBlob = ref config.Rewards.Value;

            var entities = _levelUpQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];

                // Only process enabled LevelUpEvent
                if (!EntityManager.IsComponentEnabled<LevelUpEvent>(entity))
                    continue;

                var levelUp = EntityManager.GetComponentData<LevelUpEvent>(entity);

                // Distribute rewards for each level gained
                for (int lvl = levelUp.PreviousLevel + 1; lvl <= levelUp.NewLevel; lvl++)
                {
                    for (int r = 0; r < rewardsBlob.Rewards.Length; r++)
                    {
                        ref var reward = ref rewardsBlob.Rewards[r];
                        if (reward.Level != lvl) continue;

                        switch (reward.RewardType)
                        {
                            case LevelRewardType.StatPoints:
                                var prog = EntityManager.GetComponentData<PlayerProgression>(entity);
                                prog.UnspentStatPoints += reward.IntValue;
                                EntityManager.SetComponentData(entity, prog);
                                break;

                            case LevelRewardType.CurrencyGold:
                                if (SystemAPI.HasBuffer<CurrencyTransaction>(entity))
                                {
                                    var transactions = SystemAPI.GetBuffer<CurrencyTransaction>(entity);
                                    transactions.Add(new CurrencyTransaction
                                    {
                                        Type = CurrencyType.Gold,
                                        Amount = reward.IntValue,
                                        Source = entity
                                    });
                                }
                                break;

                            case LevelRewardType.RecipeUnlock:
                                if (SystemAPI.HasComponent<DIG.Crafting.CraftingKnowledgeLink>(entity))
                                {
                                    var craftLink = SystemAPI.GetComponent<DIG.Crafting.CraftingKnowledgeLink>(entity);
                                    if (craftLink.KnowledgeEntity != Entity.Null
                                        && SystemAPI.HasBuffer<DIG.Crafting.RecipeUnlockRequest>(craftLink.KnowledgeEntity))
                                    {
                                        var unlockBuffer = SystemAPI.GetBuffer<DIG.Crafting.RecipeUnlockRequest>(craftLink.KnowledgeEntity);
                                        unlockBuffer.Add(new DIG.Crafting.RecipeUnlockRequest { RecipeId = reward.IntValue });
                                    }
                                }
                                break;

                            case LevelRewardType.AbilityUnlock:
                                // EPIC 16.14 stub: Future ability unlock system integration
                                break;

                            case LevelRewardType.ContentGate:
                                // EPIC 16.14 stub: Future content gating system
                                break;

                            case LevelRewardType.ResourceMaxUp:
                                // Handled by LevelStatScalingSystem via base stat blob
                                break;

                            case LevelRewardType.TalentPoint:
                                if (SystemAPI.HasComponent<DIG.SkillTree.TalentLink>(entity))
                                {
                                    var talentLink = SystemAPI.GetComponent<DIG.SkillTree.TalentLink>(entity);
                                    if (talentLink.TalentChild != Entity.Null
                                        && SystemAPI.HasComponent<DIG.SkillTree.TalentState>(talentLink.TalentChild))
                                    {
                                        var talentState = SystemAPI.GetComponent<DIG.SkillTree.TalentState>(talentLink.TalentChild);
                                        talentState.TotalTalentPoints += reward.IntValue;
                                        SystemAPI.SetComponent(talentLink.TalentChild, talentState);
                                    }
                                }
                                break;

                            case LevelRewardType.Title:
                                // EPIC 16.14 stub: Future title/achievement system
                                break;
                        }
                    }
                }

                // Enqueue visual event
                LevelUpVisualQueue.EnqueueLevelUp(levelUp.NewLevel, levelUp.PreviousLevel,
                    config.Curve.Value.StatPointsPerLevel * (levelUp.NewLevel - levelUp.PreviousLevel));

                // Disable LevelUpEvent (consumed)
                EntityManager.SetComponentEnabled<LevelUpEvent>(entity, false);
            }

            entities.Dispose();
        }
    }
}
