using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Collections;
using DIG.Voxel.Core;
using DIG.Voxel.Rendering;
using DIG.Voxel.Components;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Editor window for visualizing and debugging the LOD system.
    /// Shows LOD rings around camera, chunk statistics, and performance metrics.
    /// </summary>
    public class LODVisualizerWindow : EditorWindow
    {
        private VoxelLODConfig _config;
        private bool _showGizmos = true;
        private bool _showStats = true;
        private Vector2 _scrollPos;

        // Runtime stats (updated in play mode)
        private int[] _chunksPerLOD = new int[8];
        private int _totalChunks;
        private EntityQuery _lodQuery;
        private int _chunksWithColliders;

        [MenuItem("DIG/Voxel/LOD Visualizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<LODVisualizerWindow>("LOD Visualizer");
            window.minSize = new Vector2(300, 400);
        }

        private void OnEnable()
        {
            // Try to load config
            _config = Resources.Load<VoxelLODConfig>("VoxelLODConfig");
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("LOD Visualizer", EditorStyles.boldLabel);

            // Config reference
            _config = (VoxelLODConfig)EditorGUILayout.ObjectField(
                "LOD Config", _config, typeof(VoxelLODConfig), false);

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Assign a VoxelLODConfig to visualize LOD rings.", MessageType.Warning);
                
                if (GUILayout.Button("Create Default Config"))
                {
                    CreateDefaultConfig();
                }
                return;
            }

            EditorGUILayout.Space(10);

            // Visualization toggles
            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);
            _showGizmos = EditorGUILayout.Toggle("Show LOD Rings in Scene", _showGizmos);
            _showStats = EditorGUILayout.Toggle("Show Statistics", _showStats);

            EditorGUILayout.Space(10);

            // LOD Level Overview
            EditorGUILayout.LabelField("LOD Levels", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Distance", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Step", EditorStyles.boldLabel, GUILayout.Width(40));
            EditorGUILayout.LabelField("Collider", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Tri Reduction", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _config.Levels.Length; i++)
            {
                var level = _config.Levels[i];
                
                EditorGUILayout.BeginHorizontal();
                
                // Color indicator
                var colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
                EditorGUI.DrawRect(colorRect, level.DebugColor);
                
                EditorGUILayout.LabelField($"LOD{i}", GUILayout.Width(30));
                EditorGUILayout.LabelField($"{level.Distance}m", GUILayout.Width(70));
                EditorGUILayout.LabelField($"{level.VoxelStep}", GUILayout.Width(40));
                EditorGUILayout.LabelField(level.HasCollider ? "✓" : "✗", GUILayout.Width(60));
                
                // Triangle reduction estimate
                float reduction = 1f / (level.VoxelStep * level.VoxelStep);
                EditorGUILayout.LabelField($"~{reduction * 100:F0}%", GUILayout.Width(100));
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Runtime Stats (Play Mode only)
            if (Application.isPlaying && _showStats)
            {
                EditorGUILayout.LabelField("Runtime Statistics", EditorStyles.boldLabel);
                
                UpdateRuntimeStats();
                
                EditorGUILayout.LabelField($"Total Chunks: {_totalChunks}");
                EditorGUILayout.LabelField($"With Colliders: {_chunksWithColliders}");
                
                for (int i = 0; i < Mathf.Min(_config.Levels.Length, _chunksPerLOD.Length); i++)
                {
                    EditorGUILayout.LabelField($"  LOD{i}: {_chunksPerLOD[i]} chunks");
                }
            }
            else if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see runtime statistics.", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            // Performance estimation
            EditorGUILayout.LabelField("Performance Estimation", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Estimate Triangle Count"))
            {
                EstimateTriangles();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_showGizmos || _config == null) return;

            // Draw LOD rings around camera
            Vector3 cameraPos = sceneView.camera.transform.position;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < _config.Levels.Length; i++)
            {
                var level = _config.Levels[i];
                
                Handles.color = level.DebugColor;
                Handles.DrawWireDisc(cameraPos, Vector3.up, level.Distance, 2f);
                
                // Label
                Vector3 labelPos = cameraPos + Vector3.forward * level.Distance;
                Handles.Label(labelPos, $"LOD{i} ({level.Distance}m)");
            }
        }

        private void UpdateRuntimeStats()
        {
            if (!Application.isPlaying) return;

            // Reset counters
            _totalChunks = 0;
            _chunksWithColliders = 0;
            for (int i = 0; i < _chunksPerLOD.Length; i++)
                _chunksPerLOD[i] = 0;

            // Query ECS world
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var entityManager = world.EntityManager;
            
            // Create or reuse cached query - check validity FIRST
            if (_lodQuery == default || !entityManager.IsQueryValid(_lodQuery))
            {
                _lodQuery = entityManager.CreateEntityQuery(
                    typeof(ChunkLODState),
                    typeof(ChunkPosition)
                );
            }

            using var entities = _lodQuery.ToEntityArray(Allocator.Temp);
            
            foreach (var entity in entities)
            {
                _totalChunks++;
                
                var lodState = entityManager.GetComponentData<ChunkLODState>(entity);
                int lod = Mathf.Clamp(lodState.CurrentLOD, 0, _chunksPerLOD.Length - 1);
                _chunksPerLOD[lod]++;

                if (entityManager.HasComponent<ChunkColliderState>(entity))
                {
                    var colliderState = entityManager.GetComponentData<ChunkColliderState>(entity);
                    if (colliderState.IsActive)
                        _chunksWithColliders++;
                }
            }
        }

        private void EstimateTriangles()
        {
            if (_config == null) return;

            // Assume 1000 base triangles per chunk at LOD0
            const int BASE_TRIANGLES = 1000;
            
            int totalTriangles = 0;
            string breakdown = "Estimated Triangle Count:\n";

            // Estimate based on view distance
            for (int i = 0; i < _config.Levels.Length; i++)
            {
                var level = _config.Levels[i];
                float prevDist = i > 0 ? _config.Levels[i - 1].Distance : 0;
                float area = Mathf.PI * (level.Distance * level.Distance - prevDist * prevDist);
                const float CHUNK_SIZE = 32f; // VoxelConstants.CHUNK_SIZE
                int chunksInRing = Mathf.RoundToInt(area / (CHUNK_SIZE * CHUNK_SIZE));
                
                int trisPerChunk = BASE_TRIANGLES / (level.VoxelStep * level.VoxelStep);
                int ringTriangles = chunksInRing * trisPerChunk;
                totalTriangles += ringTriangles;
                
                breakdown += $"  LOD{i}: ~{chunksInRing} chunks × {trisPerChunk} tris = {ringTriangles}\n";
            }

            breakdown += $"\nTotal: ~{totalTriangles} triangles";
            
            UnityEngine.Debug.Log(breakdown);
            EditorUtility.DisplayDialog("Triangle Estimation", breakdown, "OK");
        }

        private void CreateDefaultConfig()
        {
            var config = ScriptableObject.CreateInstance<VoxelLODConfig>();
            
            string path = "Assets/Resources/VoxelLODConfig.asset";
            
            // Ensure Resources folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            _config = config;
            
            UnityEngine.Debug.Log($"[LODVisualizer] Created VoxelLODConfig at {path}");
        }

        private double _lastRepaint;

        private void OnInspectorUpdate()
        {
            // Throttled repaint at 2Hz max instead of every inspector update
            if (Application.isPlaying && EditorApplication.timeSinceStartup - _lastRepaint > 0.5)
            {
                _lastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }
    }

    /// <summary>
    /// Custom editor for VoxelLODConfig with visual LOD level editing.
    /// </summary>
    [CustomEditor(typeof(VoxelLODConfig))]
    public class VoxelLODConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (VoxelLODConfig)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("LOD Configuration", EditorStyles.boldLabel);
            
            // Open visualizer button
            if (GUILayout.Button("Open LOD Visualizer"))
            {
                LODVisualizerWindow.ShowWindow();
            }

            EditorGUILayout.Space(10);

            // Draw default inspector
            base.OnInspectorGUI();

            EditorGUILayout.Space(10);

            // Quick presets
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Close Range"))
            {
                ApplyPreset(config, "close");
            }
            if (GUILayout.Button("Medium Range"))
            {
                ApplyPreset(config, "medium");
            }
            if (GUILayout.Button("Far Range"))
            {
                ApplyPreset(config, "far");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyPreset(VoxelLODConfig config, string preset)
        {
            Undo.RecordObject(config, "Apply LOD Preset");

            switch (preset)
            {
                case "close":
                    config.Levels = new VoxelLODConfig.LODLevel[]
                    {
                        new VoxelLODConfig.LODLevel { Distance = 24f,  VoxelStep = 1, HasCollider = true,  DebugColor = new Color(0, 1, 0, 0.3f) },
                        new VoxelLODConfig.LODLevel { Distance = 48f,  VoxelStep = 2, HasCollider = true,  DebugColor = new Color(1, 1, 0, 0.3f) },
                        new VoxelLODConfig.LODLevel { Distance = 80f,  VoxelStep = 4, HasCollider = false, DebugColor = new Color(1, 0.5f, 0, 0.3f) },
                    };
                    break;
                case "medium":
                    config.Levels = new VoxelLODConfig.LODLevel[]
                    {
                        new VoxelLODConfig.LODLevel { Distance = 32f,  VoxelStep = 1, HasCollider = true,  DebugColor = new Color(0, 1, 0, 0.3f) },
                        new VoxelLODConfig.LODLevel { Distance = 64f,  VoxelStep = 2, HasCollider = true,  DebugColor = new Color(1, 1, 0, 0.3f) },
                        new VoxelLODConfig.LODLevel { Distance = 128f, VoxelStep = 4, HasCollider = false, DebugColor = new Color(1, 0.5f, 0, 0.3f) },
                        new VoxelLODConfig.LODLevel { Distance = 256f, VoxelStep = 8, HasCollider = false, DebugColor = new Color(1, 0, 0, 0.3f) },
                    };
                    break;
                case "far":
                    config.Levels = new VoxelLODConfig.LODLevel[]
                    {
                        new VoxelLODConfig.LODLevel { Distance = 48f,  VoxelStep = 1, HasCollider = true,  DebugColor = new Color(0, 1, 0, 0.3f) },
                        new VoxelLODConfig.LODLevel { Distance = 96f,  VoxelStep = 2, HasCollider = true,  DebugColor = new Color(1, 1, 0, 0.3f) },
                        new VoxelLODConfig.LODLevel { Distance = 192f, VoxelStep = 4, HasCollider = false, DebugColor = new Color(1, 0.5f, 0, 0.3f) },
                        new VoxelLODConfig.LODLevel { Distance = 384f, VoxelStep = 8, HasCollider = false, DebugColor = new Color(1, 0, 0, 0.3f) },
                    };
                    break;
            }

            EditorUtility.SetDirty(config);
        }
    }
}
