using Unity.Entities;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Global knockback tuning parameters. Singleton entity.
    /// </summary>
    public struct KnockbackConfig : IComponentData
    {
        // Duration defaults (seconds)
        public float PushDuration;
        public float LaunchDuration;
        public float PullDuration;
        public float StaggerDuration;

        // Force-to-velocity conversion
        /// <summary>Velocity = Force / ForceDivisor. Higher = slower knockback.</summary>
        public float ForceDivisor;
        /// <summary>Maximum knockback velocity magnitude (m/s).</summary>
        public float MaxVelocity;
        /// <summary>Minimum force (after resistance) to produce knockback.</summary>
        public float MinimumEffectiveForce;

        // Launch tuning
        /// <summary>Default vertical ratio for Launch type (0-1).</summary>
        public float DefaultLaunchVerticalRatio;
        /// <summary>Gravity multiplier for Launch arc descent.</summary>
        public float LaunchGravityMultiplier;

        // Stagger tuning
        /// <summary>Force multiplier for Stagger type (typically 0.2).</summary>
        public float StaggerForceMultiplier;
        /// <summary>Freeze frames at stagger start (in fixed timesteps).</summary>
        public int StaggerFreezeFrames;

        // Surface friction
        /// <summary>If true, knockback slide is affected by surface material friction.</summary>
        public bool EnableSurfaceFriction;

        // Interrupt
        /// <summary>Force threshold above which knockback triggers InterruptRequest.</summary>
        public float InterruptForceThreshold;

        public static KnockbackConfig Default => new KnockbackConfig
        {
            PushDuration = 0.4f,
            LaunchDuration = 0.6f,
            PullDuration = 0.5f,
            StaggerDuration = 0.2f,
            ForceDivisor = 100f,
            MaxVelocity = 25f,
            MinimumEffectiveForce = 50f,
            DefaultLaunchVerticalRatio = 0.4f,
            LaunchGravityMultiplier = 1.5f,
            StaggerForceMultiplier = 0.2f,
            StaggerFreezeFrames = 2,
            EnableSurfaceFriction = true,
            InterruptForceThreshold = 300f
        };
    }
}
