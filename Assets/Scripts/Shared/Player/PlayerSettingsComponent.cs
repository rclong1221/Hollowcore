using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

/// <summary>
/// Player movement settings - speeds, acceleration, jump force, etc.
/// </summary>
public struct PlayerMovementSettings : IComponentData
{
    // ===== SPEED SETTINGS =====
    /// <summary>Walking speed (m/s) - slow movement</summary>
    public float WalkSpeed;
    
    /// <summary>Running speed (m/s) - normal movement</summary>
    public float RunSpeed;
    
    /// <summary>Sprinting speed (m/s) - fast movement with stamina drain</summary>
    public float SprintSpeed;
    
    /// <summary>Crouching speed (m/s)</summary>
    public float CrouchSpeed;
    
    /// <summary>Prone speed (m/s)</summary>
    public float ProneSpeed;
    
    // ===== ACCELERATION SETTINGS =====
    /// <summary>Ground acceleration (m/s²) - how fast player reaches max speed</summary>
    public float GroundAcceleration;
    
    /// <summary>Air acceleration (m/s²) - reduced control while in air</summary>
    public float AirAcceleration;
    
    /// <summary>Ground friction coefficient - how fast player stops</summary>
    public float Friction;

    /// <summary>Character rotation speed (deg/s)</summary>
    public float TurnSpeed;
    
    // ===== JUMP SETTINGS =====
    /// <summary>Initial jump velocity (m/s)</summary>
    public float JumpForce;
    
    /// <summary>Gravity acceleration (m/s²)</summary>
    public float Gravity;
    
    /// <summary>Maximum falling speed (m/s) - terminal velocity</summary>
    public float MaxFallSpeed;
    
    /// <summary>Coyote time (seconds) - grace period after leaving ground to still jump</summary>
    public float CoyoteTime;
    
    /// <summary>Jump buffer time (seconds) - grace period to buffer jump input before landing</summary>
    public float JumpBufferTime;
    
    public static PlayerMovementSettings Default => new PlayerMovementSettings
    {
        // Speeds (m/s) - realistic human locomotion
        WalkSpeed = 1.4f,        // 5 km/h - casual walking
        RunSpeed = 3.0f,         // 10.8 km/h - jogging pace
        SprintSpeed = 5.5f,      // 19.8 km/h - fast sprint (non-athlete)
        CrouchSpeed = 1.0f,      // 3.6 km/h - crouched movement
        ProneSpeed = 0.5f,       // 1.8 km/h - crawling
        
        // Acceleration
        GroundAcceleration = 15.0f,  // Slightly reduced for more weight
        AirAcceleration = 1.5f,
        Friction = 12.0f,            // Increased friction for more grounded feel
        TurnSpeed = 180.0f,           // Default rotation speed
        
        // Jump (realistic ~0.5m vertical jump height for average person)
        JumpForce = 3.2f,        // Initial upward velocity
        Gravity = -9.81f,        // Earth gravity
        MaxFallSpeed = -20.0f,
        CoyoteTime = 0.1f,
        JumpBufferTime = 0.1f
    };
}

/// <summary>
/// Player input preferences - toggle vs hold for crouch/prone
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PlayerInputPreferences : IComponentData
{
    /// <summary>If true, crouch toggles on/off. If false, must hold key</summary>
    [GhostField] public bool CrouchToggle;
    
    /// <summary>If true, prone toggles on/off. If false, must hold key</summary>
    [GhostField] public bool ProneToggle;
    
    public static PlayerInputPreferences Default => new PlayerInputPreferences
    {
        CrouchToggle = false,  // Hold by default
        ProneToggle = false    // Hold by default
    };
}

/// <summary>
/// Player stamina - drains when sprinting, regenerates when not
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PlayerStamina : IComponentData
{
    /// <summary>Current stamina amount</summary>
    public float Current;
    
    /// <summary>Maximum stamina capacity</summary>
    public float Max;
    
    /// <summary>Stamina drain rate when sprinting (units/sec)</summary>
    public float DrainRate;
    
    /// <summary>Stamina regeneration rate when not sprinting (units/sec)</summary>
    public float RegenRate;
    
    /// <summary>Delay before stamina starts regenerating after sprinting (seconds)</summary>
    public float RegenDelay;
    
    /// <summary>Time when stamina was last drained</summary>
    public float LastDrainTime;
    
    /// <summary>Is stamina depleted?</summary>
    public bool IsDepleted => Current <= 0;
    
    /// <summary>Can sprint? (has stamina and not depleted)</summary>
    public bool CanSprint => Current > 0;
    
    public static PlayerStamina Default => new PlayerStamina
    {
        Current = 100f,
        Max = 100f,
        DrainRate = 20f,  // Drains in 5 seconds
        RegenRate = 10f,  // Regenerates in 10 seconds
        RegenDelay = 1.0f,
        LastDrainTime = 0f
    };
}

/// <summary>
/// Player jump state - tracks coyote time and jump buffering
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PlayerJumpState : IComponentData
{
    /// <summary>Time when player left the ground (for coyote time)</summary>
    public float TimeLeftGround;
    
    /// <summary>Time when jump was requested (for jump buffering)</summary>
    public float TimeJumpRequested;

    /// <summary>Has used the coyote time jump?</summary>
    public bool UsedCoyoteJump;

    /// <summary>Can coyote jump? (within coyote time window and hasn't used it)</summary>
    public bool CanCoyoteJump(float currentTime, float coyoteTime)
    {
        return !UsedCoyoteJump && (currentTime - TimeLeftGround) <= coyoteTime;
    }
    
    /// <summary>Is jump buffered? (jump requested within buffer window)</summary>
    public bool IsJumpBuffered(float currentTime, float bufferTime)
    {
        return TimeJumpRequested > 0 && (currentTime - TimeJumpRequested) <= bufferTime;
    }
}

