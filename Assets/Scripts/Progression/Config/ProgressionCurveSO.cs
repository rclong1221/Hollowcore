using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: XP curve and formula configuration.
    /// Defines XP thresholds per level, kill XP formula, diminishing returns,
    /// rested XP parameters, and source-specific base XP values.
    /// Loaded from Resources/ProgressionCurve by ProgressionBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionCurve", menuName = "DIG/Progression/Progression Curve", order = 0)]
    public class ProgressionCurveSO : ScriptableObject
    {
        [Header("Level Caps")]
        [Min(1)] public int MaxLevel = 50;
        [Min(0)] public int StatPointsPerLevel = 3;

        [Header("XP Per Level")]
        [Tooltip("XP required for each level-up. Index 0 = level 1→2. If empty or shorter than MaxLevel, remaining levels use geometric formula.")]
        public int[] XPPerLevel;

        [Header("Geometric Fallback (for unspecified levels)")]
        [Min(1)] public int GeometricBaseXP = 100;
        [Tooltip("Each level requires this multiple of the previous level's XP")]
        [Range(1.01f, 3f)] public float GeometricMultiplier = 1.12f;

        [Header("Kill XP Formula")]
        [Tooltip("Base XP for killing a level 1 enemy")]
        [Min(1)] public float BaseKillXP = 100f;
        [Tooltip("XP multiplier per enemy level: rawXP = BaseKillXP * pow(KillXPPerEnemyLevel, enemyLevel - 1)")]
        [Range(1f, 2f)] public float KillXPPerEnemyLevel = 1.15f;

        [Header("Diminishing Returns")]
        [Tooltip("Level gap between player and enemy before diminishing returns apply")]
        [Min(0)] public int DiminishStartDelta = 3;
        [Tooltip("XP multiplier reduction per level below threshold")]
        [Range(0f, 1f)] public float DiminishFactorPerLevel = 0.8f;
        [Tooltip("Minimum XP multiplier floor (never goes below this)")]
        [Range(0f, 1f)] public float DiminishFloor = 0.1f;

        [Header("Other XP Sources")]
        [Min(0)] public float QuestXPBase = 200f;
        [Min(0)] public float CraftXPBase = 50f;
        [Min(0)] public float ExplorationXPBase = 150f;
        [Min(0)] public float InteractionXPBase = 25f;

        [Header("Rested XP")]
        [Tooltip("Bonus multiplier when rested (1.0 = double XP while rested)")]
        [Min(0)] public float RestedXPMultiplier = 1.0f;
        [Tooltip("Rested pool accumulation per offline hour")]
        [Min(0)] public float RestedXPAccumRatePerHour = 500f;
        [Tooltip("Maximum offline days counted for rested accumulation")]
        [Min(0)] public float RestedXPMaxDays = 3f;

        /// <summary>
        /// Returns the XP required for a specific level-up.
        /// Uses designer-defined values if available, otherwise geometric formula.
        /// </summary>
        public int GetXPForLevel(int level)
        {
            int index = level - 1; // level 1→2 is index 0
            if (XPPerLevel != null && index < XPPerLevel.Length && XPPerLevel[index] > 0)
                return XPPerLevel[index];

            // Geometric fallback
            return Mathf.RoundToInt(GeometricBaseXP * Mathf.Pow(GeometricMultiplier, index));
        }
    }
}
