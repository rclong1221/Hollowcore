using System.Collections.Generic;
using Unity.Entities;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Reads pending choices, shop inventory, event state from ECS.
    /// Pushes to RewardUIRegistry for game HUD. Change detection avoids pushing every frame.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RewardUIBridgeSystem : SystemBase
    {
        // Reusable lists — avoids GC allocation per frame
        private readonly List<RewardChoiceSnapshot> _choices = new();
        private readonly List<ShopEntrySnapshot> _shopEntries = new();

        // Change detection
        private int _lastChoiceCount = -1;
        private int _lastShopCount = -1;
        private int _lastShopSoldOutMask;
        private object _lastActiveEvent;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<RunState>(out var runEntity))
                return;

            bool choicesChanged = false;
            bool shopChanged = false;

            // Check reward choices
            int choiceCount = 0;
            if (EntityManager.HasBuffer<PendingRewardChoice>(runEntity))
                choiceCount = EntityManager.GetBuffer<PendingRewardChoice>(runEntity).Length;

            if (choiceCount != _lastChoiceCount)
            {
                _lastChoiceCount = choiceCount;
                choicesChanged = true;

                _choices.Clear();
                if (choiceCount > 0)
                {
                    var buffer = EntityManager.GetBuffer<PendingRewardChoice>(runEntity);
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        var c = buffer[i];
                        _choices.Add(new RewardChoiceSnapshot
                        {
                            RewardId = c.RewardId,
                            Type = c.Type,
                            Rarity = c.Rarity,
                            IntValue = c.IntValue,
                            FloatValue = c.FloatValue,
                            SlotIndex = c.SlotIndex
                        });
                    }
                }

                RewardUIRegistry.SetPendingChoices(_choices);
            }

            // Check shop inventory (detect count changes and sold-out state changes)
            int shopCount = 0;
            int soldOutMask = 0;
            if (EntityManager.HasBuffer<ShopInventoryEntry>(runEntity))
            {
                var shopBuffer = EntityManager.GetBuffer<ShopInventoryEntry>(runEntity);
                shopCount = shopBuffer.Length;
                for (int i = 0; i < shopBuffer.Length && i < 32; i++)
                {
                    if (shopBuffer[i].IsSoldOut)
                        soldOutMask |= (1 << i);
                }
            }

            if (shopCount != _lastShopCount || soldOutMask != _lastShopSoldOutMask)
            {
                _lastShopCount = shopCount;
                _lastShopSoldOutMask = soldOutMask;
                shopChanged = true;

                _shopEntries.Clear();
                if (shopCount > 0)
                {
                    var shopBuffer = EntityManager.GetBuffer<ShopInventoryEntry>(runEntity);
                    for (int i = 0; i < shopBuffer.Length; i++)
                    {
                        var e = shopBuffer[i];
                        _shopEntries.Add(new ShopEntrySnapshot
                        {
                            RewardId = e.RewardId,
                            Type = e.Type,
                            Rarity = e.Rarity,
                            IntValue = e.IntValue,
                            FloatValue = e.FloatValue,
                            Price = e.Price,
                            IsSoldOut = e.IsSoldOut
                        });
                    }
                }

                RewardUIRegistry.SetShopEntries(_shopEntries);
            }

            // Track active event changes
            bool eventChanged = false;
            var currentEvent = RewardUIRegistry.ActiveEvent;
            if (!ReferenceEquals(currentEvent, _lastActiveEvent))
            {
                _lastActiveEvent = currentEvent;
                eventChanged = true;
            }

            // Notify provider only on changes
            if (RewardUIRegistry.HasProvider)
            {
                if (choicesChanged)
                    RewardUIRegistry.Provider.OnPendingChoicesChanged(_choices);
                if (shopChanged)
                    RewardUIRegistry.Provider.OnShopInventoryChanged(_shopEntries);
                if (eventChanged)
                    RewardUIRegistry.Provider.OnActiveEventChanged(currentEvent);
            }
        }
    }
}
