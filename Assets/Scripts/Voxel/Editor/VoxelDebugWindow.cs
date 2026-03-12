using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using System.Linq;

namespace DIG.Voxel.Editor
{
    public class VoxelDebugWindow : EditorWindow
    {
        [MenuItem("DIG/Voxel/Debug Window")]
        public static void ShowWindow()
        {
            GetWindow<VoxelDebugWindow>("Voxel Debug");
        }
        
        private Vector2 _scrollPos;
        private bool _showChunkStats = true;
        private bool _showGizmos = true;
        private bool _showPerformance = true;
        
        // Gizmo settings
        public static bool DrawChunkBounds = true;
        public static bool DrawDensityGizmos = false;
        public static bool DrawMaterialColors = false;
        public static int GizmoChunkRadius = 2;
        
        // Runtime stats
        private int _totalChunks;
        private int _chunksWithData;
        private int _chunksWithMesh;
        private int _chunksNeedingRemesh;
        private long _memoryBytes;
        private float _nextUpdate;

        // Cache to prevent leaks
        private World _cachedWorld;
        private EntityQuery _cachedChunkQuery;
        
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            DrawHeader();
            
            // Stats update throttling
            if (EditorApplication.timeSinceStartup > _nextUpdate && Application.isPlaying)
            {
                UpdateStats();
                _nextUpdate = (float)EditorApplication.timeSinceStartup + 0.2f; // 5Hz
            }
            
