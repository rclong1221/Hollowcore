using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// OPTIMIZATION 10.9.5: Singleton component caching camera data.
    /// Updated once per frame by CameraDataSystem, read by all other systems.
    /// Eliminates redundant Camera.main lookups and frustum plane calculations.
    /// </summary>
    public struct CameraData : IComponentData
    {
        /// <summary>World position of the main camera.</summary>
        public float3 Position;
        
        /// <summary>Forward direction of the main camera.</summary>
        public float3 Forward;
        
        /// <summary>Up direction of the main camera.</summary>
        public float3 Up;
        
        /// <summary>Right direction of the main camera.</summary>
        public float3 Right;
        
        /// <summary>Chunk coordinates containing the camera.</summary>
        public int3 ChunkPosition;
        
        /// <summary>Whether camera data is valid (Camera.main exists).</summary>
        public bool IsValid;
        
        /// <summary>
        /// Pre-computed frustum planes (packed as float4: xyz=normal, w=distance).
        /// Order: Left, Right, Bottom, Top, Near, Far
        /// </summary>
        public CachedFrustumPlanes Frustum;
    }
    
    /// <summary>
    /// Pre-computed frustum planes stored as float4 (normal.xyz, distance.w).
    /// </summary>
    public struct CachedFrustumPlanes
    {
        public float4 Left;
        public float4 Right;
        public float4 Bottom;
        public float4 Top;
        public float4 Near;
        public float4 Far;
        
        /// <summary>
        /// Test if an AABB is inside or intersects the frustum.
        /// </summary>
        public bool TestAABB(float3 center, float3 extents)
        {
            // Test against each plane
            if (!TestPlane(Left, center, extents)) return false;
            if (!TestPlane(Right, center, extents)) return false;
            if (!TestPlane(Bottom, center, extents)) return false;
            if (!TestPlane(Top, center, extents)) return false;
            if (!TestPlane(Near, center, extents)) return false;
            if (!TestPlane(Far, center, extents)) return false;
            return true;
        }
        
        private static bool TestPlane(float4 plane, float3 center, float3 extents)
        {
            float3 normal = plane.xyz;
            float distance = plane.w;
            
            // Compute the projection interval radius
            float r = math.dot(extents, math.abs(normal));
            
            // Compute signed distance from center to plane
            float s = math.dot(normal, center) + distance;
            
            // If s + r < 0, the box is completely behind the plane
            return s + r >= 0;
        }
    }
}
