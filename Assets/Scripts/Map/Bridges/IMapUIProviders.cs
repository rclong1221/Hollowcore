using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Provider interface for minimap UI rendering.
    /// Implement on a MonoBehaviour (MinimapView) and register via MapUIRegistry.
    /// </summary>
    public interface IMinimapProvider
    {
        void UpdateIcons(NativeList<MapIconEntry> icons, float3 playerPos, float playerYaw, float zoom);
        void UpdateFogProgress(int revealed, int total);
        void SetRenderTexture(RenderTexture minimapRT, RenderTexture fogRT);
        void SetZoom(float zoom);
    }

    /// <summary>
    /// EPIC 17.6: Provider interface for fullscreen world map UI.
    /// Implement on a MonoBehaviour (WorldMapView) and register via MapUIRegistry.
    /// </summary>
    public interface IWorldMapProvider
    {
        void UpdatePlayerMarker(float3 worldPos, float yaw);
        void SetFogTexture(RenderTexture fogRT);
        void ShowWorldMap(bool show);
        void SetZoneLabels(POIDefinition[] pois);
        void HighlightFastTravel(int poiId);
    }

    /// <summary>
    /// EPIC 17.6: Provider interface for compass bar UI at screen top.
    /// Implement on a MonoBehaviour (CompassView) and register via MapUIRegistry.
    /// </summary>
    public interface ICompassProvider
    {
        void UpdateEntries(NativeList<CompassEntry> entries);
        void SetVisible(bool visible);
    }

    /// <summary>
    /// EPIC 17.6: Provider interface for map-related toast notifications.
    /// Implement on a MonoBehaviour (MapNotificationView) and register via MapUIRegistry.
    /// </summary>
    public interface IMapNotificationProvider
    {
        void OnPOIDiscovered(string label, POIType type);
        void OnZoneEntered(string zoneName);
    }
}
