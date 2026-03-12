using Unity.Entities;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 8: Singleton for runtime toggling of surface gameplay features.
    /// Useful for difficulty settings, game modes, and debugging.
    /// If absent, all systems fall back to enabled (default behavior).
    /// </summary>
    public struct SurfaceGameplayToggles : IComponentData
    {
        public bool EnableMovementModifiers;
        public bool EnableStealthModifiers;
        public bool EnableSlipPhysics;
        public bool EnableFallDamageModifiers;
        public bool EnableSurfaceDamageZones;

        public static SurfaceGameplayToggles AllEnabled => new()
        {
            EnableMovementModifiers = true,
            EnableStealthModifiers = true,
            EnableSlipPhysics = true,
            EnableFallDamageModifiers = true,
            EnableSurfaceDamageZones = true
        };
    }
}
