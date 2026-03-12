using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Loot.Components;
using DIG.Loot.Definitions;
using DIG.Loot.Systems;
using DIG.Roguelite;
using Player.Components;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Shared reward application logic used by RewardSelectionSystem and ShopPurchaseSystem.
    /// Handles all RewardType cases. Uses EntityManager directly (no SystemAPI — not inside a system).
    /// </summary>
    public static class RewardApplicationUtility
    {
        public static void Apply(
            RewardDefinitionSO def,
            PendingRewardChoice choice,
            ref RunState runState,
            Entity runEntity,
            EntityManager em)
        {
            switch (def.Type)
            {
                case RewardType.RunCurrency:
                    runState.RunCurrency += choice.IntValue;
                    break;

                case RewardType.MetaCurrency:
                    ApplyMetaCurrency(choice.IntValue, em);
                    break;

                case RewardType.Item:
                    ApplyItem(def, runEntity, em);
                    break;

                case RewardType.Healing:
                    ApplyHealing(choice.FloatValue, em);
                    break;

                case RewardType.MaxHPUp:
                    ApplyMaxHPUp(choice.IntValue, em);
                    break;

                case RewardType.Modifier:
                    ApplyModifier(def, runEntity, em);
                    break;

                case RewardType.StatBoost:
                case RewardType.AbilityUnlock:
                    // Game-specific — no-op in framework. Game bridges consume PendingRewardChoice directly.
                    break;
            }
        }

        private static void ApplyMetaCurrency(int amount, EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<MetaBank>());
            if (query.IsEmptyIgnoreFilter) return;

            var entity = query.GetSingletonEntity();
            var bank = em.GetComponentData<MetaBank>(entity);
            bank.MetaCurrency += amount;
            em.SetComponentData(entity, bank);
        }

        private static void ApplyItem(RewardDefinitionSO def, Entity runEntity, EntityManager em)
        {
            if (def.LootTable == null) return;

            var context = new LootContext
            {
                Level = 1,
                DifficultyMultiplier = 1f,
                LuckModifier = 0f,
                RandomSeed = (uint)(runEntity.Index + 1)
            };

            var drops = new NativeList<LootDrop>(4, Allocator.Temp);
            LootTableResolver.Resolve(def.LootTable, context, ref drops);

            float3 spawnPos = float3.zero;
            using var playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<LocalTransform>());
            if (!playerQuery.IsEmptyIgnoreFilter)
            {
                spawnPos = playerQuery.GetSingleton<LocalTransform>().Position + new float3(0, 0, 2f);
            }

            var lootEntity = em.CreateEntity();
            em.AddBuffer<PendingLootSpawn>(lootEntity);
            var buffer = em.GetBuffer<PendingLootSpawn>(lootEntity);

            for (int i = 0; i < drops.Length; i++)
            {
                var d = drops[i];
                buffer.Add(new PendingLootSpawn
                {
                    ItemTypeId = d.ItemTypeId,
                    Quantity = d.Quantity,
                    Rarity = d.Rarity,
                    Type = d.Type,
                    Currency = d.Currency,
                    Resource = d.Resource,
                    SpawnPosition = spawnPos
                });
            }

            drops.Dispose();
        }

        private static void ApplyHealing(float healPercent, EntityManager em)
        {
            if (healPercent <= 0f) healPercent = 0.5f;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<Health>(), ComponentType.ReadOnly<PlayerTag>());
            if (query.IsEmptyIgnoreFilter) return;

            var playerEntity = query.GetSingletonEntity();
            var health = em.GetComponentData<Health>(playerEntity);
            float healAmount = health.Max * healPercent;
            health.Current = math.min(health.Max, health.Current + healAmount);
            em.SetComponentData(playerEntity, health);
        }

        private static void ApplyMaxHPUp(int amount, EntityManager em)
        {
            if (amount <= 0) amount = 10;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<Health>(), ComponentType.ReadOnly<PlayerTag>());
            if (query.IsEmptyIgnoreFilter) return;

            var playerEntity = query.GetSingletonEntity();
            var health = em.GetComponentData<Health>(playerEntity);
            health.Max += amount;
            health.Current += amount;
            em.SetComponentData(playerEntity, health);
        }

        /// <summary>
        /// Uses 23.4's ModifierAcquisitionRequest (IEnableableComponent on RunState entity)
        /// instead of creating transient AddModifierRequest entities.
        /// </summary>
        private static void ApplyModifier(RewardDefinitionSO def, Entity runEntity, EntityManager em)
        {
            if (def.Modifier == null) return;

            if (em.HasComponent<ModifierAcquisitionRequest>(runEntity))
            {
                em.SetComponentData(runEntity, new ModifierAcquisitionRequest
                {
                    ModifierId = def.Modifier.ModifierId
                });
                em.SetComponentEnabled<ModifierAcquisitionRequest>(runEntity, true);
            }
        }
    }
}
