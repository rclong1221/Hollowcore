using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Player.Components
{
    /// <summary>
    /// Temporary component added when server collision state differs from client prediction.
    /// Used to smoothly interpolate StaggerVelocity corrections over multiple frames.
    /// Epic 7.5.1: Prediction smoothing for collision corrections.
    /// </summary>
    public struct CollisionReconcile : IComponentData
    {
        /// <summary>
        /// The velocity adjustment to apply (server value - predicted value).
        /// </summary>
        public float3 VelocityAdjustment;
        
        /// <summary>
        /// Total time over which to apply the adjustment.
        /// </summary>
        public float TotalTime;
        
        /// <summary>
        /// Remaining time for the adjustment.
        /// </summary>
        public float RemainingTime;
        
        /// <summary>
        /// Stagger time adjustment (if server stagger duration differs).
        /// </summary>
        public float StaggerTimeAdjustment;
        
        /// <summary>
        /// Knockdown time adjustment (if server knockdown duration differs).
        /// </summary>
        public float KnockdownTimeAdjustment;
        
        /// <summary>
        /// Collision cooldown adjustment (if server cooldown differs from client prediction).
        /// Epic 7.5.2: Cooldown reconciliation for dual-client collision scenarios.
        /// </summary>
        public float CooldownAdjustment;
    }
}
