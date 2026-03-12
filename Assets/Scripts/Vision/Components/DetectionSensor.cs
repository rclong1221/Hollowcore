using Unity.Entities;

namespace DIG.Vision.Components
{
    /// <summary>
    /// Component for AI agents defining their detection capabilities.
    /// Includes vision cone, proximity sensing, and hearing.
    /// Attach to any entity that needs to detect other entities.
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// </summary>
    public struct DetectionSensor : IComponentData
    {
        /// <summary>Maximum detection range in meters.</summary>
        public float ViewDistance;

        /// <summary>Half-angle of the horizontal vision cone in degrees (e.g. 45 = 90 degree FOV).</summary>
        public float ViewAngle;

        /// <summary>
        /// Half-angle of the vertical vision cone in degrees.
        /// Humans look mostly forward with limited up/down peripheral vision.
        /// Set to 180 for creatures that can look in all vertical directions.
        /// </summary>
        public float VerticalViewAngle;

        /// <summary>Vertical offset from entity origin for eye position.</summary>
        public float EyeHeight;

        /// <summary>
        /// 360-degree proximity detection radius (meters). Targets within this range are
        /// always detected regardless of facing direction (simulates close-range sensing).
        /// Set to 0 to disable proximity detection (default for most humanoids).
        /// </summary>
        public float ProximityRadius;

        /// <summary>
        /// 360-degree hearing radius (meters). Detects combat sounds, running, etc.
        /// Unlike ProximityRadius, this only triggers on noisy actions.
        /// Set to 0 to disable hearing (deaf creatures).
        /// </summary>
        public float HearingRadius;

        /// <summary>Seconds between detection scans. Higher = cheaper. Per-entity throttle.</summary>
        public float UpdateInterval;

        /// <summary>Internal accumulator tracking time since last scan. Do not set manually.</summary>
        public float TimeSinceLastUpdate;
    }
}
