using Unity.Entities;

namespace DIG.ProceduralMotion
{
    /// <summary>
    /// EPIC 15.25 Phase 1: ECS singleton for motion intensity settings.
    /// Provides Burst-safe access to global intensity values.
    /// Bridges from the managed MotionIntensitySettings MonoBehaviour (EPIC 15.24).
    /// Named differently to avoid collision with the MonoBehaviour class.
    /// </summary>
    public struct ProceduralMotionIntensity : IComponentData
    {
        /// <summary>Master scale (0=disabled, 1=normal, 2=exaggerated).</summary>
        public float GlobalIntensity;

        /// <summary>Camera procedural force scale.</summary>
        public float CameraMotionScale;

        /// <summary>Weapon procedural force scale.</summary>
        public float WeaponMotionScale;

        public static ProceduralMotionIntensity Default => new ProceduralMotionIntensity
        {
            GlobalIntensity = 1f,
            CameraMotionScale = 1f,
            WeaponMotionScale = 1f
        };
    }
}
