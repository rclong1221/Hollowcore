using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Static singleton registry for achievement UI providers.
    /// Follows CombatUIRegistry / ProgressionUIRegistry pattern.
    /// MonoBehaviours register in OnEnable, unregister in OnDisable.
    /// </summary>
    public static class AchievementUIRegistry
    {
        private static IAchievementUIProvider _provider;

        public static bool HasProvider => _provider != null;

        public static void Register(IAchievementUIProvider provider) => _provider = provider;

        public static void Unregister(IAchievementUIProvider provider)
        {
            if (_provider == provider) _provider = null;
        }

        public static void ShowToast(AchievementToastData data) => _provider?.ShowToast(data);
        public static void UpdatePanel(AchievementPanelData data) => _provider?.UpdatePanel(data);
        public static void UpdateProgress(ushort achievementId, int current, int threshold) => _provider?.UpdateProgress(achievementId, current, threshold);
        public static void HideToast() => _provider?.HideToast();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _provider = null;
        }
    }
}
