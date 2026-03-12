using UnityEngine;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Penalty configuration for violation thresholds and ban durations.
    /// Place in Resources/PenaltyConfig.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Validation/Penalty Config")]
    public class PenaltyConfigSO : ScriptableObject
    {
        [Header("Violation Thresholds")]
        [Tooltip("Violation score to issue a warning.")]
        [Min(0.1f)] public float WarnThreshold = 5f;

        [Tooltip("Violation score to kick the player.")]
        [Min(1f)] public float KickThreshold = 20f;

        [Header("Ban Escalation")]
        [Tooltip("Kicks within decay window before temp ban.")]
        [Min(1)] public int ConsecutiveKicksForTempBan = 3;

        [Tooltip("Temp bans before permanent ban.")]
        [Min(1)] public int TempBansForPermaBan = 3;

        [Tooltip("Duration of temp ban in minutes.")]
        [Min(1)] public int TempBanDurationMinutes = 30;

        [Header("Decay")]
        [Tooltip("Score decay per second (e.g., 0.5 = halves in 2s).")]
        [Min(0.01f)] public float ViolationDecayRate = 0.5f;

        [Header("Violation Weights")]
        [Tooltip("Multiplier for rate-limit violations.")]
        [Min(0.1f)] public float RateLimitWeight = 1f;

        [Tooltip("Multiplier for movement violations (high — speed hacks are obvious).")]
        [Min(0.1f)] public float MovementWeight = 2f;

        [Tooltip("Multiplier for economy violations (highest — money exploits are damaging).")]
        [Min(0.1f)] public float EconomyWeight = 3f;

        [Tooltip("Multiplier for cooldown violations.")]
        [Min(0.1f)] public float CooldownWeight = 1.5f;

        [Header("Warning")]
        [Tooltip("Minimum seconds between warnings to same player.")]
        [Min(1f)] public float WarnCooldownSeconds = 10f;
    }
}
