using Unity.Entities;

namespace DIG.Vision.Components
{
    /// <summary>
    /// Marker component for entities that can be detected by DetectionSensor.
    /// Attach to players, allies, or any entity that AI should be able to see.
    /// Implements IEnableableComponent so detectability can be toggled without
    /// structural changes (e.g. entering a safe zone, becoming fully invisible).
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// </summary>
    public struct Detectable : IComponentData, IEnableableComponent
    {
        /// <summary>Vertical offset from entity origin for the raycast target point (center mass).</summary>
        public float DetectionHeightOffset;

        /// <summary>
        /// Multiplier applied to the sensor's ViewDistance when checking this target.
        /// 1.0 = fully visible (normal), 0.5 = half detection range (crouching),
        /// 0.0 = invisible. Other systems modify this for stealth mechanics.
        /// </summary>
        public float StealthMultiplier;
    }
}
