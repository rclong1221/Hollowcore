#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.PvP.Editor.Modules
{
    /// <summary>
    /// EPIC 17.10: Win rate by Elo bracket, normalization impact analysis,
    /// spawn point fairness heatmap.
    /// </summary>
    public class BalanceAnalyzerModule : IPvPWorkstationModule
    {
        public string ModuleName => "Balance Analyzer";

        private PvPArenaConfigSO _arenaConfig;
        private PvPRankingConfigSO _rankingConfig;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Balance Analyzer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _arenaConfig = (PvPArenaConfigSO)EditorGUILayout.ObjectField(
                "Arena Config", _arenaConfig, typeof(PvPArenaConfigSO), false);
            _rankingConfig = (PvPRankingConfigSO)EditorGUILayout.ObjectField(
                "Ranking Config", _rankingConfig, typeof(PvPRankingConfigSO), false);

            if (_arenaConfig == null)
                _arenaConfig = Resources.Load<PvPArenaConfigSO>("PvPArenaConfig");
            if (_rankingConfig == null)
                _rankingConfig = Resources.Load<PvPRankingConfigSO>("PvPRankingConfig");

            // Normalization Analysis
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Normalization Settings", EditorStyles.boldLabel);

            if (_arenaConfig != null)
            {
                EditorGUILayout.LabelField("Enabled", _arenaConfig.NormalizationEnabled ? "YES" : "NO");
                if (_arenaConfig.NormalizationEnabled)
                {
                    EditorGUILayout.LabelField("Max Health", _arenaConfig.NormalizedMaxHealth.ToString("F0"));
                    EditorGUILayout.LabelField("Attack Power", _arenaConfig.NormalizedAttackPower.ToString("F1"));
                    EditorGUILayout.LabelField("Spell Power", _arenaConfig.NormalizedSpellPower.ToString("F1"));
                    EditorGUILayout.LabelField("Defense", _arenaConfig.NormalizedDefense.ToString("F1"));
                    EditorGUILayout.LabelField("Armor", _arenaConfig.NormalizedArmor.ToString("F1"));
                }
            }

            // Anti-grief Analysis
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Anti-Grief Settings", EditorStyles.boldLabel);

            if (_arenaConfig != null)
            {
                EditorGUILayout.LabelField("AFK Timeout", $"{_arenaConfig.AFKTimeoutSeconds}s");
                EditorGUILayout.LabelField("AFK Warnings Before Kick", _arenaConfig.AFKWarningsBeforeKick.ToString());
                EditorGUILayout.LabelField("Leaver Cooldown", $"{_arenaConfig.LeaverPenaltyCooldown}s ({_arenaConfig.LeaverPenaltyCooldown / 60f:F1} min)");
                EditorGUILayout.LabelField("Spawn Camp Radius", $"{_arenaConfig.SpawnCampingRadius}m");
                EditorGUILayout.LabelField("Spawn Camp Window", $"{_arenaConfig.SpawnCampingWindow}s");
            }

            // XP Analysis
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("XP Settings", EditorStyles.boldLabel);

            if (_arenaConfig != null)
            {
                EditorGUILayout.LabelField("Kill XP Multiplier", $"{_arenaConfig.PvPKillXPMultiplier:P0} of PvE rate");
                EditorGUILayout.LabelField("Win Bonus XP", _arenaConfig.PvPWinBonusXP.ToString("F0"));
                EditorGUILayout.LabelField("Loss Consolation XP", _arenaConfig.PvPLossBonusXP.ToString("F0"));
            }

            // Elo Bracket Expected Win Rates
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Expected Win Rates by Elo Difference", EditorStyles.boldLabel);

            int[] eloDiffs = { 0, 100, 200, 400, 600, 800 };
            foreach (var diff in eloDiffs)
            {
                double expected = 1.0 / (1.0 + System.Math.Pow(10.0, -diff / 400.0));
                EditorGUILayout.LabelField($"  +{diff} Elo advantage", $"{expected * 100:F1}% expected win rate");
            }

            // Map validation
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Map Validation", EditorStyles.boldLabel);

            var mapGuids = AssetDatabase.FindAssets("t:PvPMapDefinitionSO");
            if (mapGuids.Length == 0)
            {
                EditorGUILayout.HelpBox("No PvPMapDefinitionSO assets found.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < mapGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(mapGuids[i]);
                    var map = AssetDatabase.LoadAssetAtPath<PvPMapDefinitionSO>(path);
                    if (map == null) continue;

                    int spawnCount = map.SpawnPoints != null ? map.SpawnPoints.Length : 0;
                    bool valid = spawnCount >= map.MaxPlayers;
                    string status = valid ? "OK" : "NEEDS MORE SPAWNS";

                    EditorGUILayout.LabelField($"  {map.MapName}",
                        $"Spawns: {spawnCount}/{map.MaxPlayers} [{status}]");
                }
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
