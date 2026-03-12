using UnityEngine;
using UnityEngine.UI;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Displays one side's trade offers (items + currencies).
    /// "My Offer" panel is editable (sends TradeOfferUpdateRpc on change).
    /// "Their Offer" panel is read-only.
    /// </summary>
    public class TradeOfferPanel : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool _isEditable;

        [Header("Display")]
        [SerializeField] private Transform _itemSlotContainer;
        [SerializeField] private Text _goldAmountText;
        [SerializeField] private Text _premiumAmountText;
        [SerializeField] private Text _craftingAmountText;
        [SerializeField] private Text _emptyText;

        private TradeOfferSnapshot[] _currentOffers;
        private int _currentOfferCount;

        public void DisplayOffers(TradeOfferSnapshot[] offers, int count)
        {
            _currentOffers = offers;
            _currentOfferCount = count;

            int goldAmount = 0, premiumAmount = 0, craftingAmount = 0;
            int itemCount = 0;

            if (offers != null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (offers[i].OfferType == TradeOfferType.Item)
                    {
                        itemCount++;
                    }
                    else
                    {
                        switch (offers[i].CurrencyType)
                        {
                            case DIG.Economy.CurrencyType.Gold: goldAmount = offers[i].CurrencyAmount; break;
                            case DIG.Economy.CurrencyType.Premium: premiumAmount = offers[i].CurrencyAmount; break;
                            case DIG.Economy.CurrencyType.Crafting: craftingAmount = offers[i].CurrencyAmount; break;
                        }
                    }
                }
            }

            if (_goldAmountText != null) _goldAmountText.text = goldAmount > 0 ? goldAmount.ToString() : "-";
            if (_premiumAmountText != null) _premiumAmountText.text = premiumAmount > 0 ? premiumAmount.ToString() : "-";
            if (_craftingAmountText != null) _craftingAmountText.text = craftingAmount > 0 ? craftingAmount.ToString() : "-";
            if (_emptyText != null) _emptyText.gameObject.SetActive(itemCount == 0 && goldAmount == 0 && premiumAmount == 0 && craftingAmount == 0);
        }

        public void ClearOffers()
        {
            _currentOffers = null;
            if (_goldAmountText != null) _goldAmountText.text = "-";
            if (_premiumAmountText != null) _premiumAmountText.text = "-";
            if (_craftingAmountText != null) _craftingAmountText.text = "-";
            if (_emptyText != null) _emptyText.gameObject.SetActive(true);
        }

        /// <summary>
        /// Called by inventory UI to add an item to the trade offer.
        /// Only valid for editable (My Offer) panel.
        /// </summary>
        public void AddItemOffer(byte slotIndex, int quantity)
        {
            if (!_isEditable) return;
            TradeRpcHelper.SendOfferUpdate(
                TradeOfferAction.Add,
                TradeOfferType.Item,
                slotIndex,
                quantity,
                default,
                0);
        }

        /// <summary>
        /// Called by currency input fields to set a currency offer.
        /// </summary>
        public void SetCurrencyOffer(DIG.Economy.CurrencyType type, int amount)
        {
            if (!_isEditable) return;

            // Check if we already have this currency type offered
            if (_currentOffers != null)
            {
                for (int i = 0; i < _currentOfferCount; i++)
                {
                    if (_currentOffers[i].OfferType == TradeOfferType.Currency &&
                        _currentOffers[i].CurrencyType == type)
                    {
                        if (amount <= 0)
                        {
                            TradeRpcHelper.SendOfferUpdate(TradeOfferAction.Remove, TradeOfferType.Currency, 0, 0, type, 0);
                        }
                        else
                        {
                            TradeRpcHelper.SendOfferUpdate(TradeOfferAction.UpdateQty, TradeOfferType.Currency, 0, 0, type, amount);
                        }
                        return;
                    }
                }
            }

            // New currency offer
            if (amount > 0)
            {
                TradeRpcHelper.SendOfferUpdate(TradeOfferAction.Add, TradeOfferType.Currency, 0, 0, type, amount);
            }
        }

        /// <summary>
        /// Called by item slot UI to remove an item from the trade offer.
        /// </summary>
        public void RemoveItemOffer(byte slotIndex)
        {
            if (!_isEditable) return;
            TradeRpcHelper.SendOfferUpdate(TradeOfferAction.Remove, TradeOfferType.Item, slotIndex, 0, default, 0);
        }
    }
}
