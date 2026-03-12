using Unity.Entities;

namespace Player.Components
{
    // Settings for fall damage calculations
    public struct FallDamageSettings : IComponentData
    {
        public float SafeFallHeight; // meters without damage
        public float MaxSafeFallHeight; // meters before serious damage
        public float LethalFallHeight; // meters that will kill
        public float DamagePerMeter; // damage per meter above SafeFallHeight
        // Camera shake parameters applied on landing
        public float ShakeAmplitude; // world-space offset amplitude
        public float ShakeFrequency; // oscillations per second
        public float ShakeDecay; // amplitude decay per second
        // How long landing flag should remain for adapters to read (seconds)
        public float LandingFlagDuration;
    }
}
