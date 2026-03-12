using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// Interface for swappable target indicator visuals.
    /// Allows mixing UI sprites, VFX, Decals, or Projectors.
    /// </summary>
    public interface ITargetIndicator
    {
        /// <summary>
        /// Initialize with player entity reference.
        /// </summary>
        void Initialize(EntityManager em, Entity playerEntity);
        
        /// <summary>
        /// Update indicator position and state.
        /// Called by TargetingSystem or MonoBehaviour Update.
        /// </summary>
        void UpdateIndicator(float3 worldPosition, bool hasValidTarget, bool isLocked);
        
        /// <summary>
        /// Show or hide the indicator.
        /// </summary>
        void SetVisible(bool visible);
    }
}
