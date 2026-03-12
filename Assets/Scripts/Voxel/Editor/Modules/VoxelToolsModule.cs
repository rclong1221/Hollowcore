using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using DIG.Voxel.Systems.Interaction;
using DIG.Voxel.Core;

namespace DIG.Voxel.Editor.Modules
{
    public class VoxelToolsModule : IVoxelModule
    {
        // Crater settings
        private float _radius = 5f;
        private float _strength = 1f;
        private bool _spawnLoot = true;
        private bool _showPreview = true;
        
        // Preview state
        private bool _placementMode = false;
        private Vector3 _previewPosition;
        
        // Statistics
        private int _lastVoxelsDestroyed;
        private float _lastExplosionTime;
        private int _totalExplosions;

        public void Initialize() { }
        public void OnDestroy() { _placementMode = false; }

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("Explosion & Interaction Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use interaction tools.", MessageType.Info);
                DrawSettingsSection();
                return;
            }
            
            DrawSettingsSection();
            EditorGUILayout.Space(10);
            DrawActionsSection();
            EditorGUILayout.Space(10);
            DrawStatisticsSection();
        }
        
        public void DrawSceneGUI(SceneView sceneView)
        {
            if (!Application.isPlaying) return;
            
            // Handle placement mode
            if (_placementMode)
            {
                HandlePlacementMode(sceneView);
            }
            
            // Always show preview if enabled and in placement mode
            if (_showPreview && _placementMode)
            {
                DrawPreviewSphere();
            }
            
            // Visualize active explosions (Time-Sliced)
            DrawDebugVisualization();
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("Crater Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _radius = EditorGUILayout.Slider("Radius", _radius, 1f, 30f);
            _strength = EditorGUILayout.Slider("Strength", _strength, 0.1f, 1f);
            _spawnLoot = EditorGUILayout.Toggle("Spawn Loot", _spawnLoot);
            _showPreview = EditorGUILayout.Toggle("Show Preview Sphere", _showPreview);
            
            // Radius presets
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Small (3)")) _radius = 3f;
            if (GUILayout.Button("Medium (8)")) _radius = 8f;
            if (GUILayout.Button("Large (15)")) _radius = 15f;
            if (GUILayout.Button("Huge (25)")) _radius = 25f;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawActionsSection()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Toggle placement mode
            var toggleStyle = new GUIStyle(GUI.skin.button);
            if (_placementMode)
            {
                toggleStyle.normal.textColor = Color.yellow;
                toggleStyle.fontStyle = FontStyle.Bold;
            }
            
            if (GUILayout.Button(_placementMode ? "🎯 PLACEMENT MODE ACTIVE (Click Scene)" : "Start Placement Mode", toggleStyle))
            {
                _placementMode = !_placementMode;
                SceneView.RepaintAll();
            }
            
            EditorGUILayout.Space(5);
            
            // Quick spawn buttons
            EditorGUILayout.LabelField("Quick Spawn:", EditorStyles.miniLabel);
            
            if (GUILayout.Button("Explosion at Scene Camera"))
            {
                SpawnExplosionAtSceneCamera();
            }
            
            if (GUILayout.Button("Explosion at Game Camera"))
            {
                SpawnExplosionAtGameCamera();
            }
            
            if (GUILayout.Button("Explosion at Origin (0,-5,0)"))
            {
                SpawnExplosion(new Vector3(0, -5, 0));
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatisticsSection()
        {
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField($"Total Explosions: {_totalExplosions}");
            EditorGUILayout.LabelField($"Last Voxels Destroyed: {_lastVoxelsDestroyed}");
            
            if (_lastExplosionTime > 0)
            {
                float timeSince = Time.time - _lastExplosionTime;
                EditorGUILayout.LabelField($"Time Since Last: {timeSince:F1}s");
            }
            
            EditorGUILayout.EndVertical();
            
            // Estimated impact
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Estimated Impact", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            float volume = (4f / 3f) * Mathf.PI * _radius * _radius * _radius;
            int estimatedVoxels = (int)(volume * _strength);
            int estimatedChunks = Mathf.CeilToInt(_radius * 2 / VoxelConstants.CHUNK_SIZE) + 1;
            estimatedChunks = estimatedChunks * estimatedChunks * estimatedChunks;
            
            EditorGUILayout.LabelField($"Estimated Voxels: ~{estimatedVoxels}");
            EditorGUILayout.LabelField($"Affected Chunks: ~{estimatedChunks}");
            
            if (estimatedVoxels > 5000)
            {
                EditorGUILayout.HelpBox("Large explosion! May cause frame drop.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawDebugVisualization()
        {
            var world = GetServerWorld();
            if (world == null) return;
            
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(ExplosionState));
            if (query.IsEmpty) return;
            
            using var states = query.ToComponentDataArray<ExplosionState>(Allocator.Temp);
            foreach (var state in states)
            {
                // Draw sphere for explosion bounds
                Handles.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange transparent
                Handles.SphereHandleCap(0, state.Center, Quaternion.identity, state.Radius * 2, EventType.Repaint);
                
                // Draw progress wireframe
                Handles.color = Color.red;
                Handles.DrawWireDisc(state.Center, Vector3.up, state.Radius);
                
                // Draw Progress Label
                float progress = (float)state.ProcessedChunks / math.max(1, state.TotalChunks);
                string text = $"💥 EXPLOSION PROCESSING\nProgress: {progress:P0}\nChunks: {state.ProcessedChunks}/{state.TotalChunks}";
                
                Handles.Label((Vector3)state.Center + Vector3.up * (state.Radius + 2f), text, EditorStyles.whiteBoldLabel);
            }
        }
        
        private World GetServerWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Name.Contains("Server")) return world;
            }
            return null;
        }
        
        private void HandlePlacementMode(SceneView sceneView)
        {
            Event e = Event.current;
            
            // Get mouse position in world
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            // Raycast against terrain/chunks
            // For simplicity, use a plane at Y=-5 or find intersection with voxel terrain
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, -5, 0));
            if (groundPlane.Raycast(ray, out float distance))
            {
                _previewPosition = ray.GetPoint(distance);
            }
            
            // Left click to place
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                SpawnExplosion(_previewPosition);
                e.Use();
            }
            
            // Escape to exit placement mode
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _placementMode = false;
                // Force window repaint
                EditorWindow.GetWindow<VoxelWorkstationWindow>().Repaint();
            }
            
            // Force scene view to update
            sceneView.Repaint();
        }
        
        private void DrawPreviewSphere()
        {
            Handles.color = new Color(1f, 0.3f, 0.1f, 0.3f);
            Handles.SphereHandleCap(0, _previewPosition, Quaternion.identity, _radius * 2, EventType.Repaint);
            
            Handles.color = new Color(1f, 0.5f, 0.2f, 0.8f);
            Handles.DrawWireDisc(_previewPosition, Vector3.up, _radius);
            Handles.DrawWireDisc(_previewPosition, Vector3.right, _radius);
            Handles.DrawWireDisc(_previewPosition, Vector3.forward, _radius);
            
            // Draw label
            Handles.Label(_previewPosition + Vector3.up * _radius * 1.2f, 
                $"Radius: {_radius:F1}\nClick to detonate", EditorStyles.whiteBoldLabel);
        }
        
        private void SpawnExplosionAtSceneCamera()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                Vector3 pos = sceneView.camera.transform.position + sceneView.camera.transform.forward * 10f;
                SpawnExplosion(pos);
            }
        }
        
        private void SpawnExplosionAtGameCamera()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 pos = cam.transform.position + cam.transform.forward * 10f;
                SpawnExplosion(pos);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[ExplosionTester] No Main Camera found!");
            }
        }
        
        private void SpawnExplosion(Vector3 position)
        {
            World targetWorld = GetServerWorld();
            
            if (targetWorld == null)
            {
                UnityEngine.Debug.LogError("[ToolsModule] No Server world found! Cannot spawn authoritative explosion.");
                return;
            }
            
            var em = targetWorld.EntityManager;
            
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new CreateCraterRequest
            {
                Center = position,
                Radius = _radius,
                Strength = Unity.Mathematics.math.clamp(_strength, 0f, 1f),
                ReplaceMaterial = 0, // AIR
                SpawnLoot = _spawnLoot,
                FromRpc = false
            });
            
            UnityEngine.Debug.Log($"[ToolsModule] Queued authoritative explosion in {targetWorld.Name}");
            
            _totalExplosions++;
            _lastExplosionTime = Time.time;
            
            float volume = (4f / 3f) * Mathf.PI * _radius * _radius * _radius;
            _lastVoxelsDestroyed = (int)(volume * _strength);
        }
    }
}
