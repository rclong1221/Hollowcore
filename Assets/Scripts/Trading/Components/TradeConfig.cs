using Unity.Entities;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Singleton configuration for the trading system.
    /// Created by TradeBootstrapSystem from TradeConfigSO.
    /// </summary>
    public struct TradeConfig : IComponentData
    {
        /// <summary>Max distinct item entries per side (default 8).</summary>
        public int MaxItemsPerOffer;
        /// <summary>Max currency types per side (default 3).</summary>
        public int MaxCurrencyPerOffer;
        /// <summary>Max distance between traders in meters (default 10).</summary>
        public float ProximityRange;
        /// <summary>Ticks before session auto-cancels (converted from seconds at bootstrap).</summary>
        public uint TimeoutTicks;
        /// <summary>Min ticks between trade requests per player (converted from seconds).</summary>
        public uint CooldownTicks;
        /// <summary>Max concurrent trades per player — should be 1.</summary>
        public int MaxActiveTradesPerPlayer;
        /// <summary>Whether Premium currency can be traded (default false).</summary>
        public bool AllowPremiumCurrencyTrade;
    }
}
