using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Static singleton registry for party UI providers.
    /// Follows CombatUIRegistry / TalentUIRegistry pattern.
    /// MonoBehaviours register/unregister in OnEnable/OnDisable.
    /// </summary>
    public static class PartyUIRegistry
    {
        public static IPartyUIProvider PartyUI { get; private set; }
        public static bool HasPartyUI => PartyUI != null;

        public static void RegisterPartyUI(IPartyUIProvider provider) => PartyUI = provider;
        public static void UnregisterPartyUI(IPartyUIProvider provider)
        {
            if (PartyUI == provider) PartyUI = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            PartyUI = null;
        }
    }
}
