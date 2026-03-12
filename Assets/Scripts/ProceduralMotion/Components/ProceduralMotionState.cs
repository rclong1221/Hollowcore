using Unity.Entities;
using Unity.Mathematics;

namespace DIG.ProceduralMotion
{
    /// <summary>
    /// EPIC 15.25 Phase 1: Runtime state for the procedural motion state machine.
    /// Tracks current motion state, transition progress, and cached paradigm weights.
    /// Client-only — NOT ghost-replicated.
    /// </summary>
    public struct ProceduralMotionState : IComponentData
    {
        // State machine
        public MotionState CurrentState;
        public MotionState PreviousState;
        public float StateBlendT;          // 0 to 1 transition progress
        public float StateTransitionSpeed;  // 1 / transitionDuration

        // Tracking for force providers
        public float3 PreviousVelocity;    // Last frame velocity (inertia)
        public float2 SmoothedLookDelta;   // EMA-filtered mouse input (sway + HUD)
        public float BobPhase;             // Lissajous phase accumulator
        public float TimeSinceLanding;     // Seconds since last ground contact
        public float LandingImpactSpeed;   // Vertical speed at moment of landing
        public float IdleNoiseTime;        // Perlin noise time accumulator
        public float WallTuckT;            // Wall tuck interpolation 0 to 1
        public bool WasGrounded;           // Previous frame grounded state

        // Cached paradigm weights (updated on paradigm change)
        public float FPMotionWeight;       // First-person weapon motion
        public float CameraMotionWeight;   // Camera procedural forces
        public float WeaponMotionWeight;   // Weapon visual motion
        public float HitReactionWeight;    // Hit reaction camera force
        public float BobWeight;            // Bob force weight
        public float SwayWeight;           // Sway force weight
    }
}
