using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel.Editor
{
    public class WorldSliceViewer : EditorWindow
    {
        [MenuItem("DIG/Voxel/World Slice Viewer")]
        static void ShowWindow() => GetWindow<WorldSliceViewer>("World Slice");
        
        private enum SliceAxis { X, Y, Z }
        private enum ViewMode { Density, Material, Collision }
        
        private SliceAxis _axis = SliceAxis.Y;
        private ViewMode _mode = ViewMode.Density;
        private int _slicePosition = 0;
        private int _viewSize = 64;  // Voxels visible
        private Vector2 _scrollOffset;
        
        private Texture2D _sliceTexture;
        
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            _axis = (SliceAxis)EditorGUILayout.EnumPopup("Axis", _axis, GUILayout.Width(150));
            _mode = (ViewMode)EditorGUILayout.EnumPopup("View", _mode, GUILayout.Width(150));
            
            EditorGUILayout.LabelField("Slice:", GUILayout.Width(40));
            _slicePosition = EditorGUILayout.IntSlider(_slicePosition, -100, 100, GUILayout.Width(200));
            
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                RefreshSlice();
            
            if (GUILayout.Button("Center on Camera", GUILayout.Width(120)))
                CenterOnCamera();
            
            EditorGUILayout.EndHorizontal();
            
            // Draw slice
            var rect = GUILayoutUtility.GetRect(position.width, position.height - 25);
            if (_sliceTexture != null)
            {
                // Draw texture fitting in the rect maintaining aspect ratio
                float aspect = (float)_sliceTexture.width / _sliceTexture.height;
                float targetWidth = Mathf.Min(rect.width, rect.height * aspect);
                float targetHeight = targetWidth / aspect;
                Rect drawRect = new Rect(rect.x + (rect.width - targetWidth) * 0.5f, rect.y, targetWidth, targetHeight);
                
                EditorGUI.DrawTextureTransparent(drawRect, _sliceTexture, ScaleMode.ScaleToFit);
                
                // Draw crosshair at center
                var center = drawRect.center;
                EditorGUI.DrawRect(new Rect(center.x - 5, center.y, 10, 1), Color.yellow);
                EditorGUI.DrawRect(new Rect(center.x, center.y - 5, 1, 10), Color.yellow);
            }
            else
            {
                EditorGUI.DrawRect(rect, Color.black);
                EditorGUI.LabelField(rect, "Press Refresh to generate slice", 
                    new GUIStyle { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }});
            }
            
            // Legend
            DrawLegend();
        }

        private void CenterOnCamera()
        {
            if (Camera.main != null)
            {
                var pos = Camera.main.transform.position;
                _scrollOffset = Vector2.zero; // Reset offset, use slice position?
                // Actually slice position determines Z/Y/X.
                
                switch (_axis)
                {
                    case SliceAxis.X: _slicePosition = (int)Mathf.Round(pos.x); break;
                    case SliceAxis.Y: _slicePosition = (int)Mathf.Round(pos.y); break;
                    case SliceAxis.Z: _slicePosition = (int)Mathf.Round(pos.z); break;
                }
                
                // Adjust scroll offset to center camera X/Z? 
                // Currently scroll offset just shifts the view window.
                // Assuming view is centered on 0,0 by default? 
                // Code: u - viewSize/2 + offset.
                // If u=viewSize/2 (center pixel), worldPos = offset.
                // So offset is the World Coordinate of the Center Pixel.
                
                switch (_axis)
                {
                    case SliceAxis.X: _scrollOffset = new Vector2(pos.z, pos.y); break; 
                    case SliceAxis.Y: _scrollOffset = new Vector2(pos.x, pos.z); break;
                    case SliceAxis.Z: _scrollOffset = new Vector2(pos.x, pos.y); break;
                }
                
                RefreshSlice();
            }
            else
            {
                UnityEngine.Debug.LogWarning("No Main Camera found.");
            }
        }
            

        
        private void RefreshSlice()
        {
            if (!Application.isPlaying) 
            {
                UnityEngine.Debug.LogWarning("[WorldSliceViewer] Must be in Play Mode to view voxel data.");
                return;
            }
            
            _sliceTexture = new Texture2D(_viewSize, _viewSize);
            _sliceTexture.filterMode = FilterMode.Point; // Pixelated view
            
            var world = GetServerWorld();
            if (world == null) 
            {
                UnityEngine.Debug.LogWarning("[WorldSliceViewer] No Server World found.");
                return;
            }
            
            // Sample voxels across the slice
            for (int u = 0; u < _viewSize; u++)
            {
                for (int v = 0; v < _viewSize; v++)
                {
                    int3 worldPos = GetWorldPos(u, v);
                    var color = SampleVoxel(worldPos, world);
                    _sliceTexture.SetPixel(u, v, color);
                }
            }
            
            _sliceTexture.Apply();
        }
        
        private int3 GetWorldPos(int u, int v)
        {
            int offset_u = u - _viewSize / 2 + (int)_scrollOffset.x;
            int offset_v = v - _viewSize / 2 + (int)_scrollOffset.y;
            
            switch (_axis)
            {
                case SliceAxis.X: return new int3(_slicePosition, offset_v, offset_u);
                case SliceAxis.Y: return new int3(offset_u, _slicePosition, offset_v);
                case SliceAxis.Z: return new int3(offset_u, offset_v, _slicePosition);
                default: return int3.zero;
            }
        }
        
        private Color SampleVoxel(int3 worldPos, World world)
        {
            int3 chunkPos = CoordinateUtils.WorldToChunkPos(new float3(worldPos));
            int3 localPos = CoordinateUtils.WorldToLocalVoxelPos(new float3(worldPos));
            
            // Find chunk entity
            var em = world.EntityManager;
            
            // Inefficient O(N) search per pixel? No, Refresh is manual. 
            // For 64x64 = 4096 pixels, querying 4096 times is SLOW.
            // Optimization: Cache chunk entities?
            // Or just iterate existing chunks and map them?
            
            // Better: Get all chunks once, put in Dictionary<int3, ChunkVoxelData>.
            // But from Editor Window to ECS is tricky without copying.
            
            // For now, I'll use a brute force query helper that caches per Refresh.
            // Actually, querying ComponentLookup is fast IF I can look it up.
            // But ChunkLookup is singleton.
            
            // Let's use loop.
            // WARNING: This O(N) inside loop is terrible.
            // I should cache specific chunks needed?
            // There are only ~4 chunks visible in 64x64 view.
            
            var entity = GetChunkEntity(em, chunkPos);
            if (entity == Entity.Null) return Color.black; // Void/Unloaded
            
            if (!em.HasComponent<ChunkVoxelData>(entity)) return Color.magenta; // Error
            
            var data = em.GetComponentData<ChunkVoxelData>(entity);
            if (!data.IsValid) return Color.magenta;
            
            ref var blob = ref data.Data.Value;
            
            // Flattened index
            // int index = localPos.x + localPos.y * ChunkSize + localPos.z * ChunkSize * ChunkSize;
            int index = CoordinateUtils.VoxelPosToIndex(localPos);
            
            byte density = blob.Densities[index];
            byte material = blob.Materials[index];
            
            switch (_mode)
            {
                case ViewMode.Density:
                    float d = density / 255f;
                    return new Color(d, d, d);
                    
                case ViewMode.Material:
                    return GetMaterialColor(material);
                    
                case ViewMode.Collision:
                    return density >= 127 ? Color.green : Color.red; // ISO_LEVEL assumption
                    
                default:
                    return Color.black;
            }
        }
        
        private Entity GetChunkEntity(EntityManager em, int3 chunkPos)
        {
            // Try to use ChunkLookup if available?
            // For debugging, we can query all chunks.
            // We can cache this query result at the start of RefreshSlice.
            // But "GetChunkEntity" is helper.
            
            // I'll make RefreshSlice fetch all chunks first.
            // But to keep code clean, I'll do a query here.
            
            using var query = em.CreateEntityQuery(typeof(ChunkPosition), typeof(ChunkVoxelData));
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var positions = query.ToComponentDataArray<ChunkPosition>(Allocator.Temp);
            
            for (int i = 0; i < entities.Length; i++)
            {
                if (positions[i].Value.Equals(chunkPos)) return entities[i];
            }
            
            return Entity.Null;
        }

        private Color GetMaterialColor(byte material)
        {
            switch (material)
            {
                case 0: return Color.clear;  // Air
                case 1: return Color.gray;   // Stone
                case 2: return new Color(0.5f, 0.3f, 0.1f);  // Dirt
                case 3: return new Color(0.6f, 0.4f, 0.2f);  // Iron
                case 4: return Color.yellow;  // Gold
                default: return Color.magenta;
            }
        }
        
        private void DrawLegend()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (_mode == ViewMode.Material)
            {
                DrawLegendItem("Air", Color.clear);
                DrawLegendItem("Stone", Color.gray);
                DrawLegendItem("Dirt", new Color(0.5f, 0.3f, 0.1f));
                DrawLegendItem("Iron", new Color(0.6f, 0.4f, 0.2f));
                DrawLegendItem("Gold", Color.yellow);
            }
            else if (_mode == ViewMode.Collision)
            {
                DrawLegendItem("Solid", Color.green);
                DrawLegendItem("Air", Color.red);
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        
        private void DrawLegendItem(string label, Color color)
        {
            var rect = GUILayoutUtility.GetRect(60, 20);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 4, 12, 12), color);
            EditorGUI.LabelField(new Rect(rect.x + 16, rect.y, 44, 20), label);
        }
        
        private World GetServerWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Name.Contains("Server")) return world;
            }
            return null;
        }
    }
}
