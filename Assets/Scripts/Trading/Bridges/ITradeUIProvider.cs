namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Interface for trade UI providers.
    /// MonoBehaviours implement this and register with TradeUIRegistry.
    /// Follows IPartyUIProvider pattern.
    /// </summary>
    public interface ITradeUIProvider
    {
        /// <summary>Incoming trade request from another player (show accept/decline popup).</summary>
        void OnTradeRequested(int requesterGhostId);
        /// <summary>Trade session became active — open trade window.</summary>
        void OnTradeSessionStarted();
        /// <summary>Trade session was cancelled — close trade window.</summary>
        void OnTradeSessionCancelled(TradeCancelReason reason);
        /// <summary>Trade completed successfully or failed.</summary>
        void OnTradeCompleted(bool success);
        /// <summary>An offer was updated — refresh offer display panels.
        /// Arrays are reused buffers; only read indices [0..count-1].</summary>
        void OnOfferUpdated(TradeOfferSnapshot[] myOffers, int myCount, TradeOfferSnapshot[] theirOffers, int theirCount);
        /// <summary>Confirmation state changed — update confirm button states.</summary>
        void OnConfirmStateChanged(bool iConfirmed, bool theyConfirmed);
    }
}
