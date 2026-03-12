using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Vision.Components
{
    /// <summary>
    /// Dynamic buffer element storing a perceived target.
    /// Each VisionSensor entity has a buffer of these representing what it currently sees
    /// or recently saw.
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct SeenTargetElement : IBufferElementData
    {
        /// <summary>The detected entity.</summary>
        public Entity Entity;

        /// <summary>World position where the target was last confirmed visible.</summary>
        public float3 LastKnownPosition;

        /// <summary>Seconds since this target was last confirmed visible. 0 = visible right now.</summary>
        public float TimeSinceLastSeen;

        /// <summary>True if the target is currently within cone and has clear LOS.</summary>
        public bool IsVisibleNow;
    }
}
