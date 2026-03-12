#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Map.Editor.Modules
{
    /// <summary>
    /// EPIC 17.6: Play-mode only — icon buffer count, compass entry count, minimap camera
    /// position/zoom, local player position, discovered POI list. Toggle compass/minimap
    /// visibility. Manual zoom override.
    /// </summary>
    public class LiveInspectorModule : IMapWorkstationModule
    {
        public string ModuleName => "Live Inspector";

        private bool _showIcons = true;
        private bool _showCompass = true;
        private bool _showDiscoveredPOIs = true;
        private float _zoomOverride;
        private bool _useZoomOverride;

        // Cached queries to avoid CreateEntityQuery every OnGUI repaint
        private World _cachedWorld;
        private EntityQuery _configQuery;
        private EntityQuery _managedQuery;
        private EntityQuery _poiQuery;

        private void EnsureQueries(World world, EntityManager em)
        {
            if (_cachedWorld == world) return;
            _cachedWorld = world;
            _configQuery = em.CreateEntityQuery(ComponentType.ReadOnly<MinimapConfig>());
            _managedQuery = em.CreateEntityQuery(ComponentType.ReadOnly<MapManagedState>());
            _poiQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PointOfInterest>());
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Live Map Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect live map data.", MessageType.Info);
                return;
            }

            var world = GetClientWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No client world found.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            EnsureQueries(world, em);

            // MinimapConfig
            if (_configQuery.CalculateEntityCount() > 0)
            {
                var config = _configQuery.GetSingleton<MinimapConfig>();

                EditorGUILayout.LabelField("Minimap Config", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Zoom: {config.Zoom:F1} (range: {config.MinZoom:F0}-{config.MaxZoom:F0})");
                EditorGUILayout.LabelField($"  Rotate With Player: {config.RotateWithPlayer}");
                EditorGUILayout.LabelField($"  Icon Scale: {config.IconScale:F2}");
                EditorGUILayout.LabelField($"  Frame Spread: {config.UpdateFrameSpread}");
                EditorGUILayout.LabelField($"  Max Icon Range: {config.MaxIconRange:F0}m");
                EditorGUILayout.LabelField($"  Compass Range: {config.CompassRange:F0}m");

                // Manual zoom override
                EditorGUILayout.Space(4);
                _useZoomOverride = EditorGUILayout.Toggle("Override Zoom", _useZoomOverride);
                if (_useZoomOverride)
                {
                    _zoomOverride = EditorGUILayout.Slider("Zoom Value", _zoomOverride, config.MinZoom, config.MaxZoom);
                    if (GUILayout.Button("Apply Zoom Override"))
                    {
                        config.Zoom = _zoomOverride;
                        var writeQuery = em.CreateEntityQuery(ComponentType.ReadWrite<MinimapConfig>());
                        writeQuery.SetSingleton(config);
                    }
                }
            }

            EditorGUILayout.Space(8);

            // Managed state (icon buffer, compass buffer)
            if (_managedQuery.CalculateEntityCount() > 0)
            {
                var entities = _managedQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                var managed = em.GetComponentObject<MapManagedState>(entities[0]);
                entities.Dispose();

                EditorGUILayout.LabelField("Buffers", EditorStyles.boldLabel);
                int iconCount = managed.IconBuffer.IsCreated ? managed.IconBuffer.Length : 0;
                int compassCount = managed.CompassBuffer.IsCreated ? managed.CompassBuffer.Length : 0;
                EditorGUILayout.LabelField($"  Icon Buffer: {iconCount} entries");
                EditorGUILayout.LabelField($"  Compass Buffer: {compassCount} entries");

                // Icon breakdown
                _showIcons = EditorGUILayout.Foldout(_showIcons, $"Icon Buffer ({iconCount})", true);
                if (_showIcons && managed.IconBuffer.IsCreated)
                {
                    int maxShow = Mathf.Min(iconCount, 20);
                    for (int i = 0; i < maxShow; i++)
                    {
                        var entry = managed.IconBuffer[i];
                        EditorGUILayout.LabelField($"    [{i}] {entry.IconType} at ({entry.WorldPos2D.x:F1}, {entry.WorldPos2D.y:F1}) prio={entry.Priority}");
                    }
                    if (iconCount > 20)
                        EditorGUILayout.LabelField($"    ... and {iconCount - 20} more");
                }

                // Compass breakdown
                _showCompass = EditorGUILayout.Foldout(_showCompass, $"Compass Buffer ({compassCount})", true);
                if (_showCompass && managed.CompassBuffer.IsCreated)
                {
                    int maxShow = Mathf.Min(compassCount, 20);
                    for (int i = 0; i < maxShow; i++)
                    {
                        var entry = managed.CompassBuffer[i];
                        EditorGUILayout.LabelField($"    [{i}] {entry.IconType} angle={math.degrees(entry.Angle):F0} dist={entry.Distance:F0}m \"{entry.Label}\"");
                    }
                }

                // Camera info
                if (managed.MinimapCamera != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Minimap Camera", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"  Position: {managed.MinimapCamera.transform.position}");
                    EditorGUILayout.LabelField($"  Ortho Size: {managed.MinimapCamera.orthographicSize:F1}");
                }
            }

            EditorGUILayout.Space(8);

            // Discovered POIs
            _showDiscoveredPOIs = EditorGUILayout.Foldout(_showDiscoveredPOIs, "Discovered POIs", true);
            if (_showDiscoveredPOIs)
            {
                if (_poiQuery.CalculateEntityCount() > 0)
                {
                    var pois = _poiQuery.ToComponentDataArray<PointOfInterest>(Unity.Collections.Allocator.Temp);
                    int discovered = 0;
                    for (int i = 0; i < pois.Length; i++)
                    {
                        if (pois[i].DiscoveredByPlayer)
                        {
                            EditorGUILayout.LabelField($"    [{pois[i].POIId}] {pois[i].Label} ({pois[i].Type})");
                            discovered++;
                        }
                    }
                    EditorGUILayout.LabelField($"  {discovered}/{pois.Length} POIs discovered");
                    pois.Dispose();
                }
                else
                {
                    EditorGUILayout.LabelField("  No PointOfInterest entities found.");
                }
            }

            // UI visibility toggles
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("UI Controls", EditorStyles.boldLabel);
            if (GUILayout.Button("Toggle Compass Visibility"))
            {
                if (MapUIRegistry.HasCompass)
                {
                    Debug.Log("[MapWorkstation] Compass toggle requested. Use CompassView.SetVisible() at runtime.");
                }
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private static World GetClientWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Name.Contains("Client") || world.Flags.HasFlag(WorldFlags.GameClient))
                    return world;
            }
            foreach (var world in World.All)
            {
                if (world.Flags.HasFlag(WorldFlags.GameServer))
                    return world;
            }
            return World.DefaultGameObjectInjectionWorld;
        }
    }
}
#endif
