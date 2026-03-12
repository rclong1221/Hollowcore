using Unity.Entities;
using Unity.Mathematics;

namespace Player.Components
{
    /// <summary>
    /// One-frame landing event emitted when a player lands.
    /// Consumed by adapters (camera, animation) on the main-thread.
    /// </summary>
    public struct LandingEvent : IComponentData
    {
        /// <summary>
        /// Normalized intensity of the landing (0..1) useful for animation blending.
        /// </summary>
        public float Intensity;
    }
}
