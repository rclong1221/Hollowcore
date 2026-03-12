#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.6: Monte Carlo balance simulator.
    /// Runs N simulated runs and aggregates statistics for balance analysis.
    /// Uses MonteCarloAccumulator (no string/list allocs per run) for minimal GC pressure.
    /// Async via EditorApplication.update to avoid blocking the editor.
    /// </summary>
    public class RunMonteCarloSimulator
    {
        private RunConfigSO _config;
        private byte _ascensionLevel;
        private int _totalRuns;
        private int _completedRuns;
        private bool _isRunning;
        private bool _cancelled;
        private MonteCarloResult _result;
        private MonteCarloAccumulator _accumulator;

        public bool IsRunning => _isRunning;
        public float Progress => _totalRuns > 0 ? (float)_completedRuns / _totalRuns : 0f;
        public MonteCarloResult Result => _result;

        /// <summary>
        /// Start a Monte Carlo simulation asynchronously.
        /// </summary>
        public void Start(RunConfigSO config, byte ascensionLevel, int runCount = 1000)
        {
            if (config == null)
            {
                Debug.LogError("[MonteCarlo] RunConfigSO is null.");
                return;
            }

            _config = config;
            _ascensionLevel = ascensionLevel;
            _totalRuns = runCount;
            _completedRuns = 0;
            _isRunning = true;
            _cancelled = false;
            _result = null;
            _accumulator = new MonteCarloAccumulator(config.ZoneCount);

            EditorApplication.update += ProcessBatch;
        }

        public void Cancel()
        {
            _cancelled = true;
        }

        private void ProcessBatch()
        {
            if (_cancelled || _completedRuns >= _totalRuns)
            {
                EditorApplication.update -= ProcessBatch;
                _isRunning = false;

                if (!_cancelled)
                    FinalizeResult();

                return;
            }

            // Process a batch of runs per editor frame to stay responsive
            int batchSize = math.min(50, _totalRuns - _completedRuns);
            for (int i = 0; i < batchSize; i++)
            {
                uint seed = (uint)(_completedRuns + 1);
                RunSimulator.SimulateAggregate(_config, seed, _ascensionLevel, ref _accumulator);
                _completedRuns++;
            }

            // Show progress bar
            if (EditorUtility.DisplayCancelableProgressBar(
                "Monte Carlo Simulation",
                $"Simulating run {_completedRuns}/{_totalRuns}...",
                Progress))
            {
                Cancel();
                EditorUtility.ClearProgressBar();
            }
        }

        private void FinalizeResult()
        {
            EditorUtility.ClearProgressBar();

            float n = _totalRuns;
            _result = new MonteCarloResult
            {
                RunCount = _totalRuns,
                AverageScore = _accumulator.TotalScore / n,
                AverageCurrencyEarned = _accumulator.TotalCurrency / n,
                AverageZonesCleared = _accumulator.TotalZonesCleared / n,
                RewardFrequency = _accumulator.RewardFrequency,
                ModifierFrequency = _accumulator.ModifierFrequency,
                DifficultyPerZone = new float[_accumulator.DifficultyPerZone.Length],
                CurrencyPerZone = new float[_accumulator.CurrencyPerZone.Length]
            };

            for (int z = 0; z < _accumulator.DifficultyPerZone.Length; z++)
            {
                _result.DifficultyPerZone[z] = _accumulator.DifficultyPerZone[z] / n;
                _result.CurrencyPerZone[z] = _accumulator.CurrencyPerZone[z] / n;
            }

            // Estimate runs to full unlock (if a MetaUnlockTree exists in Resources)
            var unlockTree = Resources.Load<MetaUnlockTreeSO>("MetaUnlockTree");
            if (_result.AverageCurrencyEarned > 0 && unlockTree != null)
            {
                int totalUnlockCost = GetTotalUnlockCost(unlockTree);
                _result.EstimatedRunsToFullUnlock = totalUnlockCost / _result.AverageCurrencyEarned;
            }

            // Release accumulator
            _accumulator = null;

            Debug.Log($"[MonteCarlo] Complete: {_totalRuns} runs. " +
                      $"Avg score={_result.AverageScore:F0}, " +
                      $"Avg currency={_result.AverageCurrencyEarned:F0}, " +
                      $"Avg zones={_result.AverageZonesCleared:F1}, " +
                      $"Runs to full unlock={_result.EstimatedRunsToFullUnlock:F0}");
        }

        private static int GetTotalUnlockCost(MetaUnlockTreeSO tree)
        {
            if (tree == null || tree.Unlocks == null) return 0;

            int total = 0;
            for (int i = 0; i < tree.Unlocks.Count; i++)
                total += tree.Unlocks[i].Cost;
            return total;
        }

        /// <summary>
        /// Draw Monte Carlo results in an editor GUI context.
        /// </summary>
        public static void DrawResults(MonteCarloResult result)
        {
            if (result == null) return;

            EditorGUILayout.LabelField("Monte Carlo Results", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Runs simulated: {result.RunCount}");
            EditorGUILayout.LabelField($"Average score: {result.AverageScore:F0}");
            EditorGUILayout.LabelField($"Average currency earned: {result.AverageCurrencyEarned:F0}");
            EditorGUILayout.LabelField($"Average zones cleared: {result.AverageZonesCleared:F1}");

            if (result.EstimatedRunsToFullUnlock > 0)
                EditorGUILayout.LabelField($"Estimated runs to full unlock: {result.EstimatedRunsToFullUnlock:F0}");

            // Difficulty per zone
            if (result.DifficultyPerZone != null && result.DifficultyPerZone.Length > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Avg Difficulty Per Zone:", EditorStyles.miniLabel);
                for (int z = 0; z < result.DifficultyPerZone.Length; z++)
                {
                    float d = result.DifficultyPerZone[z];
                    int barWidth = Mathf.Clamp((int)(d * 20f), 1, 60);
                    EditorGUILayout.LabelField($"  Zone {z}: {new string('|', barWidth)} {d:F2}x");
                }
            }
        }
    }
}
#endif
