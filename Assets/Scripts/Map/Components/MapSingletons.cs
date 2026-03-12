using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Minimap configuration singleton — created by MinimapBootstrapSystem.
    /// </summary>
    public struct MinimapConfig : IComponentData
    {
        public float Zoom;                 // Current orthographic camera size
        public float MinZoom;
        public float MaxZoom;
        public float ZoomStep;
        public bool RotateWithPlayer;      // true = rotate minimap, false = north-up
        public float IconScale;            // Base scale multiplier for map icons
        public int UpdateFrameSpread;      // Frame-spread K for MapIconUpdateSystem
        public int RenderTextureSize;      // Minimap RT resolution (256/512/1024)
        public float MaxIconRange;         // Max world-space distance for icon visibility
        public float CompassRange;         // Max distance for compass POI display
    }

    /// <summary>
    /// EPIC 17.6: Fog-of-war state singleton — created by MinimapBootstrapSystem.
    /// </summary>
    public struct MapRevealState : IComponentData
    {
        public int FogTextureWidth;
        public int FogTextureHeight;
        public float RevealRadius;         // World-space radius revealed per frame
        public float WorldMinX;            // World bounds (fog UV 0,0)
        public float WorldMinZ;
        public float WorldMaxX;            // World bounds (fog UV 1,1)
        public float WorldMaxZ;
        public int TotalRevealed;          // Total fog pixels revealed (for stats)
        public int TotalPixels;
        public float LastRevealX;          // Last player X that triggered reveal
        public float LastRevealZ;
        public float RevealMoveThreshold;  // Min movement before new reveal circle
    }

    /// <summary>
    /// EPIC 17.6: Managed singleton for render texture references and buffers.
    /// Cannot store RenderTexture/Camera in unmanaged IComponentData.
    /// </summary>
    public class MapManagedState : IComponentData
    {
        public RenderTexture MinimapRenderTexture;
        public RenderTexture FogOfWarTexture;
        public Camera MinimapCamera;
        public Material FogRevealMaterial;
        public NativeList<MapIconEntry> IconBuffer;
        public NativeList<CompassEntry> CompassBuffer;
        public bool IsInitialized;
        public int LastSavedRevealCount;     // For dirty tracking in save module
    }
}
