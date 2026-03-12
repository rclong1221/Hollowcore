using Unity.Entities;

namespace DIG.Targeting.Components
{
    /// <summary>
    /// EPIC 15.16: Singleton component that caches target lock settings for ECS access.
    /// Updated each frame by TargetLockSettingsSyncSystem from the managed TargetLockSettingsManager.
    /// </summary>
    public struct TargetLockSettings : IComponentData
    {
        /// <summary>
        /// Whether target locking is allowed (Tab/Grab input).
        /// </summary>
        public bool AllowTargetLock;
        
        /// <summary>
        /// Whether aim assist is allowed.
        /// </summary>
        public bool AllowAimAssist;
        
        /// <summary>
        /// Whether to show lock indicator UI.
        /// </summary>
        public bool ShowIndicator;
        
        /// <summary>
        /// Version number - incremented on settings change for reactive systems.
        /// </summary>
        public int Version;
    }
}
