using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Generic camera target component that can be attached to any entity (Player, Ship, Spectator).
/// The CameraManager will find this component and move the Unity Camera to match.
/// This is a CLIENT-ONLY component - it does NOT need to be replicated to the server.
/// </summary>
public struct CameraTarget : IComponentData
{
    /// <summary>
    /// World-space position where the camera should be
    /// </summary>
    public float3 Position;

    /// <summary>
    /// World-space rotation of the camera
    /// </summary>
    public quaternion Rotation;

    /// <summary>
    /// Field of view in degrees
    /// </summary>
    public float FOV;

    /// <summary>
    /// Near clipping plane distance
    /// </summary>
    public float NearClip;

    /// <summary>
    /// Far clipping plane distance
    /// </summary>
    public float FarClip;
}

/// <summary>
/// Camera shake parameters for impact effects
/// </summary>
public struct CameraShake : IComponentData
{
    /// <summary>
    /// Current shake intensity (0 = no shake)
    /// </summary>
    public float Amplitude;
    
    /// <summary>
    /// Shake frequency (oscillations per second)
    /// </summary>
    public float Frequency;
    
    /// <summary>
    /// How quickly the shake decays (per second)
    /// </summary>
    public float Decay;
    
    /// <summary>
    /// Internal timer for shake calculation
    /// </summary>
    public float Timer;
}

/// <summary>
/// Camera settings for smooth interpolation
/// </summary>
public struct CameraSettings : IComponentData
{
    /// <summary>
    /// Position interpolation speed (higher = snappier)
    /// </summary>
    public float PositionSmoothing;
    
    /// <summary>
    /// Rotation interpolation speed (higher = snappier)
    /// </summary>
    public float RotationSmoothing;
    
    /// <summary>
    /// FOV transition speed (degrees per second)
    /// </summary>
    public float FOVTransitionSpeed;
}

