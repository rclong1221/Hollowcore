using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Player.Components
{
    /// <summary>
    /// EPIC 15.20: Stores the locked facing direction for isometric modes.
    /// When attacking, the cursor direction is stored here.
    /// Player maintains this facing until they move again.
    /// </summary>
    public struct IsometricFacingLock : IComponentData
    {
        /// <summary>
        /// The locked facing direction (normalized, XZ plane).
        /// </summary>
        public float3 LockedDirection;
        
        /// <summary>
        /// Whether a direction is currently locked.
        /// True after attack, cleared when movement starts.
        /// </summary>
        public bool IsLocked;
    }
}
