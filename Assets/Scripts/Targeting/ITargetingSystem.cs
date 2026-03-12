using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Targeting
{
    /// <summary>
    /// Interface for targeting system implementations.
    /// MonoBehaviour-based for polymorphism and easy mode switching.
    /// Writes results to TargetData ECS component.
    /// </summary>
    public interface ITargetingSystem
    {
        /// <summary>
        /// Current targeting mode.
        /// </summary>
        TargetingMode Mode { get; }
        
        /// <summary>
        /// Initialize the targeting system with configuration.
        /// </summary>
        void Initialize(TargetingConfig config);
        
        /// <summary>
        /// Called each frame to update targeting state.
        /// Writes results to TargetData component on ownerEntity.
        /// </summary>
        void UpdateTargeting(EntityManager em, Entity ownerEntity);
        
        /// <summary>
        /// Get the currently targeted entity (may be Entity.Null).
        /// </summary>
        Entity GetPrimaryTarget();
        
        /// <summary>
        /// Get the aim direction vector (normalized).
        /// </summary>
        float3 GetAimDirection();
        
        /// <summary>
        /// Get the target world position (for ground-target abilities).
        /// </summary>
        float3 GetTargetPoint();
        
        /// <summary>
        /// Check if current target is valid (alive, in range, visible).
        /// </summary>
        bool HasValidTarget();
        
        /// <summary>
        /// Manually set a target entity (for click-select mode).
        /// </summary>
        void SetTarget(Entity target);
        
        /// <summary>
        /// Clear the current target.
        /// </summary>
        void ClearTarget();
        
        /// <summary>
        /// Cycle to next/previous target (for lock-on mode).
        /// </summary>
        /// <param name="direction">1 for next, -1 for previous</param>
        void CycleTarget(int direction);
    }
}
