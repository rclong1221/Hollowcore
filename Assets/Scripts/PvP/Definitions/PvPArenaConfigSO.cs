using UnityEngine;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: ScriptableObject defining PvP arena match parameters.
    /// Loaded from Resources/PvPArenaConfig by PvPBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "PvPArenaConfig", menuName = "DIG/PvP/Arena Config")]
    public class PvPArenaConfigSO : ScriptableObject
    {
        [Header("Match Timers")]
        [Tooltip("Warmup phase countdown (seconds).")]
        [Min(0f)] public float WarmupDuration = 30f;

        [Tooltip("Post-match results display (seconds).")]
        [Min(0f)] public float ResultsDuration = 15f;

        [Tooltip("Overtime duration (0 = sudden death).")]
        [Min(0f)] public float OvertimeDuration = 60f;

        [Tooltip("Seconds before respawn after death.")]
        [Min(1f)] public float RespawnDelay = 5f;

        [Tooltip("Invulnerability after spawn (seconds).")]
        [Min(0f)] public float SpawnProtectionDuration = 3f;

        [Header("Score Limits")]
        [Tooltip("Kill limit for FFA mode.")]
        [Min(1)] public int FreeForAllKillLimit = 20;

        [Tooltip("Combined kill limit for Team Deathmatch.")]
        [Min(1)] public int TeamDeathmatchKillLimit = 50;

        [Tooltip("Score limit for CapturePoint mode.")]
        [Min(1)] public int CapturePointScoreLimit = 1000;

        [Tooltip("Best of N for Duel mode.")]
        [Min(1)] public int DuelRounds = 5;

        [Header("Equipment Normalization")]
        [Tooltip("Enable stat normalization for competitive fairness.")]
        public bool NormalizationEnabled = false;

        [Min(1f)] public float NormalizedMaxHealth = 1000f;
        [Min(1f)] public float NormalizedAttackPower = 50f;
        [Min(1f)] public float NormalizedSpellPower = 50f;
        [Min(0f)] public float NormalizedDefense = 30f;
        [Min(0f)] public float NormalizedArmor = 20f;

        [Header("Anti-Grief")]
        [Tooltip("AFK detection threshold (seconds).")]
        [Min(10f)] public float AFKTimeoutSeconds = 60f;

        [Tooltip("Warnings before auto-kick.")]
        [Min(1)] public int AFKWarningsBeforeKick = 3;

        [Tooltip("Queue ban for leavers (seconds).")]
        [Min(0f)] public float LeaverPenaltyCooldown = 300f;

        [Tooltip("Spawn camping detection radius (meters).")]
        [Min(5)] public int SpawnCampingRadius = 15;

        [Tooltip("Spawn camping detection window (seconds).")]
        [Min(1f)] public float SpawnCampingWindow = 5f;

        [Header("XP")]
        [Tooltip("PvP kill XP relative to PvE.")]
        [Range(0f, 2f)] public float PvPKillXPMultiplier = 0.5f;

        [Tooltip("Bonus XP for match winner.")]
        [Min(0f)] public float PvPWinBonusXP = 500f;

        [Tooltip("Consolation XP for losers.")]
        [Min(0f)] public float PvPLossBonusXP = 100f;
    }
}
