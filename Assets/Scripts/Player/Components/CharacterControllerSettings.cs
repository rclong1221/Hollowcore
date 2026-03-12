using Unity.Entities;
using Unity.Mathematics;

namespace Player.Components
{
    public struct CharacterControllerSettings : IComponentData
    {
        public float Radius;
        public float Height;
        public float SkinWidth;
        public float StepHeight;
        public float MaxSlopeAngleDeg;

        // Movement tuning
        public float WalkSpeed;
        public float RunSpeed;
        public float Acceleration;

        // Rigidbody/push behavior
        public byte PushRigidbodies; // 0 = false, 1 = true
        
        // Slide / resolver tuning (designer-facing)
        // Fraction of velocity retained when sliding into a blocking contact (0..1)
        // e.g. 0.92 means retain 92% (friction = 8%)
        public float SlideBlockedRetention;
        // Fraction of velocity retained when sliding in open space (0..1)
        public float SlideOpenRetention;
        // Extra step-up allowance applied when sliding (meters)
        public float SlideStepExtra;
        // Multiplier for impulse applied to rigidbodies when hit by sliding player
        public float SlideHitImpulseScale;

        public static CharacterControllerSettings Default => new CharacterControllerSettings
        {
            Radius = 0.4f,
            Height = 2.0f,
            SkinWidth = 0.02f,
            StepHeight = 0.3f,
            MaxSlopeAngleDeg = 45f,
            WalkSpeed = 2.0f,
            RunSpeed = 4.0f,
            Acceleration = 20.0f,
            PushRigidbodies = 1,
            SlideBlockedRetention = 0.92f,
            SlideOpenRetention = 0.98f,
            SlideStepExtra = 0.05f,
            SlideHitImpulseScale = 1.2f
        };
    }
}
