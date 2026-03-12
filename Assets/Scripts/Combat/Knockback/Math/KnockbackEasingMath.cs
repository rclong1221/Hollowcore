using Unity.Mathematics;
using DIG.Surface;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Burst-compatible static math utilities for knockback easing and falloff.
    /// </summary>
    public static class KnockbackEasingMath
    {
        /// <summary>
        /// Evaluate easing curve at given progress (0-1).
        /// Returns a factor (0-1) to multiply against initial velocity.
        /// At progress=0 returns ~1 (full speed), at progress=1 returns ~0 (stopped).
        /// </summary>
        public static float EvaluateEasing(float progress, KnockbackEasing easing)
        {
            float t = math.saturate(progress);
            switch (easing)
            {
                case KnockbackEasing.Linear:
                    return 1f - t;
                case KnockbackEasing.EaseOut:
                    return 1f - (t * t);
                case KnockbackEasing.Bounce:
                    return EvaluateBounce(t);
                case KnockbackEasing.Sharp:
                    return (1f - t) * (1f - t) * (1f - t);
                default:
                    return 1f - (t * t);
            }
        }

        /// <summary>
        /// Primary deceleration (0-0.7), then small bounce (0.7-1.0).
        /// </summary>
        public static float EvaluateBounce(float t)
        {
            if (t < 0.7f)
                return 1f - (t / 0.7f) * (t / 0.7f);
            float bt = (t - 0.7f) / 0.3f;
            return 0.15f * math.sin(bt * math.PI);
        }

        /// <summary>
        /// Compute distance-based force falloff multiplier (0-1).
        /// Returns 1 at distance=0, approaches 0 at distance=maxRadius.
        /// </summary>
        public static float ComputeFalloff(float distance, float maxRadius, KnockbackFalloff falloff)
        {
            if (falloff == KnockbackFalloff.None || maxRadius <= 0f)
                return 1f;

            float ratio = math.saturate(distance / maxRadius);
            switch (falloff)
            {
                case KnockbackFalloff.Linear:
                    return 1f - ratio;
                case KnockbackFalloff.Quadratic:
                    return 1f - (ratio * ratio);
                case KnockbackFalloff.Cubic:
                    return 1f - (ratio * ratio * ratio);
                default:
                    return 1f - ratio;
            }
        }

        /// <summary>
        /// Get surface friction multiplier from SurfaceID.
        /// Values: Ice=1.8, Snow=1.3, Water=1.2, Metal=1.1, Concrete=0.9, Wood=0.95,
        /// Dirt=0.85, Grass=0.8, Gravel=0.7, Sand=0.6, Mud=0.5, Rubber=0.3.
        /// Default=1.0 for unknown surfaces.
        /// </summary>
        public static float GetSurfaceFriction(SurfaceID surfaceId)
        {
            switch (surfaceId)
            {
                case SurfaceID.Ice: return 1.8f;
                case SurfaceID.Snow: return 1.3f;
                case SurfaceID.Water: return 1.2f;
                case SurfaceID.Metal_Thin: return 1.1f;
                case SurfaceID.Metal_Thick: return 1.05f;
                case SurfaceID.Concrete: return 0.9f;
                case SurfaceID.Wood: return 0.95f;
                case SurfaceID.Dirt: return 0.85f;
                case SurfaceID.Grass: return 0.8f;
                case SurfaceID.Gravel: return 0.7f;
                case SurfaceID.Sand: return 0.6f;
                case SurfaceID.Mud: return 0.5f;
                default: return 1.0f;
            }
        }
    }
}
