using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

/// <summary>
/// Player camera settings and state for orbit camera with FPS zoom.
/// Yaw, Pitch, and CurrentDistance are ghost-replicated to non-owners so
/// spectators can see the watched player's camera perspective.
/// The owning client keeps local control — SendToNonOwner prevents server override.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.All, OwnerSendType = SendToOwnerType.SendToNonOwner)]
public struct PlayerCameraSettings : IComponentData
{
    // ===== ORBIT DISTANCE =====
    /// <summary>Current camera distance from player</summary>
    [GhostField]
    public float CurrentDistance;

    /// <summary>Target camera distance (for smooth interpolation)</summary>
    public float TargetDistance;

    /// <summary>Minimum camera distance (0 = FPS mode)</summary>
    public float MinDistance;

    /// <summary>Maximum camera distance</summary>
    public float MaxDistance;

    /// <summary>Zoom speed (m/s)</summary>
    public float ZoomSpeed;

    // ===== ROTATION =====
    /// <summary>Current pitch angle (degrees, vertical rotation)</summary>
    [GhostField]
    public float Pitch;

    /// <summary>Current yaw angle (degrees, horizontal rotation)</summary>
    [GhostField]
    public float Yaw;

    /// <summary>Mouse look sensitivity</summary>
    public float LookSensitivity;

    /// <summary>Minimum pitch angle (degrees)</summary>
    public float MinPitch;

    /// <summary>Maximum pitch angle (degrees)</summary>
    public float MaxPitch;

    // ===== OFFSETS =====
    /// <summary>Pivot offset from player origin (where camera orbits around)</summary>
    public float3 PivotOffset;

    /// <summary>FPS camera offset (eye position when distance = 0)</summary>
    public float3 FPSOffset;

    // ===== FIELD OF VIEW =====
    /// <summary>Base field of view in degrees (unmultiplied)</summary>
    public float BaseFOV;

    /// <summary>Default settings for player camera (MMORPG-style like WoW)</summary>
    public static PlayerCameraSettings Default => new PlayerCameraSettings
    {
        // Orbit distance - WoW-style: further back for better situational awareness
        CurrentDistance = 8.0f,
        TargetDistance = 8.0f,
        MinDistance = 0.0f,
        MaxDistance = 15.0f,
        ZoomSpeed = 5.0f, // Per scroll wheel click

        // Rotation - WoW-style: higher angle looking down at character
        Pitch = 25.0f, // Look down at ~25 degrees (WoW default is around 20-30)
        Yaw = 0.0f,
        LookSensitivity = 0.10f, // Mouse sensitivity (degrees per pixel)
        MinPitch = -89.0f,
        MaxPitch = 89.0f,

        // Offsets
        PivotOffset = new float3(0, 1.6f, 0), // At head height
        FPSOffset = new float3(0, 1.7f, 0),   // Eye position

        // Field of view
        BaseFOV = 60.0f
    };
}
