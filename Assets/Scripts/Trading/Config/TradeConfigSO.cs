using UnityEngine;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: ScriptableObject for trade system configuration.
    /// Create at Resources/TradeConfig via Assets menu.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Trading/Trade Config", fileName = "TradeConfig")]
    public class TradeConfigSO : ScriptableObject
    {
        [Header("Offer Limits")]
        [Tooltip("Max distinct item entries per side in a single trade.")]
        [Min(1)] public int MaxItemsPerOffer = 8;
        [Tooltip("Max currency types per side (Gold + Premium + Crafting = 3).")]
        [Min(0)] public int MaxCurrencyPerOffer = 3;

        [Header("Proximity")]
        [Tooltip("Max meters between traders to initiate or maintain a trade.")]
        [Min(1f)] public float ProximityRange = 10f;

        [Header("Timing")]
        [Tooltip("Seconds before an inactive trade session auto-cancels.")]
        [Min(10f)] public float TimeoutSeconds = 120f;
        [Tooltip("Minimum seconds between trade requests from the same player.")]
        [Min(1f)] public float CooldownSeconds = 5f;

        [Header("Session Limits")]
        [Tooltip("Max concurrent trades per player. Should be 1 for most games.")]
        [Min(1)] public int MaxActiveTradesPerPlayer = 1;

        [Header("Currency")]
        [Tooltip("Whether Premium currency can be traded between players.")]
        public bool AllowPremiumCurrencyTrade = false;
    }
}
