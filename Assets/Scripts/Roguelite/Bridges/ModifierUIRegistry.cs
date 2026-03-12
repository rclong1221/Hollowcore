using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Provider interface for game-specific modifier selection UI.
    /// Games implement this on a MonoBehaviour to display modifier choices and active modifiers.
    /// </summary>
    public interface IModifierUIProvider
    {
        /// <summary>Called when modifier choices are generated (zone transition). Read PendingModifierChoice buffer for details.</summary>
        void OnModifierChoicesReady(int choiceCount);

        /// <summary>Called when a modifier is acquired (added to RunModifierStack).</summary>
        void OnModifierAcquired(int modifierId);

        /// <summary>Called every frame with current modifier count and composite difficulty.</summary>
        void UpdateModifierDisplay(int activeModifierCount, float zoneDifficultyMultiplier);
    }

    /// <summary>
    /// EPIC 23.4: Central registry for modifier UI providers.
    /// Follows MetaUIRegistry pattern: single provider, register/unregister with replacement warning.
    /// </summary>
    public static class ModifierUIRegistry
    {
        private static IModifierUIProvider _provider;

        public static IModifierUIProvider Provider => _provider;
        public static bool HasProvider => _provider != null;

        public static void Register(IModifierUIProvider provider)
        {
            if (_provider != null && provider != null)
                Debug.LogWarning("[ModifierUIRegistry] Replacing existing modifier UI provider.");
            _provider = provider;
        }

        public static void Unregister(IModifierUIProvider provider)
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
