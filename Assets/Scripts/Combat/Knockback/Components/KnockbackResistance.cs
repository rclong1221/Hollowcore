using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Knockback resistance and immunity configuration.
    /// Optional — entities without it receive full knockback (zero resistance).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct KnockbackResistance : IComponentData
    {
        /// <summary>
        /// Percentage of knockback force absorbed (0-1).
        /// 0 = full knockback. 0.5 = half. 1.0 = immune.
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float ResistancePercent;

        /// <summary>
        /// Force threshold below which knockback is ignored.
        /// 0 = any force causes knockback. Bypassed by IgnoreSuperArmor.
        /// </summary>
        [GhostField(Quantization = 10)]
        public float SuperArmorThreshold;

        /// <summary>Seconds of knockback immunity after a knockback ends.</summary>
        [GhostField(Quantization = 100)]
        public float ImmunityDuration;

        /// <summary>Remaining immunity time. While > 0, all knockback rejected.</summary>
        [GhostField(Quantization = 100)]
        public float ImmunityTimeRemaining;

        /// <summary>Hard immunity flag. ALL knockback rejected regardless of force.</summary>
        [GhostField]
        public bool IsImmune;

        /// <summary>True if entity is currently in immunity window or hard immune.</summary>
        public bool IsCurrentlyImmune => IsImmune || ImmunityTimeRemaining > 0f;
    }
}
