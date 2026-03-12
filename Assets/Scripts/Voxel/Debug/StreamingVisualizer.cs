using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Voxel.Components;
using DIG.Voxel.Core;

namespace DIG.Voxel.Debug
{
    /// <summary>
    /// Visualization tool for the chunk streaming and LOD system.
    /// Draws an overlay with stats and gizmos for chunk states.
    /// </summary>
    public class StreamingVisualizer : MonoBehaviour
    {
        [Header("Display")]
        public bool ShowOverlay = true;
        public KeyCode ToggleKey = KeyCode.F8;
        
        [Header("Appearance")]
        public Color LoadingColor = Color.yellow;
        public Color LoadedColor = Color.green;
        public Color UnloadingColor = Color.red;
        public Color LOD0Color = Color.green;
        public Color LOD1Color = new Color(0.5f, 1f, 0.5f);
        public Color LOD2Color = new Color(0.3f, 0.8f, 0.3f);
        public Color LOD3Color = new Color(0.1f, 0.5f, 0.1f);
        
        [Header("Gizmos")]
        public bool ShowGizmos = true;
        public bool DrawChunkLabels = false;

        private void Update()
        {
            // Use new Input System
            if (Keyboard.current != null && GetKeyDown(ToggleKey))
                ShowOverlay = !ShowOverlay;
        }
        
        private bool GetKeyDown(KeyCode legacyKey)
        {
            // Map legacy KeyCode to new Input System Key
            var key = legacyKey switch
            {
                KeyCode.F1 => Key.F1,
                KeyCode.F2 => Key.F2,
                KeyCode.F3 => Key.F3,
                KeyCode.F4 => Key.F4,
                KeyCode.F5 => Key.F5,
                KeyCode.F6 => Key.F6,
                KeyCode.F7 => Key.F7,
                KeyCode.F8 => Key.F8,
                KeyCode.F9 => Key.F9,
                KeyCode.F10 => Key.F10,
                KeyCode.F11 => Key.F11,
                KeyCode.F12 => Key.F12,
                _ => Key.F8
            };
            return Keyboard.current[key].wasPressedThisFrame;
        }
        
        private void OnGUI()
        {
            if (!ShowOverlay) return;
            
            DrawStats();
        }
        
        private World GetTargetWorld()
        {
            // 1. Try Default
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && WorldHasStats(world)) return world;
            
            // 2. Search all worlds for one with stats (Prioritize Client)
            foreach (var w in World.All)
            {
                if (w.IsCreated && !w.Name.Contains("Server") && WorldHasStats(w))
                    return w;
            }
            
            // 3. Fallback to Server if no client found
            foreach (var w in World.All)
            {
                if (w.IsCreated && WorldHasStats(w))
                    return w;
            }
            
            // 4. Just return default or any non-null to avoid crash
            if (world != null) return world;
            return World.All.Count > 0 ? World.All[0] : null;
        }
        
        private bool WorldHasStats(World world)
        {
            return world.EntityManager.CreateEntityQuery(typeof(VoxelStreamingStats)).HasSingleton<VoxelStreamingStats>();
        }

        private void DrawStats()
        {
            var world = GetTargetWorld();
            if (world == null) return;
            
            var entityManager = world.EntityManager;
            
            // Get Streaming Stats
            int loadedCount = 0;
            int pendingCount = 0;
            int unloadCount = 0;
            float memoryMB = 0;
            
            // Re-check just in case
            if (WorldHasStats(world))
            {
                var stats = entityManager.CreateEntityQuery(typeof(VoxelStreamingStats)).GetSingleton<VoxelStreamingStats>();
                loadedCount = stats.LoadedChunks;
                pendingCount = stats.ChunksToSpawnQueue;
                unloadCount = stats.ChunksToUnloadQueue;
                memoryMB = stats.EstimatedMemoryMB;
            }
            
            // Draw Box
            GUILayout.BeginArea(new Rect(10, 10, 250, 170), GUI.skin.box);
            GUILayout.Label($"Streaming Visualizer ({world.Name})", GUI.skin.box);
            GUILayout.Space(5);
            
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Loaded:", GUILayout.Width(80));
                GUILayout.Label(loadedCount.ToString(), GetStyle(LoadedColor));
            }
            
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Pending:", GUILayout.Width(80));
                GUILayout.Label(pendingCount.ToString(), GetStyle(LoadingColor));
            }
            
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Unloading:", GUILayout.Width(80));
                GUILayout.Label(unloadCount.ToString(), GetStyle(UnloadingColor));
            }
            
            GUILayout.Label($"Memory: {memoryMB:F2} MB (Voxel Data)");
            
            GUILayout.Space(5);
            GUILayout.Label("[F8] Toggle Overlay");
            GUILayout.EndArea();
        }

        private GUIStyle GetStyle(Color c)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = c;
            style.fontStyle = FontStyle.Bold;
            return style;
        }
        
        private void OnDrawGizmos()
        {
            if (!ShowGizmos || !Application.isPlaying) return;

            var world = GetTargetWorld();
            if (world == null) return;
            
            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                typeof(ChunkPosition),
                typeof(ChunkLODState)
            );
            
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var positions = query.ToComponentDataArray<ChunkPosition>(Unity.Collections.Allocator.Temp);
            var lods = query.ToComponentDataArray<ChunkLODState>(Unity.Collections.Allocator.Temp);
            
            for (int i = 0; i < entities.Length; i++)
            {
                int3 chunkPos = positions[i].Value;
                int lod = lods[i].CurrentLOD;
                
                float3 center = CoordinateUtils.ChunkToWorldPos(chunkPos) + new float3(VoxelConstants.CHUNK_SIZE / 2f);
                float size = VoxelConstants.CHUNK_SIZE * 0.95f; // Slight gap
                
                Gizmos.color = GetLODColor(lod);
                Gizmos.DrawWireCube(center, new Vector3(size, size, size));
                
                if (DrawChunkLabels)
                {
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(center, $"LOD{lod}\n({chunkPos.x},{chunkPos.y},{chunkPos.z})");
                    #endif
                }
            }
            
            entities.Dispose();
            positions.Dispose();
            lods.Dispose();
        }
        
        private Color GetLODColor(int lod)
        {
            switch (lod)
            {
                case 0: return LOD0Color;
                case 1: return LOD1Color;
                case 2: return LOD2Color;
                case 3: return LOD3Color;
                default: return Color.gray;
            }
        }
    }
}
