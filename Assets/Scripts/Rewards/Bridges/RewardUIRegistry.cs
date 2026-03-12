using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Snapshot of a single reward choice for UI display.
    /// </summary>
    public struct RewardChoiceSnapshot
    {
        public int RewardId;
        public RewardType Type;
        public byte Rarity;
        public int IntValue;
        public float FloatValue;
        public int SlotIndex;
    }

    /// <summary>
    /// EPIC 23.5: Snapshot of a shop entry for UI display.
    /// </summary>
    public struct ShopEntrySnapshot
    {
        public int RewardId;
        public RewardType Type;
        public byte Rarity;
        public int IntValue;
        public float FloatValue;
        public int Price;
        public bool IsSoldOut;
    }

    /// <summary>
    /// EPIC 23.5: Provider interface for reward/shop/event UI.
    /// Games implement to display choice cards, shop panels, event dialogs.
    /// </summary>
    public interface IRewardUIProvider
    {
        void OnPendingChoicesChanged(IReadOnlyList<RewardChoiceSnapshot> choices);
        void OnShopInventoryChanged(IReadOnlyList<ShopEntrySnapshot> entries);
        void OnActiveEventChanged(RunEventDefinitionSO evt);
    }

    /// <summary>
    /// EPIC 23.5: Static registry for reward UI. RewardUIBridgeSystem pushes ECS state here.
    /// </summary>
    public static class RewardUIRegistry
    {
        private static IRewardUIProvider _provider;
        private static readonly List<RewardChoiceSnapshot> _pendingChoices = new();
        private static readonly List<ShopEntrySnapshot> _shopEntries = new();
        private static RunEventDefinitionSO _activeEvent;

        public static IRewardUIProvider Provider => _provider;
        public static bool HasProvider => _provider != null;

        public static IReadOnlyList<RewardChoiceSnapshot> PendingChoices => _pendingChoices;
        public static IReadOnlyList<ShopEntrySnapshot> ShopEntries => _shopEntries;
        public static RunEventDefinitionSO ActiveEvent => _activeEvent;

        public static void Register(IRewardUIProvider provider)
        {
            if (_provider != null && provider != null)
                Debug.LogWarning("[RewardUIRegistry] Replacing existing reward UI provider.");
            _provider = provider;
        }

        public static void Unregister(IRewardUIProvider provider)
        {
            if (_provider == provider)
                _provider = null;
        }

        public static void SetPendingChoices(IReadOnlyList<RewardChoiceSnapshot> choices)
        {
            _pendingChoices.Clear();
            if (choices != null)
            {
                foreach (var c in choices)
                    _pendingChoices.Add(c);
            }
        }

        public static void SetShopEntries(IReadOnlyList<ShopEntrySnapshot> entries)
        {
            _shopEntries.Clear();
            if (entries != null)
            {
                foreach (var e in entries)
                    _shopEntries.Add(e);
            }
        }

        public static void SetActiveEvent(RunEventDefinitionSO evt)
        {
            _activeEvent = evt;
        }

        /// <summary>Queue a reward selection for next frame. Called by UI.</summary>
        public static void QueueRewardSelection(int slotIndex)
        {
            _queuedRewardSlot = slotIndex;
        }

        /// <summary>Queue a shop purchase for next frame. Called by UI.</summary>
        public static void QueueShopPurchase(int shopSlotIndex)
        {
            _queuedShopSlot = shopSlotIndex;
        }

        internal static int ConsumeQueuedRewardSelection()
        {
            var v = _queuedRewardSlot;
            _queuedRewardSlot = -1;
            return v;
        }

        internal static int ConsumeQueuedShopPurchase()
        {
            var v = _queuedShopSlot;
            _queuedShopSlot = -1;
            return v;
        }

        private static int _queuedRewardSlot = -1;
        private static int _queuedShopSlot = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _provider = null;
            _pendingChoices.Clear();
            _shopEntries.Clear();
            _activeEvent = null;
            _queuedRewardSlot = -1;
            _queuedShopSlot = -1;
        }
    }
}
