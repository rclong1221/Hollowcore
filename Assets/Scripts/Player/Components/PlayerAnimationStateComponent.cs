using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

/// <summary>
/// Lightweight animation parameters that are predicted/replicated with the player ghost.
/// Systems derive these values from gameplay state and the client presentation layer
/// feeds them into the Animator so visuals stay in sync with netcode.
/// NOTE: Using GhostPrefabType.All to replicate to interpolated ghosts on remote clients.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct PlayerAnimationState : IComponentData
{
    /// <summary>Normalized movement input (local space X/Z).</summary>
    [GhostField] public float2 MoveInput;
    /// <summary>World-space planar velocity magnitude.</summary>
    [GhostField] public float MoveSpeed;
    /// <summary>Vertical velocity (positive = up, negative = down).</summary>
    [GhostField] public float VerticalSpeed;
    /// <summary>Lean amount (-1 = left, 1 = right).</summary>
    [GhostField] public float Lean;
    /// <summary>Cached movement state to pick blend trees.</summary>
    [GhostField] public PlayerMovementState MovementState;
    /// <summary>True when controller is grounded.</summary>
    [GhostField] public bool IsGrounded;
    /// <summary>True when player is jumping (not just falling).</summary>
    [GhostField] public bool IsJumping;
    /// <summary>True when stance is crouch/prone.</summary>
    [GhostField] public bool IsCrouching;
    /// <summary>True when explicitly prone (networked/predicted).</summary>
    [GhostField] public bool IsProne;
    /// <summary>True when sprint input is pressed.</summary>
    [GhostField] public bool IsSprinting;
    /// <summary>True when crouch+sprint+movement (simple slide heuristic).</summary>
    [GhostField] public bool IsSliding;
    /// <summary>True when climbing.</summary>
    [GhostField] public bool IsClimbing;
    /// <summary>Climb progress (0-1).</summary>
    [GhostField] public float ClimbProgress;

    // --- 13.14.4: Fall Blend Tree Data ---
    /// <summary>True when actively in fall ability (not just airborne).</summary>
    [GhostField] public bool IsFalling;
    /// <summary>
    /// Vertical velocity for fall blend tree (Y velocity in local space).
    /// Negative = falling down, Positive = rising up.
    /// Used by animator blend trees to transition between fall start/loop/end.
    /// </summary>
    [GhostField] public float FallVelocity;
    /// <summary>
    /// Fall state index for animator state machine.
    /// 0 = falling, 1 = landed (mirrors Opsive AbilityIntData).
    /// </summary>
    [GhostField] public int FallStateIndex;
    
    // --- 13.15.3: Height Animator Parameter ---
    /// <summary>
    /// Height state for animator blend tree.
    /// 0 = Standing, 1 = Crouching, 2 = Prone.
    /// </summary>
    [GhostField] public int Height;

    /// <summary>True when in swimming mode.</summary>
    [GhostField] public bool IsSwimming;
    /// <summary>True when head is underwater (submerged).</summary>
    [GhostField] public bool IsUnderwater;
    /// <summary>
    /// Swimming action state for animation blend tree:
    /// 0 = Not swimming, 1 = Surface swimming, 2 = Underwater, 3 = Swimming down, 4 = Swimming up
    /// </summary>
    [GhostField] public int SwimActionState;
    /// <summary>Swim input magnitude for animation blending (0 = idle, 1 = full speed).</summary>
    [GhostField] public float SwimInputMagnitude;

    // --- 13.26: Opsive Ability Parameters ---
    /// <summary>
    /// Currently active ability index (0 = none, 503 = FreeClimb, etc.).
    /// See OpsiveAnimatorConstants for values.
    /// </summary>
    [GhostField] public int AbilityIndex;
    
    /// <summary>
    /// Sub-state within the active ability (e.g., 0=BottomMount, 2=Climbing, 6=TopDismount).
    /// See OpsiveAnimatorConstants.CLIMB_* for FreeClimb values.
    /// </summary>
    [GhostField] public int AbilityIntData;
    
    /// <summary>
    /// Float data for ability (e.g., movement direction -1 to 1 for climb blend trees).
    /// </summary>
    [GhostField] public float AbilityFloatData;
    
    /// <summary>
    /// True when ability has just changed, used to trigger AbilityChange in Animator.
    /// Reset to false after one frame.
    /// </summary>
    [GhostField] public bool AbilityChange;
    
    /// <summary>
    /// Yaw (turn) value for tank turn animations.
    /// -1 = turning left, 0 = no turn, 1 = turning right.
    /// Used by animator blend trees for turn-in-place animations.
    /// EPIC 15.20
    /// </summary>
    [GhostField] public float Yaw;

    public static PlayerAnimationState Default => new PlayerAnimationState
    {
        MoveInput = float2.zero,
        MoveSpeed = 0f,
        VerticalSpeed = 0f,
        Lean = 0f,
        MovementState = PlayerMovementState.Idle,
        IsGrounded = true,
        IsJumping = false,
        IsCrouching = false,
        IsProne = false,
        IsSprinting = false,
        IsSliding = false,
        IsClimbing = false,
        ClimbProgress = 0f,
        // 13.14.4: Fall blend tree defaults
        IsFalling = false,
        FallVelocity = 0f,
        FallStateIndex = 0,
        IsSwimming = false,
        IsUnderwater = false,
        SwimActionState = 0,
        SwimInputMagnitude = 0f,
        // 13.15.3: Height default
        Height = 0,
        // 13.26: Opsive ability defaults
        AbilityIndex = 0,
        AbilityIntData = 0,
        AbilityFloatData = 0f,
        AbilityChange = false,
        // EPIC 15.20: Tank turn animation
        Yaw = 0f,
    };
}
