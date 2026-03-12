using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel.Editor
{
    [InitializeOnLoad]
    public static class ChunkGizmoDrawer
    {
        static ChunkGizmoDrawer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!Application.isPlaying) return;
            if (!DIG.Voxel.Editor.Modules.VoxelDebugModule.DrawChunkBounds) return;
            
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            var cam = sceneView.camera;
            if (cam == null) return;
            
            var centerChunk = CoordinateUtils.WorldToChunkPos(cam.transform.position);
            int radius = DIG.Voxel.Editor.Modules.VoxelDebugModule.GizmoChunkRadius;
            
            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(typeof(ChunkPosition));
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            foreach (var entity in entities)
            {
                if (!entityManager.HasComponent<ChunkPosition>(entity)) continue;
                var pos = entityManager.GetComponentData<ChunkPosition>(entity);
                
                // Only draw nearby chunks
                if (math.abs(pos.Value.x - centerChunk.x) > radius ||
                    math.abs(pos.Value.y - centerChunk.y) > radius ||
                    math.abs(pos.Value.z - centerChunk.z) > radius)
                    continue;
                
                DrawChunkGizmo(entity, pos.Value, entityManager);
            }
        }
        
        private static void DrawChunkGizmo(Entity entity, int3 chunkPos, EntityManager em)
        {
            var worldPos = CoordinateUtils.ChunkToWorldPos(chunkPos);

            float sizeWorld = VoxelConstants.CHUNK_SIZE * VoxelConstants.VOXEL_SIZE;
            var center = worldPos + new float3(sizeWorld * 0.5f);
            
            // Determine color based on state
            Color color = Color.gray;
            
            if (em.HasComponent<ChunkVoxelData>(entity))
            {
                var data = em.GetComponentData<ChunkVoxelData>(entity);
                if (data.IsValid)
                    color = Color.blue;
            }
            
            if (em.HasComponent<ChunkMeshState>(entity))
            {
                var state = em.GetComponentData<ChunkMeshState>(entity);
                if (state.HasMesh)
                    color = Color.green;
            }
            
            if (em.IsComponentEnabled<ChunkNeedsRemesh>(entity))
            {
                color = Color.yellow;
            }
            
            // Draw wireframe box
            Handles.color = color;
            Handles.DrawWireCube(center, new Vector3(sizeWorld, sizeWorld, sizeWorld));
            
            // Draw label
            var labelPos = center + new float3(0, sizeWorld * 0.6f, 0);
            Handles.Label(labelPos, $"{chunkPos.x},{chunkPos.y},{chunkPos.z}");
        }
    }
}
