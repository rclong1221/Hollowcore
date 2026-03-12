using UnityEngine;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Static singleton registry for trade UI providers.
    /// Follows PartyUIRegistry / CombatUIRegistry pattern.
    /// MonoBehaviours register in OnEnable, unregister in OnDisable.
    /// </summary>
    public static class TradeUIRegistry
    {
        public static ITradeUIProvider TradeUI { get; private set; }
        public static bool HasTradeUI => TradeUI != null;

        public static void Register(ITradeUIProvider provider) => TradeUI = provider;
        public static void Unregister(ITradeUIProvider provider)
        {
            if (TradeUI == provider) TradeUI = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            TradeUI = null;
        }
    }
}
