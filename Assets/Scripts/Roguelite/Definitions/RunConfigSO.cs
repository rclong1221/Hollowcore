using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Designer-authored run configuration. Baked to RunConfigBlob at runtime.
    /// Place in Resources/RunConfigs/ folder.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Run Configuration")]
    public class RunConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string ConfigName;
        public int ConfigId;

        [Header("Structure")]
        [Tooltip("Number of zones in the run.")]
        public int ZoneCount = 5;

        [Tooltip("Zone sequence defining the order and composition of zones in this run. Null = use ZoneCount only.")]
        public Zones.ZoneSequenceSO ZoneSequence;

        [Header("Difficulty")]
        [Tooltip("Difficulty curve from zone 0 (start) to zone N (end). Y-axis = multiplier.")]
        public AnimationCurve DifficultyPerZone = AnimationCurve.Linear(0, 1, 1, 3);

        [Header("Time")]
        [Tooltip("Per-zone time limit in seconds. 0 = no limit.")]
        public float BaseZoneTimeLimit;
        [Tooltip("Total run time limit in seconds. 0 = no limit.")]
        public float RunTimeLimit;

        [Header("Economy")]
        public int StartingRunCurrency;
        public int RunCurrencyPerZoneClear = 10;
        [Range(0f, 2f)]
        public float MetaCurrencyConversionRate = 0.5f;

        // AscensionDefinitionSO reference (23.4 — nullable)
        // public AscensionDefinitionSO AscensionDefinition;

        /// <summary>
        /// Samples the difficulty curve at a normalized position [0, 1].
        /// </summary>
        public float GetDifficultyAtZone(int zoneIndex)
        {
            if (ZoneCount <= 1) return DifficultyPerZone.Evaluate(0f);
            float t = (float)zoneIndex / (ZoneCount - 1);
            return DifficultyPerZone.Evaluate(t);
        }
    }
}
