#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.PvP.Editor.Modules
{
    /// <summary>
    /// EPIC 17.10: "Simulate N matches" button, Elo distribution analysis,
    /// tier progression chart, K-factor analysis.
    /// </summary>
    public class RankingSimulatorModule : IPvPWorkstationModule
    {
        public string ModuleName => "Ranking Simulator";

        private PvPRankingConfigSO _config;
        private int _simulationCount = 100;
        private int _playerCount = 2;
        private float _player1WinRate = 0.5f;
        private int _startingElo = 1200;

        // Simulation results
        private int _resultElo1;
        private int _resultElo2;
        private PvPTier _resultTier1;
        private PvPTier _resultTier2;
        private int _resultWins1;
        private int _resultLosses1;
        private bool _hasResults;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Ranking Simulator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _config = (PvPRankingConfigSO)EditorGUILayout.ObjectField(
                "Ranking Config", _config, typeof(PvPRankingConfigSO), false);

            if (_config == null)
            {
                _config = Resources.Load<PvPRankingConfigSO>("PvPRankingConfig");
                if (_config == null)
                {
                    EditorGUILayout.HelpBox("No PvPRankingConfigSO found. Create one or select manually.", MessageType.Warning);
                    return;
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Simulation Parameters", EditorStyles.boldLabel);

            _simulationCount = EditorGUILayout.IntSlider("Match Count", _simulationCount, 10, 1000);
            _startingElo = EditorGUILayout.IntField("Starting Elo", _startingElo);
            _player1WinRate = EditorGUILayout.Slider("Player 1 Win Rate", _player1WinRate, 0f, 1f);

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Run Simulation", GUILayout.Height(30)))
            {
                RunSimulation();
            }

            if (_hasResults)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Player 1: {_resultElo1} Elo ({_resultTier1})");
                EditorGUILayout.LabelField($"  Record: {_resultWins1}W / {_resultLosses1}L");
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Player 2: {_resultElo2} Elo ({_resultTier2})");
                EditorGUILayout.LabelField($"  Record: {_simulationCount - _resultWins1}W / {_resultWins1}L");
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(4);
                int eloDiff = Mathf.Abs(_resultElo1 - _resultElo2);
                EditorGUILayout.LabelField($"Elo Spread: {eloDiff}");
                EditorGUILayout.LabelField($"Elo Change from Start: P1={_resultElo1 - _startingElo:+0;-0}, P2={_resultElo2 - _startingElo:+0;-0}");
            }

            // K-Factor reference
            EditorGUILayout.Space(16);
            EditorGUILayout.LabelField("K-Factor Reference", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Standard K: {_config.KFactor}");
            EditorGUILayout.LabelField($"High Rating K: {_config.KFactorHighRating} (above {_config.HighRatingThreshold})");
            EditorGUILayout.LabelField($"Placement K: {_config.KFactor * _config.PlacementKMultiplier:F0} ({_config.PlacementMatchCount} matches)");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tier Thresholds", EditorStyles.boldLabel);
            if (_config.TierThresholds != null)
            {
                string[] tierNames = { "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Master" };
                for (int i = 0; i < _config.TierThresholds.Length && i < tierNames.Length; i++)
                    EditorGUILayout.LabelField($"  {tierNames[i]}", $">= {_config.TierThresholds[i]} Elo");
            }
        }

        private void RunSimulation()
        {
            int elo1 = _startingElo;
            int elo2 = _startingElo;
            int wins1 = 0;
            int streak1 = 0;
            int streak2 = 0;

            for (int m = 0; m < _simulationCount; m++)
            {
                bool p1Wins = Random.value < _player1WinRate;

                // K factor
                float k1 = GetKFactor(elo1, m);
                float k2 = GetKFactor(elo2, m);

                // Expected win
                double exp1 = 1.0 / (1.0 + System.Math.Pow(10.0, (elo2 - elo1) / 400.0));
                double exp2 = 1.0 - exp1;

                float actual1 = p1Wins ? 1f : 0f;
                float actual2 = p1Wins ? 0f : 1f;

                float delta1 = k1 * (actual1 - (float)exp1);
                float delta2 = k2 * (actual2 - (float)exp2);

                if (p1Wins)
                {
                    wins1++;
                    streak1++;
                    streak2 = 0;
                    if (streak1 >= 3)
                        delta1 += Mathf.Min(_config.WinStreakBonus * (streak1 - 2), _config.MaxWinStreakBonus);
                }
                else
                {
                    streak2++;
                    streak1 = 0;
                    if (streak2 >= 3)
                        delta2 += Mathf.Min(_config.WinStreakBonus * (streak2 - 2), _config.MaxWinStreakBonus);
                }

                elo1 = Mathf.Max(0, elo1 + Mathf.RoundToInt(delta1));
                elo2 = Mathf.Max(0, elo2 + Mathf.RoundToInt(delta2));
            }

            _resultElo1 = elo1;
            _resultElo2 = elo2;
            _resultTier1 = _config.GetTierForElo(elo1);
            _resultTier2 = _config.GetTierForElo(elo2);
            _resultWins1 = wins1;
            _resultLosses1 = _simulationCount - wins1;
            _hasResults = true;
        }

        private float GetKFactor(int elo, int matchIndex)
        {
            float k = _config.KFactor;
            if (elo > _config.HighRatingThreshold)
                k = _config.KFactorHighRating;
            if (matchIndex < _config.PlacementMatchCount)
                k *= _config.PlacementKMultiplier;
            return k;
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
