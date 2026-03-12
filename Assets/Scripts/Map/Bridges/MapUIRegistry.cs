using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Central static registry for map UI providers.
    /// Same pattern as CombatUIRegistry — ECS systems write through this registry,
    /// MonoBehaviour UI views register/unregister as providers.
    /// </summary>
    public static class MapUIRegistry
    {
        private static IMinimapProvider _minimapProvider;
        private static IWorldMapProvider _worldMapProvider;
        private static ICompassProvider _compassProvider;
        private static IMapNotificationProvider _notificationProvider;

        // ==================== PROPERTIES ====================

        public static bool HasMinimap => _minimapProvider != null;
        public static bool HasWorldMap => _worldMapProvider != null;
        public static bool HasCompass => _compassProvider != null;
        public static bool HasNotification => _notificationProvider != null;

        // ==================== REGISTRATION ====================

        public static void RegisterMinimap(IMinimapProvider provider)
        {
            if (_minimapProvider != null && provider != null)
                Debug.LogWarning("[MapUIRegistry] Replacing existing minimap provider.");
            _minimapProvider = provider;
        }

        public static void RegisterWorldMap(IWorldMapProvider provider)
        {
            if (_worldMapProvider != null && provider != null)
                Debug.LogWarning("[MapUIRegistry] Replacing existing world map provider.");
            _worldMapProvider = provider;
        }

        public static void RegisterCompass(ICompassProvider provider)
        {
            if (_compassProvider != null && provider != null)
                Debug.LogWarning("[MapUIRegistry] Replacing existing compass provider.");
            _compassProvider = provider;
        }

        public static void RegisterNotification(IMapNotificationProvider provider)
        {
            if (_notificationProvider != null && provider != null)
                Debug.LogWarning("[MapUIRegistry] Replacing existing map notification provider.");
            _notificationProvider = provider;
        }

        // ==================== UNREGISTRATION ====================

        public static void UnregisterMinimap(IMinimapProvider provider)
        {
            if (_minimapProvider == provider) _minimapProvider = null;
        }

        public static void UnregisterWorldMap(IWorldMapProvider provider)
        {
            if (_worldMapProvider == provider) _worldMapProvider = null;
        }

        public static void UnregisterCompass(ICompassProvider provider)
        {
            if (_compassProvider == provider) _compassProvider = null;
        }

        public static void UnregisterNotification(IMapNotificationProvider provider)
        {
            if (_notificationProvider == provider) _notificationProvider = null;
        }

        public static void UnregisterAll()
        {
            _minimapProvider = null;
            _worldMapProvider = null;
            _compassProvider = null;
            _notificationProvider = null;
        }

        // ==================== DISPATCH (called by MapUIBridgeSystem) ====================

        public static void SetMinimapRenderTextures(RenderTexture minimapRT, RenderTexture fogRT)
            => _minimapProvider?.SetRenderTexture(minimapRT, fogRT);

        public static void SetWorldMapFogTexture(RenderTexture fogRT)
            => _worldMapProvider?.SetFogTexture(fogRT);

        public static void UpdateMinimapIcons(NativeList<MapIconEntry> icons, float3 playerPos, float playerYaw, float zoom)
            => _minimapProvider?.UpdateIcons(icons, playerPos, playerYaw, zoom);

        public static void UpdateCompass(NativeList<CompassEntry> entries)
            => _compassProvider?.UpdateEntries(entries);

        public static void UpdateFogStats(int revealed, int total)
            => _minimapProvider?.UpdateFogProgress(revealed, total);

        public static void UpdatePlayerMarker(float3 pos, float yaw)
            => _worldMapProvider?.UpdatePlayerMarker(pos, yaw);

        public static void NotifyPOIDiscovered(string label, POIType type)
            => _notificationProvider?.OnPOIDiscovered(label, type);

        public static void NotifyZoneEntered(string zoneName)
            => _notificationProvider?.OnZoneEntered(zoneName);
    }
}
