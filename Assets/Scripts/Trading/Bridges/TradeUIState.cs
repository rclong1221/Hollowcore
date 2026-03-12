using DIG.Shared;
using DIG.Economy;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Snapshot of a single trade offer entry for UI display.
    /// </summary>
    public struct TradeOfferSnapshot
    {
        public TradeOfferType OfferType;
        public ResourceType ItemType;
        public int Quantity;
        public CurrencyType CurrencyType;
        public int CurrencyAmount;
    }

    /// <summary>
    /// EPIC 17.3: Visual event types for the TradeVisualQueue.
    /// </summary>
    public enum TradeVisualEventType : byte
    {
        TradeRequested = 0,
        SessionStarted = 1,
        SessionCancelled = 2,
        TradeCompleted = 3,
        TradeFailed = 4,
        OfferUpdated = 5,
        ConfirmChanged = 6
    }
}
