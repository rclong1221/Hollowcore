using Unity.Entities;
using Unity.Mathematics;

namespace Player.Components
{
    /// <summary>
    /// EPIC 13.14.5: Teleport Event Component
    ///
    /// Enable this component when teleporting a player to signal the fall system
    /// that an immediate transform change occurred. The FallDetectionSystem will
    /// check this and handle the fall state appropriately.
    ///
    /// Usage:
    /// 1. Before teleporting, set PendingImmediateTransformChange on FallAbility
    /// 2. OR enable this component and set the target position
    /// 3. The teleport system moves the entity
    /// 4. FallDetectionSystem handles fall state cleanup
    ///
    /// Note: For most cases, you can directly set FallAbility.PendingImmediateTransformChange
    /// before moving the entity. This component is for systems that need to coordinate
    /// the teleport with animation snapping.
    /// </summary>
    public struct TeleportEvent : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// Target position for the teleport.
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Target rotation for the teleport.
        /// </summary>
        public quaternion TargetRotation;

        /// <summary>
        /// If true, snap the animator to the new state immediately.
        /// Mirrors Opsive's OnImmediateTransformChange(snapAnimator) parameter.
        /// </summary>
        public bool SnapAnimator;
    }
}
