using Unity.Entities;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Processes queued reward/shop requests from UI. Adds request components to RunState.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(RewardSelectionSystem))]
    public partial class RewardInputBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<RunState>(out var runEntity))
                return;

            int rewardSlot = RewardUIRegistry.ConsumeQueuedRewardSelection();
            if (rewardSlot >= 0)
            {
                if (!EntityManager.HasComponent<RewardSelectionRequest>(runEntity))
                    EntityManager.AddComponentData(runEntity, new RewardSelectionRequest { SlotIndex = rewardSlot });
            }

            int shopSlot = RewardUIRegistry.ConsumeQueuedShopPurchase();
            if (shopSlot >= 0)
            {
                if (!EntityManager.HasComponent<ShopPurchaseRequest>(runEntity))
                    EntityManager.AddComponentData(runEntity, new ShopPurchaseRequest { ShopSlotIndex = shopSlot });
            }
        }
    }
}
