using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Static singleton registry for progression UI providers.
    /// Follows CombatUIRegistry pattern. MonoBehaviours register/unregister in OnEnable/OnDisable.
    /// </summary>
    public static class ProgressionUIRegistry
    {
        public static IXPBarProvider XPBar { get; private set; }
        public static ILevelUpPopupProvider LevelUpPopup { get; private set; }
        public static IXPGainProvider XPGain { get; private set; }
        public static IStatAllocationProvider StatAllocation { get; private set; }

        public static void RegisterXPBar(IXPBarProvider provider) => XPBar = provider;
        public static void UnregisterXPBar(IXPBarProvider provider) { if (XPBar == provider) XPBar = null; }

        public static void RegisterLevelUpPopup(ILevelUpPopupProvider provider) => LevelUpPopup = provider;
        public static void UnregisterLevelUpPopup(ILevelUpPopupProvider provider) { if (LevelUpPopup == provider) LevelUpPopup = null; }

        public static void RegisterXPGain(IXPGainProvider provider) => XPGain = provider;
        public static void UnregisterXPGain(IXPGainProvider provider) { if (XPGain == provider) XPGain = null; }

        public static void RegisterStatAllocation(IStatAllocationProvider provider) => StatAllocation = provider;
        public static void UnregisterStatAllocation(IStatAllocationProvider provider) { if (StatAllocation == provider) StatAllocation = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            XPBar = null;
            LevelUpPopup = null;
            XPGain = null;
            StatAllocation = null;
        }
    }
}
