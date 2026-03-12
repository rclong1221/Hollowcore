namespace DIG.ProceduralMotion
{
    /// <summary>
    /// EPIC 15.25 Phase 1: Motion states for procedural animation.
    /// Maps from PlayerMovementState + WeaponAimState to determine
    /// which spring overrides and force scales to apply.
    /// </summary>
    public enum MotionState : byte
    {
        Idle = 0,
        Walk = 1,
        Sprint = 2,
        ADS = 3,
        Slide = 4,
        Vault = 5,
        Swim = 6,
        Airborne = 7,
        Crouch = 8,
        Climb = 9,
        Staggered = 10
    }
}
