using UnityEngine;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Provider interface for game-specific zone HUD.
    /// Games implement this to display zone name, enemies, clear progress, etc.
    /// </summary>
    public interface IZoneUIProvider
    {
        void OnZoneActivated(int zoneIndex, string zoneName, ZoneType type, ZoneClearMode clearMode);
        void OnZoneCleared(int zoneIndex, float timeInZone, int enemiesKilled);
        void UpdateZoneHUD(float timeInZone, int enemiesAlive, int enemiesKilled,
            int enemiesSpawned, float spawnBudget, bool exitActivated, bool isCleared);
    }

    /// <summary>
    /// Central registry for zone UI providers.
    /// Follows RunUIRegistry pattern: register/unregister with replacement warning.
    /// </summary>
    public static class ZoneUIRegistry
    {
        private static IZoneUIProvider _provider;

        public static IZoneUIProvider Provider => _provider;
        public static bool HasProvider => _provider != null;

        public static void Register(IZoneUIProvider provider)
        {
            if (_provider != null && provider != null)
                Debug.LogWarning("[ZoneUIRegistry] Replacing existing zone UI provider.");
            _provider = provider;
        }

        public static void Unregister(IZoneUIProvider provider)
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
