using Unity.Entities;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Rate-limiting cooldown tracked per connection entity.
    /// ICleanupComponentData survives entity destruction for proper cleanup.
    /// Added by TradeRequestReceiveSystem on first trade request.
    /// </summary>
    public struct TradePlayerCooldown : ICleanupComponentData
    {
        /// <summary>NetworkTick of the last trade request sent by this connection.</summary>
        public uint LastTradeRequestTick;
    }
}
