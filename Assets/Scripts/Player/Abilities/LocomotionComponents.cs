using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using System.Runtime.InteropServices;

namespace DIG.Player.Abilities
{
    // --- Jump ---
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct JumpAbility : IComponentData, IEnableableComponent
    {
        [GhostField] public bool IsActive;
        [GhostField] public bool JumpPressed;
        [GhostField] public bool IsJumping; // State flag
        public float VerticalVelocity;
        public float TimeSinceJump;
        public double LastGroundedTime; // For Coyote Time (using double for system time)
        public int JumpsRemaining;
        
        // 13.13.7 Hold-For-Height
        public float HoldForce; // Accumulator for held jump force
        
        // 13.13.8 Airborne Jumps
        [GhostField] public int AirborneJumpsUsed; // Reset on ground
        
        // 13.13.9 Recurrence Delay
        public double LastLandTime; // Time when last landed
        
        // 13.13.4 Multi-Frame Force
        public int FramesRemaining;
    
        // 13.13.5 Animation Events
        public bool IsWaitingForEvent;
    
        // 13.13.6 Presentation Flag
        [GhostField] public bool JustJumped; // One-shot flag for presentation

        // Track previous frame's jump input for detecting fresh presses
        [GhostField] public bool WasJumpPressed;

        // 13.14.P8: Removed unused LastAirborneJumpFrame
    }

    // 13.13.5 Animation Event Communication
    public struct JumpEventTrigger : IComponentData 
    { 
        public bool Triggered; 
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct JumpSettings : IComponentData
    {
        public float JumpForce;
        public int MaxJumps;
        public float GravityMultiplier; // For variable jump height (holding button)
        
        // 13.13.1 Ceiling Check
        public float MinCeilingJumpHeight; // 0.05f default, -1 to disable
        
        // 13.13.2 Slope Limit
        [MarshalAs(UnmanagedType.U1)]
        public bool PreventSlopeLimitJump; // true default
        public float SlopeLimit; // 45f degrees
        
        // 13.13.3 Directional Multipliers
        public float SidewaysForceMultiplier;  // 0.8f default
        public float BackwardsForceMultiplier; // 0.7f default
        
        // 13.13.6 Coyote Time (configurable instead of hardcoded)
        public float CoyoteTime; // 0.15f default
        
        // 13.13.7 Hold-For-Height
        public float ForceHold;        // 0.003f default
        public float ForceDampingHold; // 0.5f default
        
        // 13.13.8 Airborne Jumps
        public int MaxAirborneJumps;     // 1 for double jump, 0 to disable
        public float AirborneJumpForce;  // 0.6f multiplier
        
        // 13.13.9 Recurrence Delay
        public float RecurrenceDelay; // 0.2f seconds
        
        // 13.13.4 Multi-Frame Force
        public int JumpFrames;       // 1 = instant, >1 = distributed
        public float ForceDamping;   // Damping per frame
    
        // 13.13.5 Animation Events
        [MarshalAs(UnmanagedType.U1)] public bool WaitForAnimationEvent; // true => wait for OnAnimatorJump
        public float JumpEventTimeout;     // 0.15f fallback
    
        // 13.13.6 Surface Impact
        [MarshalAs(UnmanagedType.U1)] public bool SpawnSurfaceEffect;    // true
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CrouchAbility : IComponentData
    {
        [GhostField] public bool IsCrouching;
        [GhostField] public bool CrouchPressed;
        public float CurrentHeight;
        public float OriginalHeight;
        public float OriginalRadius;
        public float3 OriginalCenter;
        
        // 13.15.1: Standup Obstruction
        [GhostField] public bool StandupBlocked; // True when ceiling prevents standing
        
        // 13.15.O5: Dirty Stance Flag
        // Set to true when stance changes, cleared after collider update
        public bool StanceDirty;
        public PlayerStance LastProcessedStance; // Track last stance to detect changes
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CrouchSettings : IComponentData
    {
        public float CrouchHeight;
        public float CrouchRadius;
        public float3 CrouchCenter;
        public float TransitionSpeed;
        public float MovementSpeedMultiplier;
        
        // 13.15.1: Height management
        public float StandingHeight;  // Original capsule height
        public float ColliderSpacing; // Skin width for overlap check (0.02f)
        
        // 13.15.4: Speed change control
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)]
        public bool AllowSpeedChange; // If false, block sprint while crouched
    }

    // --- Sprint ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct SprintAbility : IComponentData
    {
        [GhostField] public bool IsSprinting;
        [GhostField] public bool SprintPressed;
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct SprintSettings : IComponentData
    {
        public float SpeedMultiplier;
    }

    // --- Root Motion ---
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct RootMotionDelta : IComponentData
    {
        [GhostField] public float3 PositionDelta;
        [GhostField] public quaternion RotationDelta;
        [GhostField] public bool UseRootMotion;
    }
}
