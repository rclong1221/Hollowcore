using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Loads map config SOs from Resources, creates singletons, spawns minimap camera.
    /// Runs once on client startup and self-disables.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MinimapBootstrapSystem : SystemBase
    {
        private EntityQuery _managedStateQuery;

        protected override void OnCreate()
        {
            _managedStateQuery = GetEntityQuery(ComponentType.ReadOnly<MapManagedState>());
        }

        protected override void OnUpdate()
        {
            var configSO = Resources.Load<MinimapConfigSO>("MinimapConfig");
            var themeSO = Resources.Load<MapIconThemeSO>("MapIconTheme");
            var poiRegistry = Resources.Load<POIRegistrySO>("POIRegistry");

            if (configSO == null)
            {
                Debug.LogWarning("[MinimapBootstrap] MinimapConfigSO not found at Resources/MinimapConfig. Map system disabled.");
                Enabled = false;
                return;
            }

            // Create MinimapConfig singleton
            var configEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(configEntity, new MinimapConfig
            {
                Zoom = configSO.DefaultZoom,
                MinZoom = configSO.MinZoom,
                MaxZoom = configSO.MaxZoom,
                ZoomStep = configSO.ZoomStep,
                RotateWithPlayer = configSO.RotateWithPlayer,
                IconScale = configSO.IconScale,
                UpdateFrameSpread = configSO.UpdateFrameSpread,
                RenderTextureSize = configSO.RenderTextureSize,
                MaxIconRange = configSO.MaxIconRange,
                CompassRange = configSO.CompassRange
            });

            // Create MapRevealState singleton
            int fogW = configSO.FogTextureWidth;
            int fogH = configSO.FogTextureHeight;
            var revealEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(revealEntity, new MapRevealState
            {
                FogTextureWidth = fogW,
                FogTextureHeight = fogH,
                RevealRadius = configSO.RevealRadius,
                WorldMinX = configSO.WorldBoundsMin.x,
                WorldMinZ = configSO.WorldBoundsMin.y,
                WorldMaxX = configSO.WorldBoundsMax.x,
                WorldMaxZ = configSO.WorldBoundsMax.y,
                TotalRevealed = 0,
                TotalPixels = fogW * fogH,
                LastRevealX = float.MinValue,
                LastRevealZ = float.MinValue,
                RevealMoveThreshold = configSO.RevealMoveThreshold
            });

            // Create managed state with render textures and buffers
            int rtSize = configSO.RenderTextureSize;
            var minimapRT = new RenderTexture(rtSize, rtSize, 16, RenderTextureFormat.ARGB32);
            minimapRT.name = "MinimapRT";
            minimapRT.filterMode = FilterMode.Bilinear;

            var fogRT = new RenderTexture(fogW, fogH, 0, RenderTextureFormat.R8);
            fogRT.name = "FogOfWarRT";
            fogRT.filterMode = FilterMode.Bilinear;

            // Initialize fog to unexplored (all black)
            var prevRT = RenderTexture.active;
            RenderTexture.active = fogRT;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prevRT;

            // Spawn minimap camera
            var cameraGO = new GameObject("MinimapCamera");
            Object.DontDestroyOnLoad(cameraGO);
            var cam = cameraGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = configSO.DefaultZoom;
            cam.transform.position = new Vector3(0, 200, 0);
            cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            cam.targetTexture = minimapRT;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1);
            cam.depth = -10; // Render before main camera
            cam.cullingMask &= ~(1 << LayerMask.NameToLayer("UI")); // Exclude UI layer
            cam.enabled = true;

            // Load fog reveal material
            var fogMat = Resources.Load<Material>("MapFogRevealMat");

            var managedEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(managedEntity, new MapManagedState
            {
                MinimapRenderTexture = minimapRT,
                FogOfWarTexture = fogRT,
                MinimapCamera = cam,
                FogRevealMaterial = fogMat,
                IconBuffer = new NativeList<MapIconEntry>(128, Allocator.Persistent),
                CompassBuffer = new NativeList<CompassEntry>(32, Allocator.Persistent),
                IsInitialized = true,
                LastSavedRevealCount = 0
            });

            // Store theme + POI registry as managed singleton
            var dataEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(dataEntity, new MapDatabaseManaged
            {
                Theme = themeSO,
                POIRegistry = poiRegistry,
                Config = configSO
            });

            int trackCount = themeSO != null ? themeSO.Entries.Length : 0;
            int poiCount = poiRegistry != null ? poiRegistry.POIs.Length : 0;
            Debug.Log($"[MinimapBootstrap] Map system initialized. RT={rtSize}x{rtSize}, Fog={fogW}x{fogH}, Theme entries={trackCount}, POIs={poiCount}");

            Enabled = false;
        }

        protected override void OnDestroy()
        {
            // Use cached query — SystemAPI.Query may fail during world teardown
            if (_managedStateQuery.CalculateEntityCount() == 0) return;

            var entities = _managedStateQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var managed = EntityManager.GetComponentObject<MapManagedState>(entities[i]);
                if (managed == null) continue;
                if (managed.IconBuffer.IsCreated) managed.IconBuffer.Dispose();
                if (managed.CompassBuffer.IsCreated) managed.CompassBuffer.Dispose();
                if (managed.MinimapRenderTexture != null) managed.MinimapRenderTexture.Release();
                if (managed.FogOfWarTexture != null) managed.FogOfWarTexture.Release();
                if (managed.MinimapCamera != null) Object.Destroy(managed.MinimapCamera.gameObject);
            }
            entities.Dispose();
        }
    }

    /// <summary>
    /// Managed component holding references to SO databases for map systems.
    /// </summary>
    public class MapDatabaseManaged : IComponentData
    {
        public MapIconThemeSO Theme;
        public POIRegistrySO POIRegistry;
        public MinimapConfigSO Config;
    }
}
