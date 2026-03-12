using UnityEngine;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Configures HOW the spawn director operates in a zone.
    /// Different presets support different game styles:
    ///   "Corridor Burst"   — high initial budget, zero regen
    ///   "Open World"       — low initial budget, steady regen with acceleration
    ///   "Arena Survival"   — zero initial, fast regen, overwhelms over time
    ///   "Boss Room"        — zero budget (boss from EncounterState)
    ///   "Rest Zone"        — zero budget, zero regen
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnDirectorConfig", menuName = "DIG/Roguelite/Spawn Director Config", order = 13)]
    public class SpawnDirectorConfigSO : ScriptableObject
    {
        [Header("Budget")]
        [Tooltip("Starting spawn credits when zone activates.")]
        public float InitialBudget = 100f;

        [Tooltip("Credits earned per second during Active phase. Zero = one-shot mode.")]
        public float CreditsPerSecond;

        [Tooltip("Multiplier applied to CreditsPerSecond based on time in zone. " +
                 "1.0 = linear. >1.0 = accelerating. " +
                 "effectiveRate = CreditsPerSecond * (1 + timeInZone * Acceleration).")]
        public float Acceleration;

        [Tooltip("Maximum accumulated unspent credits. 0 = unlimited.")]
        public float MaxBudget = 500f;

        [Header("Spawn Rules")]
        [Tooltip("Minimum seconds between spawn attempts.")]
        public float MinSpawnInterval = 0.5f;

        [Tooltip("Maximum concurrent alive enemies from this director. 0 = unlimited.")]
        public int MaxAliveEnemies = 40;

        [Tooltip("Minimum distance from any player to spawn.")]
        public float MinSpawnDistance = 15f;

        [Tooltip("Maximum distance from nearest player to spawn.")]
        public float MaxSpawnDistance = 80f;

        [Tooltip("Don't spawn enemies within this distance of player (prevents pop-in).")]
        public float NoSpawnRadius = 10f;

        [Header("Elite Spawning")]
        [Range(0f, 1f)]
        [Tooltip("Chance (0-1) that a spawn attempt produces an elite variant.")]
        public float EliteChance = 0.05f;

        [Tooltip("Credit cost multiplier for elite variants.")]
        public float EliteCostMultiplier = 3f;

        [Tooltip("Minimum effective difficulty before elites can spawn.")]
        public float EliteMinDifficulty = 2f;

        [Header("Difficulty Scaling")]
        [Tooltip("If true, difficulty increases CreditsPerSecond.")]
        public bool DifficultyAffectsRate = true;

        [Tooltip("Multiplier for CreditsPerSecond per point of effective difficulty.")]
        public float DifficultyRateMultiplier = 0.5f;
    }
}
