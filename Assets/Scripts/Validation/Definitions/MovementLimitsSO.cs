using UnityEngine;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Movement limits configuration for speed hack detection.
    /// Place in Resources/MovementLimits.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Validation/Movement Limits")]
    public class MovementLimitsSO : ScriptableObject
    {
        [Header("Max Speed Per State (m/s)")]
        [Tooltip("Standing walk speed.")]
        [Min(0.1f)] public float MaxSpeedStanding = 5f;

        [Tooltip("Sprint speed.")]
        [Min(0.1f)] public float MaxSpeedSprinting = 10f;

        [Tooltip("Crouch speed.")]
        [Min(0.1f)] public float MaxSpeedCrouching = 2.5f;

        [Tooltip("Prone speed.")]
        [Min(0.1f)] public float MaxSpeedProne = 1f;

        [Tooltip("Swimming speed.")]
        [Min(0.1f)] public float MaxSpeedSwimming = 4f;

        [Tooltip("Terminal velocity (generous for physics).")]
        [Min(1f)] public float MaxSpeedFalling = 50f;

        [Header("Tolerance")]
        [Tooltip("Multiplier above max speed before flagging (network jitter tolerance).")]
        [Range(1.0f, 2.0f)] public float SpeedToleranceMultiplier = 1.3f;

        [Tooltip("Position delta above this is flagged as teleport (meters).")]
        [Min(5f)] public float TeleportThreshold = 20f;

        [Header("Error Accumulation")]
        [Tooltip("How fast accumulated error grows.")]
        [Min(0.1f)] public float ErrorAccumulationRate = 1f;

        [Tooltip("How fast accumulated error decays per second.")]
        [Min(0.1f)] public float ErrorDecayRate = 2f;

        [Tooltip("Error cap before violation fires.")]
        [Min(1f)] public float MaxAccumulatedError = 10f;

        [Tooltip("Ticks of immunity after server-granted teleport.")]
        [Min(1)] public uint TeleportGraceTicks = 10;
    }
}
