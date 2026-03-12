using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Provider interface for game-specific meta-progression UI.
    /// Games implement this on a MonoBehaviour to display unlock tree, currency, stats, and history.
    /// </summary>
    public interface IMetaUIProvider
    {
        /// <summary>Called when meta-currency changes (after run end conversion or unlock purchase).</summary>
        void OnMetaCurrencyChanged(int newBalance, int delta);

        /// <summary>Called when an unlock is purchased successfully.</summary>
        void OnUnlockPurchased(int unlockId, MetaUnlockCategory category, int remainingCurrency);

        /// <summary>Called when an unlock purchase fails.</summary>
        void OnUnlockPurchaseFailed(int unlockId, string reason);

        /// <summary>Called every frame during MetaScreen phase with current bank state.</summary>
        void UpdateMetaScreen(int metaCurrency, int totalRunsAttempted, int totalRunsWon,
            int bestScore, byte bestZoneReached, float totalPlaytime);

        /// <summary>Called when transitioning to MetaScreen with run results.</summary>
        void OnRunResultsReady(int metaCurrencyEarned, int totalMetaCurrency,
            RunEndReason endReason, int finalScore, byte zonesCleared);
    }

    /// <summary>
    /// EPIC 23.2: Central registry for meta-progression UI providers.
    /// Follows RunUIRegistry pattern: single provider, register/unregister with replacement warning.
    /// </summary>
    public static class MetaUIRegistry
    {
        private static IMetaUIProvider _provider;

        public static IMetaUIProvider Provider => _provider;
        public static bool HasProvider => _provider != null;

        public static void Register(IMetaUIProvider provider)
        {
            if (_provider != null && provider != null)
                Debug.LogWarning("[MetaUIRegistry] Replacing existing meta UI provider.");
            _provider = provider;
        }

        public static void Unregister(IMetaUIProvider provider)
        {
            if (_provider == provider)
                _provider = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _provider = null;
        }
    }
}