            DrawChunkStats();
            DrawGizmoSettings();
            DrawPerformanceStats();
            DrawActions();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Voxel World Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live data.", MessageType.Info);
            }
        }
        
        private World GetTargetWorld()
        {
            // If we have a cached world and it's still valid, check if we should stick with it
            if (_cachedWorld != null && _cachedWorld.IsCreated)
            {
                // Re-verify it has data occasionally? Or just stick with it?
                // Let's stick with it unless it loses data or we requested a change
                return _cachedWorld;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && WorldHasData(world)) return CacheWorld(world);
            
            // Prioritize Client
            foreach (var w in World.All)
            {
                if (w.IsCreated && !w.Name.Contains("Server") && WorldHasData(w))
                    return CacheWorld(w);
            }
            
            // Then Server
            foreach (var w in World.All)
            {
                if (w.IsCreated && WorldHasData(w))
                    return CacheWorld(w);
            }

             // If nothing has data, just return any created world to avoid null refs
            if (World.All.Count > 0) return World.All[0]; // Don't cache default/empty worlds
            
            return world;
        }

        private World CacheWorld(World w)
        {
            if (_cachedWorld != w)
            {
                _cachedWorld = w;
                _cachedChunkQuery = default; // Clear query when world changes
            }
            return w;
        }

        private bool WorldHasData(World w)
        {
            // Use GetSingleton/HasSingleton logic or carefully manage query
            // But we don't have a singleton for ChunkPosition (it's per chunk).
            // We must creating a query, but we should dispose it immediately.
            // The original code was LEAKING this query (no using, no Dispose).
            using var query = w.EntityManager.CreateEntityQuery(typeof(ChunkPosition));
            return query.CalculateEntityCount() > 0;
        }
        
        private void UpdateStats()
        {
            var world = GetTargetWorld();
            if (world == null) return;
            
            var entityManager = world.EntityManager;
            
            _totalChunks = 0;
            _chunksWithData = 0;
            _chunksWithMesh = 0;
            _chunksNeedingRemesh = 0;
            
            // Re-create query if needed (world might have changed or query not created yet)
            if (_cachedChunkQuery == default || _cachedWorld != world)
            {
                _cachedWorld = world;
                // Only create if world is valid
                 try {
                    _cachedChunkQuery = entityManager.CreateEntityQuery(typeof(ChunkPosition));
                 } catch { return; }
            }

            // Verify query validity (world might have been disposed)
            if (!_cachedWorld.IsCreated) 
            {
                _cachedWorld = null;
                _cachedChunkQuery = default;
                return;
            }

            using var entities = _cachedChunkQuery.ToEntityArray(Allocator.Temp);
            
            _totalChunks = entities.Length;
            
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<ChunkVoxelData>(entity))
                {
                    var data = entityManager.GetComponentData<ChunkVoxelData>(entity);
                    if (data.IsValid) _chunksWithData++;
                }
                
                if (entityManager.HasComponent<ChunkMeshState>(entity))
                {
                    var state = entityManager.GetComponentData<ChunkMeshState>(entity);
                    if (state.HasMesh) _chunksWithMesh++;
                }
                
                if (entityManager.IsComponentEnabled<ChunkNeedsRemesh>(entity))
                {
                    _chunksNeedingRemesh++;
                }
            }
            
            _memoryBytes = (long)_chunksWithData * VoxelConstants.VOXELS_PER_CHUNK * 2;
        }
        
        private void DrawChunkStats()
        {
            _showChunkStats = EditorGUILayout.Foldout(_showChunkStats, "Chunk Statistics");
            if (!_showChunkStats) return;

             var worldName = GetTargetWorld()?.Name ?? "None";
            
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("World:", worldName);
            EditorGUILayout.LabelField("Total Chunks:", _totalChunks.ToString());
            EditorGUILayout.LabelField("With Voxel Data:", _chunksWithData.ToString());
            EditorGUILayout.LabelField("With Mesh:", _chunksWithMesh.ToString());
            EditorGUILayout.LabelField("Pending Remesh:", _chunksNeedingRemesh.ToString());
            
            EditorGUILayout.LabelField("Voxel Memory:", $"{_memoryBytes / (1024.0 * 1024.0):F2} MB");
            
            EditorGUI.indentLevel--;
        }
        
        private void DrawGizmoSettings()
        {
            _showGizmos = EditorGUILayout.Foldout(_showGizmos, "Gizmo Settings");
            if (!_showGizmos) return;
            
            EditorGUI.indentLevel++;
            
            bool newDrawChunkBounds = EditorGUILayout.Toggle("Draw Chunk Bounds", DrawChunkBounds);
            if (newDrawChunkBounds != DrawChunkBounds)
            {
                DrawChunkBounds = newDrawChunkBounds;
                SceneView.RepaintAll();
            }
            
            DrawDensityGizmos = EditorGUILayout.Toggle("Draw Density (slow!)", DrawDensityGizmos);
            DrawMaterialColors = EditorGUILayout.Toggle("Draw Material Colors", DrawMaterialColors);
            GizmoChunkRadius = EditorGUILayout.IntSlider("Gizmo Radius", GizmoChunkRadius, 1, 5);
            
            if (DrawDensityGizmos)
            {
                EditorGUILayout.HelpBox("Density gizmos are very slow! Use only for debugging specific issues.", MessageType.Warning);
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void DrawPerformanceStats()
        {
            _showPerformance = EditorGUILayout.Foldout(_showPerformance, "Performance");
            if (!_showPerformance) return;
            
            EditorGUI.indentLevel++;
            
            // These would be populated by the actual systems
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Avg Generation Time:", $"{DIG.Voxel.Debug.VoxelProfiler.GetAverageMs("ChunkGeneration"):F2} ms");
                EditorGUILayout.LabelField("Avg Meshing Time:", $"{DIG.Voxel.Debug.VoxelProfiler.GetAverageMs("ChunkMeshing"):F2} ms");
                EditorGUILayout.LabelField("Avg Frame Time:", $"{Time.deltaTime * 1000:F1} ms");
            }
            else
            {
                EditorGUILayout.LabelField("Avg Time:", "-- ms");
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void DrawActions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Reload All Chunks"))
            {
                var world = GetTargetWorld();
                if (world != null)
                {
                    using var query = world.EntityManager.CreateEntityQuery(typeof(ChunkNeedsRemesh));
                    using var entities = query.ToEntityArray(Allocator.Temp);
                    world.EntityManager.SetComponentEnabled<ChunkNeedsRemesh>(query, true);
                    UnityEngine.Debug.Log($"[Voxel Editor] Reload requested for {entities.Length} chunks");
                }
            }
            
            /*
            if (GUILayout.Button("Clear All Chunks"))
            {
               // This is dangerous without resetting systems
               UnityEngine.Debug.LogWarning("[Voxel Editor] Clear not fully implemented safely");
            }
            */
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Chunk at Camera"))
            {
                SelectChunkAtCamera();
            }
            if (GUILayout.Button("Log Chunk Data"))
            {
                LogChunkAtCamera();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void SelectChunkAtCamera()
        {
            var cam = SceneView.lastActiveSceneView?.camera ?? Camera.main;
            if (cam == null) return;
            
            // Find chunk GameObject at camera position
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Selection.activeGameObject = hit.collider.gameObject;
            }
        }
        
        private void LogChunkAtCamera()
        {
            var cam = SceneView.lastActiveSceneView?.camera ?? Camera.main;
            if (cam == null) return;
            
            var chunkPos = CoordinateUtils.WorldToChunkPos(cam.transform.position);
            UnityEngine.Debug.Log($"[Voxel Editor] Camera at chunk: {chunkPos}");
        }
        
        private double _lastRepaint;

        private void OnInspectorUpdate()
        {
            // Throttled repaint at 4Hz max instead of every inspector update
            if (Application.isPlaying && _showChunkStats && EditorApplication.timeSinceStartup - _lastRepaint > 0.25)
            {
                _lastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }
    }
}
