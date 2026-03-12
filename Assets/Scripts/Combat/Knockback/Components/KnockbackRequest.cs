using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Request entity for knockback application.
    /// Any system creates this as a standalone entity. KnockbackResolveSystem consumes and destroys it.
    /// Lifetime: 1 frame.
    /// </summary>
    public struct KnockbackRequest : IComponentData
    {
        /// <summary>Entity to apply knockback to. Must have KnockbackState component.</summary>
        public Entity TargetEntity;

        /// <summary>Entity that caused the knockback. Entity.Null if environmental.</summary>
        public Entity SourceEntity;

        /// <summary>Normalized direction of knockback force in world space.</summary>
        public float3 Direction;

        /// <summary>
        /// Knockback force magnitude in Newtons.
        /// Reference: 200=light shove, 500=grenade, 1000=heavy melee, 2000=breaching charge.
        /// </summary>
        public float Force;

        /// <summary>Knockback behavior type.</summary>
        public KnockbackType Type;

        /// <summary>Distance-based falloff. Only relevant for area knockback.</summary>
        public KnockbackFalloff Falloff;

        /// <summary>Distance from source to target at time of request. 0 for single-target.</summary>
        public float Distance;

        /// <summary>Maximum radius for falloff calculation. Ignored when Falloff = None.</summary>
        public float MaxRadius;

        /// <summary>Override easing curve. Default (EaseOut) if not specified.</summary>
        public KnockbackEasing Easing;

        /// <summary>Override duration in seconds. 0 = use default from KnockbackConfig.</summary>
        public float DurationOverride;

        /// <summary>For Launch type: vertical force multiplier (0-1). 0=no vertical, 0.5=45-degree arc.</summary>
        public float LaunchVerticalRatio;

        /// <summary>If true, ignores SuperArmor threshold (guaranteed knockback).</summary>
        public bool IgnoreSuperArmor;

        /// <summary>If true, triggers an InterruptRequest on the target (EPIC 16.1).</summary>
        public bool TriggersInterrupt;
    }
}
