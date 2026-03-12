using Unity.Mathematics;
using Unity.Entities;

namespace Player.Utilities
{
    // Helper for voxel-aware mount anchor computation.
    // This file implements a fallback sampling strategy and provides a
    // pluggable hook (`VoxelSampler`) so a real voxel system can supply
    // high-quality anchors later (chunk metadata, topology, etc.).
    public struct MountAnchor
    {
        public float3 Position;      // canonical anchor position
        public float3 Normal;        // surface normal at anchor
        public float3 TopPosition;   // top endpoint for climb (world space)
        public float3 BottomPosition;// bottom endpoint for climb (world space)
        public bool IsVoxelSurface;  // true if computed from voxel sampling
        public Entity ChunkEntity;   // optional chunk entity (if known)
    }

    public static class VoxelClimbHelper
    {
        // Delegate that voxel systems can set to provide real sampling.
        // Signature: (hitPoint, hitNormal, out anchor) => bool success
        public delegate bool VoxelSampleDelegate(float3 hitPoint, float3 hitNormal, out MountAnchor anchor);
        public static VoxelSampleDelegate VoxelSampler = null;

        // Attempts to compute a stable mount anchor from a physics hit point.
        // If `VoxelSampler` is assigned it will be used. Otherwise a heuristic
        // fallback is returned so climb detection can still function.
        public static bool TryComputeMountAnchor(float3 hitPoint, float3 hitNormal, out MountAnchor anchor)
        {
            // If a real voxel sampler is provided by the voxel system, defer to it.
            if (VoxelSampler != null)
            {
                return VoxelSampler(hitPoint, hitNormal, out anchor);
            }

            // Fallback heuristic:
            // - Anchor at the hit point
            // - Normal from the physics hit
            // - Top/Bottom positions estimated vertically around the hit point
            // This is intentionally conservative and simple; replace with voxel-aware
            // neighborhood sampling when chunk metadata is available.
            var up = new float3(0f, 1f, 0f);
            anchor = new MountAnchor
            {
                Position = hitPoint,
                Normal = hitNormal,
                TopPosition = hitPoint + up * 1.5f,
                BottomPosition = hitPoint - up * 1.0f,
                IsVoxelSurface = false,
                ChunkEntity = Entity.Null
            };

            return true;
        }
    }
}
