using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Shared
{
    /// <summary>
    /// Stores world positions of character sockets (hands) for use by ECS systems.
    /// Updated each frame by SocketPositionSyncBridge from the animated skeleton.
    /// </summary>
    /// <remarks>
    /// This bridges the gap between MonoBehaviour-land (where the animated skeleton lives)
    /// and ECS systems that need to know hand positions (e.g., throwable spawn origin).
    /// </remarks>
    public struct SocketPositionData : IComponentData
    {
        /// <summary>
        /// World position of the main hand socket (right hand).
        /// Used as spawn origin for throwables, projectiles, etc.
        /// </summary>
        public float3 MainHandPosition;

        /// <summary>
        /// World position of the off-hand socket (left hand).
        /// Used for off-hand items like shields, dual-wield weapons.
        /// </summary>
        public float3 OffHandPosition;

        /// <summary>
        /// Whether the socket data has been initialized.
        /// If false, systems should fall back to offset-based calculation.
        /// </summary>
        public bool IsValid;
    }
}
