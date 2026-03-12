using UnityEngine;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Static accessor for the UI service layer.
    /// Initialized by UIServiceBootstrap. Game code accesses via UIServices.Screen.
    /// </summary>
    public static class UIServices
    {
        /// <summary>
        /// The active screen lifecycle service.
        /// Null until UIServiceBootstrap initializes it.
        /// </summary>
        public static IUIService Screen { get; internal set; }

        /// <summary>
        /// Whether the service has been initialized.
        /// </summary>
        public static bool IsInitialized => Screen != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Screen = null;
        }
    }
}
