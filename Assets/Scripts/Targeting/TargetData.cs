using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Targeting
{
    /// <summary>
    /// ECS component storing targeting results.
    /// Written by ITargetingSystem implementations, read by weapon/projectile systems.
    /// Blittable struct for Burst compatibility.
    /// </summary>
    [GhostComponent]
    public struct TargetData : IComponentData
    {
        /// <summary>
        /// Currently targeted entity. Entity.Null if no target.
        /// </summary>
        public Entity TargetEntity;
        
        /// <summary>
        /// World position of the target point (for ground-target abilities).
        /// </summary>
        public float3 TargetPoint;
        
        /// <summary>
        /// Aim direction vector (normalized). Used for projectile spawning.
        /// </summary>
        public float3 AimDirection;
        
        /// <summary>
        /// Whether the current target is valid (alive, in range, line of sight).
        /// </summary>
        public bool HasValidTarget;
        
        /// <summary>
        /// Distance to target (cached for systems that need it).
        /// </summary>
        public float TargetDistance;
        
        /// <summary>
        /// Active targeting mode (for systems that need mode-specific behavior).
        /// </summary>
        public TargetingMode Mode;
    }
}
