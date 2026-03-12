using Unity.NetCode;
using DIG.Shared;
using DIG.Economy;

namespace DIG.Trading
{
    // ═══════════════════════════════════════════
    //  Client → Server RPCs
    // ═══════════════════════════════════════════

    /// <summary>
    /// EPIC 17.3: Client requests a trade with another player.
    /// Server resolves ghost ID to entity.
    /// </summary>
    public struct TradeRequestRpc : IRpcCommand
    {
        /// <summary>Ghost ID of the target player (NOT Entity — unsafe across worlds).</summary>
        public int TargetGhostId;
    }

    /// <summary>
    /// EPIC 17.3: Target player accepts a pending trade request.
    /// </summary>
    public struct TradeAcceptRpc : IRpcCommand { }

    /// <summary>
    /// EPIC 17.3: Client modifies their trade offer.
    /// </summary>
    public struct TradeOfferUpdateRpc : IRpcCommand
    {
        public TradeOfferAction Action;
        public TradeOfferType OfferType;
        /// <summary>Index into local InventoryItem buffer (for item offers).</summary>
        public byte ItemSlotIndex;
        public int Quantity;
        public CurrencyType CurrencyType;
        public int CurrencyAmount;
    }

    /// <summary>
    /// EPIC 17.3: Client confirms their side of the trade.
    /// Server resolves which player from connection.
    /// </summary>
    public struct TradeConfirmRpc : IRpcCommand { }

    /// <summary>
    /// EPIC 17.3: Client cancels the trade.
    /// </summary>
    public struct TradeCancelRpc : IRpcCommand
    {
        public TradeCancelReason Reason;
    }

    // ═══════════════════════════════════════════
    //  Server → Client Notification RPCs
    // ═══════════════════════════════════════════

    /// <summary>
    /// EPIC 17.3: Notifies target player of incoming trade request.
    /// </summary>
    public struct TradeSessionNotifyRpc : IRpcCommand
    {
        /// <summary>Ghost ID of the initiator (for display name resolution).</summary>
        public int InitiatorGhostId;
    }

    /// <summary>
    /// EPIC 17.3: Syncs an offer entry to both clients after server validation.
    /// </summary>
    public struct TradeOfferSyncRpc : IRpcCommand
    {
        /// <summary>Whose offer changed: 0=initiator, 1=target.</summary>
        public byte OfferSide;
        public TradeOfferAction Action;
        public TradeOfferType OfferType;
        public byte ItemSlotIndex;
        public ResourceType ItemType;
        public int Quantity;
        public CurrencyType CurrencyType;
        public int CurrencyAmount;
    }

    /// <summary>
    /// EPIC 17.3: Notifies clients of trade session state changes.
    /// </summary>
    public struct TradeStateNotifyRpc : IRpcCommand
    {
        public TradeState NewState;
        /// <summary>0 = none, 1+ = specific failure/cancel reason.</summary>
        public byte FailReason;
    }

    // ═══════════════════════════════════════════
    //  Enums
    // ═══════════════════════════════════════════

    /// <summary>
    /// EPIC 17.3: Actions for modifying a trade offer.
    /// </summary>
    public enum TradeOfferAction : byte
    {
        Add = 0,
        Remove = 1,
        UpdateQty = 2
    }

    /// <summary>
    /// EPIC 17.3: Reasons a trade session was cancelled.
    /// </summary>
    public enum TradeCancelReason : byte
    {
        Voluntary = 0,
        Disconnect = 1,
        Timeout = 2,
        TooFar = 3,
        EnteredCombat = 4,
        InvalidSession = 5
    }
}
