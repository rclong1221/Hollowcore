using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using System.Linq;

namespace DIG.Voxel.Editor.Modules
{
    public class VoxelDebugModule : IVoxelModule
    {
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

        public void Initialize() { }
        public void DrawSceneGUI(SceneView sceneView) { }
        public void OnDestroy() { _cachedChunkQuery = default; _cachedWorld = null; }

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("Voxel World Debugger", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live data.", MessageType.Info);
            }
            
            // Stats update throttling
            if (EditorApplication.timeSinceStartup > _nextUpdate && Application.isPlaying)
            {
                UpdateStats();
                _nextUpdate = (float)EditorApplication.timeSinceStartup + 0.2f; // 5Hz
            }
            
            DrawChunkStats();
            EditorGUILayout.Space(5);
            DrawGizmoSettings();
            EditorGUILayout.Space(5);
            DrawPerformanceStats();
            EditorGUILayout.Space(10);
            DrawActions();
        }
        
        private World GetTargetWorld()
        {
            if (_cachedWorld != null && _cachedWorld.IsCreated)
            {
                return _cachedWorld;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && WorldHasData(world)) return CacheWorld(world);
            
            foreach (var w in World.All)
            {
                if (w.IsCreated && !w.Name.Contains("Server") && WorldHasData(w))
                    return CacheWorld(w);
            }
            
            foreach (var w in World.All)
            {
                if (w.IsCreated && WorldHasData(w))
                    return CacheWorld(w);
            }

            if (World.All.Count > 0) return World.All[0];
            
            return world;
        }

        private World CacheWorld(World w)
        {
            if (_cachedWorld != w)
            {
                _cachedWorld = w;
                _cachedChunkQuery = default;
            }
            return w;
        }

        private bool WorldHasData(World w)
        {
            try {
                using var query = w.EntityManager.CreateEntityQuery(typeof(ChunkPosition));
                return query.CalculateEntityCount() > 0;
            } catch { return false; }
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
            
            if (_cachedChunkQuery == default || _cachedWorld != world)
            {
                _cachedWorld = world;
                 try {
                    _cachedChunkQuery = entityManager.CreateEntityQuery(typeof(ChunkPosition));
                 } catch { return; }
            }

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
            _showChunkStats = EditorGUILayout.BeginFoldoutHeaderGroup(_showChunkStats, "Chunk Statistics");
            if (_showChunkStats)
            {
                EditorGUI.indentLevel++;
                var worldName = GetTargetWorld()?.Name ?? "None";
                EditorGUILayout.LabelField("World:", worldName);
                EditorGUILayout.LabelField("Total Chunks:", _totalChunks.ToString());
                EditorGUILayout.LabelField("With Voxel Data:", _chunksWithData.ToString());
                EditorGUILayout.LabelField("With Mesh:", _chunksWithMesh.ToString());
                EditorGUILayout.LabelField("Pending Remesh:", _chunksNeedingRemesh.ToString());
                EditorGUILayout.LabelField("Voxel Memory:", $"{_memoryBytes / (1024.0 * 1024.0):F2} MB");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawGizmoSettings()
        {
            _showGizmos = EditorGUILayout.BeginFoldoutHeaderGroup(_showGizmos, "Gizmo Settings");
            if (_showGizmos)
            {
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
                    EditorGUILayout.HelpBox("Density gizmos are very slow! Use only for debugging.", MessageType.Warning);
                }
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawPerformanceStats()
        {
            _showPerformance = EditorGUILayout.BeginFoldoutHeaderGroup(_showPerformance, "Performance");
            if (_showPerformance)
            {
                EditorGUI.indentLevel++;
                
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Avg Generation Time:", $"{DIG.Voxel.Debug.VoxelProfiler.GetAverageMs("ChunkGeneration"):F2} ms");
                    EditorGUILayout.LabelField("Avg Meshing Time:", $"{DIG.Voxel.Debug.VoxelProfiler.GetAverageMs("ChunkMeshing"):F2} ms");
                    EditorGUILayout.LabelField("Avg Frame Time:", $"{Time.deltaTime * 1000:F1} ms");
                }
                else
                {
                    EditorGUILayout.LabelField("Active in Play Mode", EditorStyles.centeredGreyMiniLabel);
                }
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawActions()
        {
            EditorGUILayout.LabelField("Chunk Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (GUILayout.Button("Reload All Chunks"))
            {
                var world = GetTargetWorld();
                if (world != null)
                {
                    using var query = world.EntityManager.CreateEntityQuery(typeof(ChunkNeedsRemesh));
                    world.EntityManager.SetComponentEnabled<ChunkNeedsRemesh>(query, true);
                    UnityEngine.Debug.Log($"[Voxel Editor] Reload requested for all chunks");
                }
            }
            
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
            
            EditorGUILayout.EndVertical();
        }
        
        private void SelectChunkAtCamera()
        {
            var cam = SceneView.lastActiveSceneView?.camera ?? Camera.main;
            if (cam == null) return;
            
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
    }
}
