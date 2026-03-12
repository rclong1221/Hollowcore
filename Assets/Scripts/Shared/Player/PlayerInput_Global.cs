using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

/// <summary>
/// Global PlayerInput used by systems and generated code (placed in global namespace intentionally)
/// Mirrors the NetCode-compatible input struct used for prediction/rollback.
/// Updated to trigger recompile.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PlayerInput : IInputComponentData
{
    // ===== MOVEMENT =====
    public int Horizontal;
    public int Vertical;
    public InputEvent Jump;
    public InputEvent Crouch;
    public InputEvent Sprint;
    public InputEvent Prone;
    public InputEvent Slide;
    public InputEvent DodgeRoll;
    public InputEvent DodgeDive;

    // ===== CAMERA =====
    public float2 LookDelta;
    public float ZoomDelta;
    public float CameraYaw;      // Degrees, sent from client so server can move relative to camera
    public float CameraPitch;    // Degrees, sent from client so server can aim up/down
    public float CameraDistance; // Current orbit distance, for spectator replication
    public byte CameraYawValid;  // 0 = invalid/unset, 1 = valid value in CameraYaw

    // ===== INTERACTION =====
    public InputEvent Interact;
    public InputEvent Use;
    public InputEvent AltUse;
    public InputEvent Grab;
    public InputEvent FreeLook;

    // ===== LOCK-ON (sent from client for server to apply lock-on rotation) =====
    /// <summary>Position of lock-on target. Server uses this to rotate player toward target.</summary>
    public float3 LockTargetPosition;
    /// <summary>0 = not locked, 1 = locked on target</summary>
    public byte IsLockedOn;

    // ===== TOOLS =====
    // NOTE: ToolSlotDelta was removed. All equipment input now goes through
    // EquipSlotId/EquipQuickSlot which is set by DIGEquipmentProvider respecting slot definitions.
    
    // ===== EQUIPMENT (slot-agnostic, set by DIGEquipmentProvider respecting slot definitions) =====
    /// <summary>Slot index for equip request (-1 = none, 0 = MainHand, 1 = OffHand, etc)</summary>
    public int EquipSlotId;
    /// <summary>Quick slot number for equip request (1-9, 0 = none)</summary>
    public int EquipQuickSlot;
    
    public InputEvent Reload;
    public InputEvent ToggleFlashlight;
    // ===== LEAN =====
    public InputEvent LeanLeft;
    public InputEvent LeanRight;

    // ===== COMBAT =====
    public InputEvent Tackle;

    // ===== ABILITIES (EPIC 18.19) =====
    /// <summary>Ability slot request: 0-5 = slot index, 255 = no request.</summary>
    public byte AbilitySlotRequest;

    // ===== CURSOR AIM (EPIC 18.19 Phase 8) =====
    /// <summary>
    /// Client-computed aim direction from cursor raycast (CursorAim targeting mode).
    /// Replicated to server so WeaponFireSystem can fire in the correct direction.
    /// </summary>
    public float3 CursorAimDirection;
    /// <summary>0 = not in CursorAim mode, 1 = valid CursorAimDirection</summary>
    public byte CursorAimValid;

    // ===== EVA =====
    public InputEvent ToggleMagneticBoots;

    // ===== MMO AUTO-RUN (EPIC 15.21) =====
    /// <summary>MMO auto-run: LMB+RMB held in MMO mode triggers forward movement</summary>
    public InputEvent AutoRun;
    
    // ===== MMO/Core STRAFE (EPIC 15.21) =====
    public InputEvent MMOStrafeLeft;
    public InputEvent MMOStrafeRight;

    // ===== CLICK-TO-MOVE PATHFINDING (EPIC 15.20 Phase 3) =====
    /// <summary>Continuous path-following movement X (camera-right). Not quantized like Horizontal.</summary>
    public float PathMoveX;
    /// <summary>Continuous path-following movement Y (camera-forward). Not quantized like Vertical.</summary>
    public float PathMoveY;
    /// <summary>0 = normal WASD input, 1 = path following active. Movement systems use PathMoveX/Y instead of Horizontal/Vertical.</summary>
    public byte IsPathFollowing;

    // ===== SOCKET POSITIONS (for accurate throwable spawn) =====
    /// <summary>
    /// World position of the main hand socket (from animated skeleton).
    /// Used by server for accurate throwable spawn position.
    /// </summary>
    public float3 MainHandPosition;
    /// <summary>0 = invalid/not available, 1 = valid position</summary>
    public byte MainHandPositionValid;

    // ===== UTILITY =====
    public uint FrameCount;
}

/// <summary>
/// InputEvent definition in global namespace to match usage by generated code
/// Uses byte backing to remain blittable for NetCode.
/// </summary>
public struct InputEvent
{
    public byte IsSetByte;
    public uint FrameCount;

    public bool IsSet
    {
        get => IsSetByte != 0;
        set => IsSetByte = (byte)(value ? 1 : 0);
    }

    public static InputEvent Pressed => new InputEvent { IsSetByte = 1, FrameCount = 1 };
    public static InputEvent Released => new InputEvent { IsSetByte = 0, FrameCount = 0 };
}
