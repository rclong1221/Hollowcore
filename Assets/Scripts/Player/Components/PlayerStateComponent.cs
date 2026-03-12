using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;

/// <summary>
/// Player mode - high-level state of what the player is doing
/// </summary>
public enum PlayerMode : byte
{
    EVA = 0,        // Extra-Vehicular Activity (on foot)
    InShip = 1,     // Inside ship but not piloting
    Piloting = 2,   // Actively piloting the ship
    Dead = 3,       // Player is dead
    Spectating = 4  // Spectating other players
}

/// <summary>
/// Player stance - physical posture
/// </summary>
public enum PlayerStance : byte
{
    Standing = 0,   // Normal standing (2m height)
    Crouching = 1,  // Crouched (1m height)
    Prone = 2       // Lying down (0.5m height)
}

/// <summary>
/// Player movement state - what type of movement is happening
/// </summary>
public enum PlayerMovementState : byte
{
    Idle = 0,       // Not moving
    Walking = 1,    // Slow movement
    Running = 2,    // Normal movement speed
    Sprinting = 3,  // Fast movement (stamina drain)
    Jumping = 4,    // In air from jump
    Falling = 5,    // In air from falling
    Climbing = 6,   // Climbing ladder/surface
    Swimming = 7,   // In water
    Rolling = 8,    // Dodge rolling
    Diving = 9,     // Dodge diving
    Sliding = 10,   // Sliding on ground
    Staggered = 11, // Collision stagger (7.3.8) - brief loss of control
    Knockdown = 12, // Extreme stagger (7.4.1) - on ground, must recover
    Tackling = 13   // Intentional tackle (7.4.2) - committed forward lunge
}

/// <summary>
/// Main player state component - tracks mode, stance, and movement state
/// Replicated across network for multiplayer
/// NOTE: Using GhostPrefabType.All to replicate to both predicted AND interpolated ghosts
/// so that remote clients can see stance changes (crouch/prone).
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct PlayerState : IComponentData
{
    
    /// <summary>High-level player mode (EVA, InShip, etc.)</summary>
    [GhostField] public PlayerMode Mode;
    
    /// <summary>Physical stance (Standing, Crouching, Prone)</summary>
    [GhostField] public PlayerStance Stance;
    
    /// <summary>Current movement state (Idle, Walking, Running, etc.)</summary>
    [GhostField] public PlayerMovementState MovementState;

    /// <summary>Is the player touching the ground?</summary>
    [GhostField] public bool IsGrounded;

    /// <summary>Was the player grounded last frame? (For landing detection)</summary>
    [GhostField] public bool WasGrounded;

    /// <summary>Distance to check for ground (raycast length)</summary>
    public float GroundCheckDistance;
    
    /// <summary>Most recent ground contact height (world Y)</summary>
    [GhostField] public float GroundHeight;
    
    /// <summary>Most recent ground contact normal (world space)</summary>
    [GhostField] public float3 GroundNormal;
    
    /// <summary>Time when stance was last changed (for cooldown)</summary>
    public float LastStanceChangeTime;
    
    /// <summary>Cooldown between stance changes (prevents spam)</summary>
    public float StanceChangeCooldown;
    
    /// <summary>Current height of the player collider based on stance</summary>
    [GhostField] public float CurrentHeight;
    
    /// <summary>Target height for smooth stance transitions</summary>
    [GhostField] public float TargetHeight;
    
    /// <summary>Speed of height interpolation during stance changes</summary>
    public float HeightTransitionSpeed;
    
    /// <summary>Crouch is currently toggled on (for toggle mode)</summary>
    [GhostField] public bool CrouchToggled;
    
    /// <summary>Prone is currently toggled on (for toggle mode)</summary>
    [GhostField] public bool ProneToggled;
    
    /// <summary>Last frame's crouch input state (for detecting press/release)</summary>
    public byte LastCrouchInput;
    
    /// <summary>Last frame's prone input state (for detecting press/release)</summary>
    public byte LastProneInput;
    
    public static PlayerState Default => new PlayerState
    {
        Mode = PlayerMode.EVA,
        Stance = PlayerStance.Standing,
        MovementState = PlayerMovementState.Idle,
        IsGrounded = false,
        GroundCheckDistance = 0.1f,
        GroundHeight = 0f,
        GroundNormal = new float3(0f, 1f, 0f),
        LastStanceChangeTime = 0f,
        StanceChangeCooldown = 0.3f, // 300ms cooldown
        CurrentHeight = 2.0f,
        TargetHeight = 2.0f,
        HeightTransitionSpeed = 8.0f,
        CrouchToggled = false,
        ProneToggled = false,
        LastCrouchInput = 0,
        LastProneInput = 0
    };
}

/// <summary>
/// Configuration for stance heights and speeds
/// </summary>
public struct PlayerStanceConfig : IComponentData
{
    public float StandingHeight;
    public float CrouchingHeight;
    public float ProneHeight;
    
    public float StandingSpeed;
    public float CrouchingSpeed;
    public float ProneSpeed;
    
    public static PlayerStanceConfig Default => new PlayerStanceConfig
    {
        StandingHeight = 2.0f,
        CrouchingHeight = 1.0f,
        ProneHeight = 0.5f,
        StandingSpeed = 4.0f,
        CrouchingSpeed = 2.0f,
        ProneSpeed = 1.0f
    };
}

