using Unity.Entities;
using UnityEngine;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Singleton authoring for global knockback tuning parameters.
    /// Place on a GameObject in a SubScene.
    /// </summary>
    public class KnockbackConfigAuthoring : MonoBehaviour
    {
        [Header("Duration Defaults (seconds)")]
        public float PushDuration = 0.4f;
        public float LaunchDuration = 0.6f;
        public float PullDuration = 0.5f;
        public float StaggerDuration = 0.2f;

        [Header("Force-to-Velocity Conversion")]
        [Tooltip("Velocity = Force / ForceDivisor. Higher = slower knockback")]
        public float ForceDivisor = 100f;
        [Tooltip("Maximum knockback velocity (m/s)")]
        public float MaxVelocity = 25f;
        [Tooltip("Minimum force after resistance to produce knockback")]
        public float MinimumEffectiveForce = 50f;

        [Header("Launch Tuning")]
        [Tooltip("Default vertical ratio for Launch type (0-1)")]
        [Range(0f, 1f)]
        public float DefaultLaunchVerticalRatio = 0.4f;
        [Tooltip("Gravity multiplier for Launch arc. Higher = snappier arcs")]
        public float LaunchGravityMultiplier = 1.5f;

        [Header("Stagger Tuning")]
        [Tooltip("Force multiplier for Stagger type (typically 0.2)")]
        [Range(0f, 1f)]
        public float StaggerForceMultiplier = 0.2f;
        [Tooltip("Freeze frames at stagger start (fixed timesteps)")]
        public int StaggerFreezeFrames = 2;

        [Header("Surface Friction")]
        [Tooltip("Enable surface-material-dependent knockback slide distance")]
        public bool EnableSurfaceFriction = true;

        [Header("Interrupt")]
        [Tooltip("Force threshold above which knockback triggers InterruptRequest")]
        public float InterruptForceThreshold = 300f;

        private class Baker : Baker<KnockbackConfigAuthoring>
        {
            public override void Bake(KnockbackConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new KnockbackConfig
                {
                    PushDuration = authoring.PushDuration,
                    LaunchDuration = authoring.LaunchDuration,
                    PullDuration = authoring.PullDuration,
                    StaggerDuration = authoring.StaggerDuration,
                    ForceDivisor = authoring.ForceDivisor,
                    MaxVelocity = authoring.MaxVelocity,
                    MinimumEffectiveForce = authoring.MinimumEffectiveForce,
                    DefaultLaunchVerticalRatio = authoring.DefaultLaunchVerticalRatio,
                    LaunchGravityMultiplier = authoring.LaunchGravityMultiplier,
                    StaggerForceMultiplier = authoring.StaggerForceMultiplier,
                    StaggerFreezeFrames = authoring.StaggerFreezeFrames,
                    EnableSurfaceFriction = authoring.EnableSurfaceFriction,
                    InterruptForceThreshold = authoring.InterruptForceThreshold
                });
            }
        }
    }
}
