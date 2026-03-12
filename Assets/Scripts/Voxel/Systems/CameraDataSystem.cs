using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Components;
using DIG.Voxel.Core;

namespace DIG.Voxel.Systems
{
    /// <summary>
    /// OPTIMIZATION 10.9.5: Camera Caching and Frustum Plane Pre-computation.
    /// 
    /// Runs first in the frame to cache camera data once.
    /// All other systems read from the CameraData singleton instead of calling Camera.main.
    /// 
    /// Benefits:
    /// - Eliminates 10+ Camera.main lookups per frame
    /// - Pre-computes frustum planes once instead of multiple times
    /// - Caches chunk coordinates for streaming/LOD systems
    /// - Provides Burst-compatible frustum data (float4 planes)
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CameraDataSystem : SystemBase
    {
        private Entity _singletonEntity;
        private Plane[] _unityPlanes = new Plane[6];
        
        protected override void OnCreate()
        {
            RequireForUpdate<VoxelWorldEnabled>();

            // Create singleton entity with CameraData component
            _singletonEntity = EntityManager.CreateEntity(typeof(CameraData));
            EntityManager.SetName(_singletonEntity, "CameraData");

            // Initialize with invalid data
            EntityManager.SetComponentData(_singletonEntity, new CameraData { IsValid = false });
        }
        
        protected override void OnDestroy()
        {
            if (EntityManager.Exists(_singletonEntity))
            {
                EntityManager.DestroyEntity(_singletonEntity);
            }
        }
        
        protected override void OnUpdate()
        {
            var cam = Camera.main;
            
            // Fallback to SceneView camera in editor if no main camera
            #if UNITY_EDITOR
            if (cam == null && UnityEditor.SceneView.lastActiveSceneView != null)
            {
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
            }
            #endif
            
            if (cam == null)
            {
                // No camera available - mark as invalid
                EntityManager.SetComponentData(_singletonEntity, new CameraData { IsValid = false });
                return;
            }
            
            var transform = cam.transform;
            float3 position = transform.position;
            float3 forward = transform.forward;
            float3 up = transform.up;
            float3 right = transform.right;
            
            // Calculate chunk position
            int3 chunkPos = CoordinateUtils.WorldToChunkPos(position);
            
            // Calculate frustum planes using Unity's utility
            GeometryUtility.CalculateFrustumPlanes(cam, _unityPlanes);
            
            // Convert to Burst-compatible format (float4: normal.xyz, distance.w)
            var frustum = new CachedFrustumPlanes
            {
                Left = PlaneToFloat4(_unityPlanes[0]),
                Right = PlaneToFloat4(_unityPlanes[1]),
                Bottom = PlaneToFloat4(_unityPlanes[2]),
                Top = PlaneToFloat4(_unityPlanes[3]),
                Near = PlaneToFloat4(_unityPlanes[4]),
                Far = PlaneToFloat4(_unityPlanes[5])
            };
            
            // Update singleton
            EntityManager.SetComponentData(_singletonEntity, new CameraData
            {
                Position = position,
                Forward = forward,
                Up = up,
                Right = right,
                ChunkPosition = chunkPos,
                IsValid = true,
                Frustum = frustum
            });
        }
        
        /// <summary>
        /// Convert Unity Plane to float4 (normal.xyz, distance.w).
        /// </summary>
        private static float4 PlaneToFloat4(Plane plane)
        {
            return new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
        }
    }
}
