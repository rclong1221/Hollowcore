using Unity.Entities;

namespace DIG.Vision.Components
{
    /// <summary>
    /// Singleton component for global vision system configuration.
    /// Created by VisionSettingsAuthoring. Systems fall back to defaults
    /// if this singleton is not present.
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// </summary>
    public struct VisionSettings : IComponentData
    {
        /// <summary>Default scan interval for sensors that don't override it.</summary>
        public float GlobalUpdateInterval;

        /// <summary>How long (seconds) a sensor remembers a target after losing sight.</summary>
        public float MemoryDuration;

        /// <summary>Maximum number of occlusion raycasts per frame across all sensors.</summary>
        public int MaxRaycastsPerFrame;

        /// <summary>Master toggle for stealth modifier application.</summary>
        public bool EnableStealthModifiers;

        /// <summary>
        /// Spread sensor updates across this many frames to prevent thundering herd.
        /// With 100 sensors and SensorSpreadFrames=10, only ~10 sensors run per frame.
        /// 0 or 1 = no spreading (all eligible sensors run every frame).
        /// </summary>
        public int SensorSpreadFrames;

        public static VisionSettings Default => new VisionSettings
        {
            GlobalUpdateInterval = 0.2f,
            MemoryDuration = 5.0f,
            MaxRaycastsPerFrame = 16,
            EnableStealthModifiers = true,
            SensorSpreadFrames = 10
        };
    }
}
