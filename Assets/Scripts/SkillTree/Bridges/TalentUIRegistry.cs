using UnityEngine;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Static singleton registry for talent tree UI providers.
    /// Follows ProgressionUIRegistry / CombatUIRegistry pattern.
    /// MonoBehaviours register/unregister in OnEnable/OnDisable.
    /// </summary>
    public static class TalentUIRegistry
    {
        public static ITalentUIProvider TalentUI { get; private set; }
        public static bool HasTalentUI => TalentUI != null;

        public static void RegisterTalentUI(ITalentUIProvider provider) => TalentUI = provider;
        public static void UnregisterTalentUI(ITalentUIProvider provider)
        {
            if (TalentUI == provider) TalentUI = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            TalentUI = null;
        }
    }
}
