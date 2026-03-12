using Unity.Entities;

namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7 Phase 7: Predefined VFX quality levels.
    /// </summary>
    public enum VFXQualityPreset : byte
    {
        Ultra = 0,
        High = 1,
        Medium = 2,
        Low = 3,
        Minimal = 4
    }

    /// <summary>
    /// EPIC 16.7 Phase 7: Singleton tracking the current quality preset.
    /// When IsDirty is true, VFXQualityApplySystem applies the preset to config singletons.
    /// </summary>
    public struct VFXQualityState : IComponentData
    {
        public VFXQualityPreset CurrentPreset;
        public bool IsDirty;
    }
}
