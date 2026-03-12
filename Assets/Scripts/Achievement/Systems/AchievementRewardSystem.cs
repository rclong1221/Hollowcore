using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Reads AchievementUnlockEvent transient entities and distributes rewards
    /// via existing pipelines (CurrencyInventory, XPGrantAPI, PlayerProgression).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(AchievementUnlockSystem))]
    public partial class AchievementRewardSystem : SystemBase
    {
        private EntityQuery _unlockEventQuery;
        private EntityQuery _registryQuery;
        private AchievementLookupMaps _lookupMaps;

        protected override void OnCreate()
        {
            _unlockEventQuery = GetEntityQuery(ComponentType.ReadOnly<AchievementUnlockEvent>());
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<AchievementRegistrySingleton>());

            RequireForUpdate(_unlockEventQuery);
            RequireForUpdate(_registryQuery);
        }

        protected override void OnUpdate()
        {
            var registry = _registryQuery.GetSingleton<AchievementRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;

            // Fetch lookup maps
            if (_lookupMaps == null || !_lookupMaps.IdToIndex.IsCreated)
            {
                var regEntities = _registryQuery.ToEntityArray(Allocator.Temp);
                if (regEntities.Length > 0 && EntityManager.HasComponent<AchievementLookupMaps>(regEntities[0]))
                    _lookupMaps = EntityManager.GetComponentObject<AchievementLookupMaps>(regEntities[0]);
                regEntities.Dispose();
            }

            var events = _unlockEventQuery.ToComponentDataArray<AchievementUnlockEvent>(Allocator.Temp);

            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                var playerEntity = evt.PlayerId;

                if (playerEntity == Entity.Null || !EntityManager.Exists(playerEntity)) continue;

                // Find definition and tier reward — O(1) via lookup map
                int defIndex = FindDefinitionIndex(ref blob, evt.AchievementId);
                if (defIndex < 0) continue;

                ref var def = ref blob.Definitions[defIndex];
                int tierIndex = evt.Tier - 1; // Tier is 1-based (Bronze=1)
                if (tierIndex < 0 || tierIndex >= def.Tiers.Length) continue;

                ref var tierReward = ref def.Tiers[tierIndex];

                switch (tierReward.RewardType)
                {
                    case AchievementRewardType.Gold:
                        if (EntityManager.HasComponent<DIG.Economy.CurrencyInventory>(playerEntity))
                        {
                            var currency = EntityManager.GetComponentData<DIG.Economy.CurrencyInventory>(playerEntity);
                            currency.Gold += tierReward.RewardIntValue;
                            EntityManager.SetComponentData(playerEntity, currency);
                            LogReward($"[Achievement] Reward: +{tierReward.RewardIntValue} Gold for '{def.Name}'");
                        }
                        break;

                    case AchievementRewardType.XP:
                        DIG.Progression.XPGrantAPI.GrantXP(
                            EntityManager,
                            playerEntity,
                            tierReward.RewardIntValue,
                            DIG.Progression.XPSourceType.Bonus
                        );
                        LogReward($"[Achievement] Reward: +{tierReward.RewardIntValue} XP for '{def.Name}'");
                        break;

                    case AchievementRewardType.TalentPoints:
                        if (EntityManager.HasComponent<DIG.Progression.PlayerProgression>(playerEntity))
                        {
                            var prog = EntityManager.GetComponentData<DIG.Progression.PlayerProgression>(playerEntity);
                            prog.UnspentStatPoints += tierReward.RewardIntValue;
                            EntityManager.SetComponentData(playerEntity, prog);
                            LogReward($"[Achievement] Reward: +{tierReward.RewardIntValue} Talent Points for '{def.Name}'");
                        }
                        break;

                    case AchievementRewardType.RecipeUnlock:
                        if (EntityManager.HasComponent<DIG.Crafting.CraftingKnowledgeLink>(playerEntity))
                        {
                            var link = EntityManager.GetComponentData<DIG.Crafting.CraftingKnowledgeLink>(playerEntity);
                            if (EntityManager.HasBuffer<DIG.Crafting.KnownRecipeElement>(link.KnowledgeEntity))
                            {
                                var recipes = EntityManager.GetBuffer<DIG.Crafting.KnownRecipeElement>(link.KnowledgeEntity);
                                // Check for duplicate
                                bool alreadyKnown = false;
                                for (int r = 0; r < recipes.Length; r++)
                                {
                                    if (recipes[r].RecipeId == tierReward.RewardIntValue)
                                    {
                                        alreadyKnown = true;
                                        break;
                                    }
                                }
                                if (!alreadyKnown)
                                {
                                    recipes.Add(new DIG.Crafting.KnownRecipeElement { RecipeId = tierReward.RewardIntValue });
                                    LogReward($"[Achievement] Reward: Unlocked recipe {tierReward.RewardIntValue} for '{def.Name}'");
                                }
                            }
                        }
                        break;

                    case AchievementRewardType.Title:
                        LogReward($"[Achievement] Reward: Title {tierReward.RewardIntValue} for '{def.Name}' (title system pending)");
                        break;

                    case AchievementRewardType.Cosmetic:
                        LogReward($"[Achievement] Reward: Cosmetic {tierReward.RewardIntValue} for '{def.Name}' (cosmetic system pending)");
                        break;

                    case AchievementRewardType.StatBonus:
                        LogReward($"[Achievement] Reward: StatBonus {tierReward.RewardFloatValue:P0} for '{def.Name}' (stat bonus system pending)");
                        break;
                }
            }

            events.Dispose();
        }

        private int FindDefinitionIndex(ref AchievementRegistryBlob blob, ushort achievementId)
        {
            if (_lookupMaps != null && _lookupMaps.IdToIndex.IsCreated)
            {
                if (_lookupMaps.IdToIndex.TryGetValue(achievementId, out int idx))
                    return idx;
                return -1;
            }
            for (int i = 0; i < blob.TotalAchievements; i++)
            {
                if (blob.Definitions[i].AchievementId == achievementId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Logging helper — stripped from non-development builds via [Conditional].
        /// Prevents string allocation + Debug.Log overhead in release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogReward(string message)
        {
            UnityEngine.Debug.Log(message);
        }
    }
}
