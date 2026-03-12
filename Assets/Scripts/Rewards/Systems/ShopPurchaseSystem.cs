using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Processes ShopPurchaseRequest. Validates RunCurrency, deducts, applies reward, marks IsSoldOut.
    /// Uses shared RewardApplicationUtility for reward effects.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ShopGenerationSystem))]
    public partial class ShopPurchaseSystem : SystemBase
    {
        private EntityQuery _requestQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            _requestQuery = GetEntityQuery(
                ComponentType.ReadWrite<RunState>(),
                ComponentType.ReadOnly<ShopPurchaseRequest>());
        }

        protected override void OnUpdate()
        {
            if (_requestQuery.IsEmptyIgnoreFilter)
                return;

            if (!SystemAPI.ManagedAPI.HasSingleton<RewardRegistryManaged>())
                return;

            var registry = SystemAPI.ManagedAPI.GetSingleton<RewardRegistryManaged>();
            var entities = _requestQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int e = 0; e < entities.Length; e++)
            {
                var entity = entities[e];
                var request = EntityManager.GetComponentData<ShopPurchaseRequest>(entity);
                int slotIndex = request.ShopSlotIndex;

                // Remove request regardless of outcome
                EntityManager.RemoveComponent<ShopPurchaseRequest>(entity);

                if (!EntityManager.HasBuffer<ShopInventoryEntry>(entity))
                    continue;

                var shopBuffer = EntityManager.GetBuffer<ShopInventoryEntry>(entity);
                if (slotIndex < 0 || slotIndex >= shopBuffer.Length)
                    continue;

                var entry = shopBuffer[slotIndex];
                if (entry.IsSoldOut)
                    continue;

                var runState = EntityManager.GetComponentData<RunState>(entity);
                if (runState.RunCurrency < entry.Price)
                    continue;

                if (!registry.RewardById.TryGetValue(entry.RewardId, out var rewardDef))
                    continue;

                // Deduct currency
                runState.RunCurrency -= entry.Price;

                // Apply reward via shared utility
                var choice = new PendingRewardChoice
                {
                    RewardId = entry.RewardId,
                    Type = entry.Type,
                    Rarity = entry.Rarity,
                    IntValue = entry.IntValue,
                    FloatValue = entry.FloatValue
                };
                RewardApplicationUtility.Apply(rewardDef, choice, ref runState, entity, EntityManager);
                EntityManager.SetComponentData(entity, runState);

                // Mark sold out
                entry.IsSoldOut = true;
                shopBuffer[slotIndex] = entry;

                LogPurchase(rewardDef.DisplayName, entry.Price);
            }

            entities.Dispose();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPurchase(string name, int price)
        {
            Debug.Log($"[ShopPurchase] Bought '{name}' for {price} RunCurrency");
        }
    }
}
