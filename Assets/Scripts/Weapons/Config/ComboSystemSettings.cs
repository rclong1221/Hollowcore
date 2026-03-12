using Unity.Entities;

namespace DIG.Weapons.Config
{
    /// <summary>
    /// ECS singleton component holding runtime combo configuration.
    /// Baked from ComboSystemConfig ScriptableObject.
    /// </summary>
    public struct ComboSystemSettings : IComponentData
    {
        public ComboInputMode InputMode;
        public int QueueDepth;
        public ComboCancelPolicy CancelPolicy;
        public ComboCancelPriority CancelPriority;
        public ComboQueueClearPolicy QueueClearPolicy;
        public float RhythmWindowStart;
        public float RhythmWindowEnd;
        public float RhythmPerfectBonus;
        public bool AllowPerWeaponOverride;

        /// <summary>
        /// Default Souls-like configuration.
        /// </summary>
        public static ComboSystemSettings Default => new ComboSystemSettings
        {
            InputMode = ComboInputMode.InputPerSwing,
            QueueDepth = 1,
            CancelPolicy = ComboCancelPolicy.RecoveryOnly,
            CancelPriority = ComboCancelPriority.Dodge,
            QueueClearPolicy = ComboQueueClearPolicy.Standard,
            RhythmWindowStart = 0.6f,
            RhythmWindowEnd = 0.9f,
            RhythmPerfectBonus = 1.25f,
            AllowPerWeaponOverride = true
        };
    }
}
