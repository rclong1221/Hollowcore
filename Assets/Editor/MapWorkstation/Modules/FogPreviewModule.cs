#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Entities;

namespace DIG.Map.Editor.Modules
{
    /// <summary>
    /// EPIC 17.6: Play-mode only — live fog texture display, reveal stats,
    /// "Reveal All" and "Reset Fog" debug buttons.
    /// </summary>
    public class FogPreviewModule : IMapWorkstationModule
    {
        public string ModuleName => "Fog Preview";

        private Texture2D _previewTexture;
        // Cached queries to avoid CreateEntityQuery every OnGUI repaint
        private World _cachedWorld;
        private EntityQuery _revealQuery;
        private EntityQuery _managedQuery;

        private void EnsureQueries(World world, EntityManager em)
        {
            if (_cachedWorld == world) return;
            _cachedWorld = world;
            _revealQuery = em.CreateEntityQuery(ComponentType.ReadOnly<MapRevealState>());
            _managedQuery = em.CreateEntityQuery(ComponentType.ReadOnly<MapManagedState>());
        }

        private MapManagedState GetManagedState(EntityManager em)
        {
            if (_managedQuery.CalculateEntityCount() == 0) return null;
            var entities = _managedQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var result = em.GetComponentObject<MapManagedState>(entities[0]);
            entities.Dispose();
            return result;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Fog of War Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view live fog data.", MessageType.Info);
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

            if (_revealQuery.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("MapRevealState singleton not found. Is MinimapBootstrapSystem running?", MessageType.Warning);
                return;
            }

            var reveal = _revealQuery.GetSingleton<MapRevealState>();

            // Stats
            float pct = reveal.TotalPixels > 0 ? (float)reveal.TotalRevealed / reveal.TotalPixels * 100f : 0f;
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Fog Resolution: {reveal.FogTextureWidth} x {reveal.FogTextureHeight}");
            EditorGUILayout.LabelField($"Total Pixels: {reveal.TotalPixels:N0}");
            EditorGUILayout.LabelField($"Revealed Pixels: {reveal.TotalRevealed:N0}");

            // Progress bar
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, pct / 100f, $"Explored: {pct:F1}%");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Reveal Radius: {reveal.RevealRadius:F1}m");
            EditorGUILayout.LabelField($"Move Threshold: {reveal.RevealMoveThreshold:F1}m");
            EditorGUILayout.LabelField($"Last Reveal Pos: ({reveal.LastRevealX:F1}, {reveal.LastRevealZ:F1})");
            EditorGUILayout.LabelField($"World Bounds: ({reveal.WorldMinX:F0},{reveal.WorldMinZ:F0}) to ({reveal.WorldMaxX:F0},{reveal.WorldMaxZ:F0})");

            EditorGUILayout.Space(12);

            // Fog texture preview
            var managed = GetManagedState(em);
            if (managed != null && managed.FogOfWarTexture != null)
            {
                EditorGUILayout.LabelField("Fog Texture Preview", EditorStyles.boldLabel);
                var previewRect = EditorGUILayout.GetControlRect(false, 256);
                EditorGUI.DrawPreviewTexture(previewRect, managed.FogOfWarTexture, null, ScaleMode.ScaleToFit);
            }

            EditorGUILayout.Space(12);

            // Debug buttons
            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reveal All"))
            {
                reveal.TotalRevealed = reveal.TotalPixels;
                var writeQuery = em.CreateEntityQuery(ComponentType.ReadWrite<MapRevealState>());
                writeQuery.SetSingleton(reveal);

                var ms = GetManagedState(em);
                if (ms?.FogOfWarTexture != null)
                {
                    var tmp = new Texture2D(ms.FogOfWarTexture.width, ms.FogOfWarTexture.height, TextureFormat.R8, false);
                    var pixels = tmp.GetRawTextureData();
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = 255;
                    tmp.LoadRawTextureData(pixels);
                    tmp.Apply();
                    Graphics.Blit(tmp, ms.FogOfWarTexture);
                    Object.DestroyImmediate(tmp);
                }
            }

            if (GUILayout.Button("Reset Fog"))
            {
                reveal.TotalRevealed = 0;
                reveal.LastRevealX = float.MaxValue;
                reveal.LastRevealZ = float.MaxValue;
                var writeQuery = em.CreateEntityQuery(ComponentType.ReadWrite<MapRevealState>());
                writeQuery.SetSingleton(reveal);

                var ms = GetManagedState(em);
                if (ms?.FogOfWarTexture != null)
                {
                    var tmp = new Texture2D(ms.FogOfWarTexture.width, ms.FogOfWarTexture.height, TextureFormat.R8, false);
                    var pixels = tmp.GetRawTextureData();
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = 0;
                    tmp.LoadRawTextureData(pixels);
                    tmp.Apply();
                    Graphics.Blit(tmp, ms.FogOfWarTexture);
                    Object.DestroyImmediate(tmp);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private static World GetClientWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Name.Contains("Client") || world.Flags.HasFlag(WorldFlags.GameClient))
                    return world;
            }
            // Fallback: local simulation (listen server)
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
