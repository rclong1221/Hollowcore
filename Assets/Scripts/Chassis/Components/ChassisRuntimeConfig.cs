using Unity.Entities;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Singleton for runtime-tunable chassis parameters.
    /// Initialized from ChassisConfigSO, modifiable in play mode.
    /// </summary>
    public struct ChassisRuntimeConfig : IComponentData
    {
        /// <summary>Base limb integrity regen rate per second (out of combat).</summary>
        public float LimbRegenRate;

        /// <summary>Temporary limb default duration multiplier (1.0 = as authored).</summary>
        public float TemporaryDurationMultiplier;

        /// <summary>Global limb damage multiplier (for difficulty scaling).</summary>
        public float LimbDamageMultiplier;

        /// <summary>Memory bonus global multiplier (1.0 = as authored).</summary>
        public float MemoryBonusMultiplier;

        public static ChassisRuntimeConfig Default => new ChassisRuntimeConfig
        {
            LimbRegenRate = 5f,
            TemporaryDurationMultiplier = 1f,
            LimbDamageMultiplier = 1f,
            MemoryBonusMultiplier = 1f
        };
    }

    /// <summary>
    /// Dirty flag. Enable to force systems to re-read config on next frame.
    /// </summary>
    public struct ChassisConfigDirty : IComponentData, IEnableableComponent { }
}
