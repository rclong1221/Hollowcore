using Unity.Entities;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Tracks whether each participant has confirmed the trade.
    /// Lives on the trade session entity. Both must be true before execution.
    /// Any offer change resets the OTHER player's confirmation.
    /// </summary>
    public struct TradeConfirmState : IComponentData
    {
        public bool InitiatorConfirmed;
        public bool TargetConfirmed;
    }
}
