#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.7: Balance Dashboard module.
    /// Cross-SO analytical dashboard with A/B comparison, spawn heatmap,
    /// difficulty curves, economy flow, and reward distribution charts.
    /// </summary>
    public class BalanceDashboardModule : IRunWorkstationModule
    {
        public string TabName => "Balance Dashboard";

        private RogueliteDataContext _context;
        private Vector2 _scrollPos;

        // A/B comparison
        private RunConfigSO _configA;
        private RunConfigSO _configB;
        private MonteCarloResult _resultsA;
        private MonteCarloResult _resultsB;
        private RunMonteCarloSimulator _simA;
        private RunMonteCarloSimulator _simB;
        private int _monteCarloRunCount = 500;
        private byte _compareAscension;

        // Heatmap data
        private float[,] _spawnHeatmap;
        private string[] _heatmapEnemyNames;
        private int _heatmapZoneCount;
        private RunConfigSO _heatmapConfig;

        // View mode
        private DashboardView _activeView = DashboardView.DifficultyComparison;
        private static readonly string[] ViewNames = {
            "Difficulty", "Spawn Heatmap", "Rewards", "Economy", "A/B Compare", "Elite Freq"
        };

        // Cached GUIStyles
        private static GUIStyle _heatmapCellStyle;
        private static GUIStyle _deltaStyle;
        private static bool _dashStylesInit;

        public void OnEnable()
        {
            _simA = new RunMonteCarloSimulator();
            _simB = new RunMonteCarloSimulator();
        }

        public void OnDisable() { }

        public void SetContext(RogueliteDataContext context)
        {
            _context = context;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Balance Dashboard", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // View selector
            _activeView = (DashboardView)GUILayout.SelectionGrid((int)_activeView, ViewNames, ViewNames.Length, EditorStyles.miniButton);
            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_activeView)
            {
                case DashboardView.DifficultyComparison: DrawDifficultyComparison(); break;
                case DashboardView.SpawnHeatmap: DrawSpawnHeatmap(); break;
                case DashboardView.RewardDistribution: DrawRewardDistribution(); break;
                case DashboardView.EconomyFlow: DrawEconomyFlow(); break;
                case DashboardView.ABComparison: DrawABComparison(); break;
                case DashboardView.EliteFrequency: DrawEliteFrequency(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ==================== Difficulty Comparison ====================

        private void DrawDifficultyComparison()
        {
            EditorGUILayout.LabelField("Difficulty Curve Comparison", EditorStyles.boldLabel);
            _configA = (RunConfigSO)EditorGUILayout.ObjectField("Config A", _configA, typeof(RunConfigSO), false);
            _configB = (RunConfigSO)EditorGUILayout.ObjectField("Config B", _configB, typeof(RunConfigSO), false);

            if (_configA == null) { EditorGUILayout.HelpBox("Assign at least Config A.", MessageType.Info); return; }

            int maxZones = _configA.ZoneCount;
            if (_configB != null && _configB.ZoneCount > maxZones) maxZones = _configB.ZoneCount;
            if (maxZones <= 0) return;

            float maxDiff = 0f;
            for (int z = 0; z < maxZones; z++)
            {
                if (z < _configA.ZoneCount)
                    maxDiff = Mathf.Max(maxDiff, _configA.GetDifficultyAtZone(z));
                if (_configB != null && z < _configB.ZoneCount)
                    maxDiff = Mathf.Max(maxDiff, _configB.GetDifficultyAtZone(z));
            }
            if (maxDiff <= 0f) maxDiff = 1f;

            EditorGUILayout.Space(4);
            for (int z = 0; z < maxZones; z++)
            {
                float diffA = z < _configA.ZoneCount ? _configA.GetDifficultyAtZone(z) : 0f;
                float diffB = _configB != null && z < _configB.ZoneCount ? _configB.GetDifficultyAtZone(z) : 0f;

                var rect = EditorGUILayout.GetControlRect(false, 18);
                var labelRect = new Rect(rect.x, rect.y, 45, rect.height);
                EditorGUI.LabelField(labelRect, $"Z{z}");

                float barMaxW = rect.width - 110;
                float pctA = diffA / (maxDiff * 1.1f);
                float pctB = diffB / (maxDiff * 1.1f);

                var barARect = new Rect(rect.x + 50, rect.y, barMaxW * pctA, 8);
                var barBRect = new Rect(rect.x + 50, rect.y + 9, barMaxW * pctB, 8);

                EditorGUI.DrawRect(barARect, new Color(0.3f, 0.5f, 0.9f, 0.8f));
                if (_configB != null)
                    EditorGUI.DrawRect(barBRect, new Color(0.9f, 0.5f, 0.3f, 0.8f));

                EditorGUI.LabelField(new Rect(rect.xMax - 55, rect.y, 55, rect.height),
                    _configB != null ? $"{diffA:F1} / {diffB:F1}" : $"{diffA:F1}x", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 8, GUILayout.Width(20)),
                new Color(0.3f, 0.5f, 0.9f, 0.8f));
            EditorGUILayout.LabelField("Config A", EditorStyles.miniLabel, GUILayout.Width(60));
            if (_configB != null)
            {
                EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 8, GUILayout.Width(20)),
                    new Color(0.9f, 0.5f, 0.3f, 0.8f));
                EditorGUILayout.LabelField("Config B", EditorStyles.miniLabel, GUILayout.Width(60));
            }
            EditorGUILayout.EndHorizontal();
        }

        // ==================== Spawn Heatmap ====================

        private void DrawSpawnHeatmap()
        {
            EditorGUILayout.LabelField("Enemy Spawn Heatmap", EditorStyles.boldLabel);
            _configA = (RunConfigSO)EditorGUILayout.ObjectField("Run Config", _configA, typeof(RunConfigSO), false);

            if (_configA == null || _configA.ZoneSequence == null)
            {
                EditorGUILayout.HelpBox("Assign a RunConfig with a ZoneSequence.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Build Heatmap") || _heatmapConfig != _configA)
                BuildHeatmap();

            if (_spawnHeatmap == null || _heatmapEnemyNames == null) return;

            EditorGUILayout.Space(4);
            int enemyCount = _heatmapEnemyNames.Length;
            int zoneCount = _heatmapZoneCount;

            // Header row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(120));
            for (int z = 0; z < zoneCount; z++)
                EditorGUILayout.LabelField($"Z{z}", EditorStyles.miniLabel, GUILayout.Width(35));
            EditorGUILayout.EndHorizontal();

            // Data rows
            for (int e = 0; e < enemyCount; e++)
            {
                EditorGUILayout.BeginHorizontal();
                string eName = _heatmapEnemyNames[e];
                if (eName.Length > 16) eName = eName.Substring(0, 14) + "..";
                EditorGUILayout.LabelField(eName, EditorStyles.miniLabel, GUILayout.Width(120));

                for (int z = 0; z < zoneCount; z++)
                {
                    float val = _spawnHeatmap[e, z];
                    var cellRect = EditorGUILayout.GetControlRect(false, 18, GUILayout.Width(35));
                    Color cellColor = val <= 0f ? new Color(0.15f, 0.15f, 0.15f)
                                    : Color.Lerp(new Color(0.2f, 0.3f, 0.6f), new Color(0.8f, 0.2f, 0.2f), val);
                    EditorGUI.DrawRect(cellRect, cellColor);
                    if (val > 0f)
                    {
                        if (!_dashStylesInit)
                        {
                            _heatmapCellStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 8 };
                            _deltaStyle = new GUIStyle(EditorStyles.label);
                            _dashStylesInit = true;
                        }
                        EditorGUI.LabelField(cellRect, $"{val:P0}", _heatmapCellStyle);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void BuildHeatmap()
        {
            _heatmapConfig = _configA;
            var seq = _configA.ZoneSequence;
            if (seq?.Layers == null) return;

            _heatmapZoneCount = _configA.ZoneCount;

            // Collect all unique enemy names across all encounter pools
            var enemyNames = new List<string>();
            var enemyIndexMap = new Dictionary<string, int>();

            foreach (var z in _context?.ZoneDefinitions ?? System.Array.Empty<ZoneDefinitionSO>())
            {
                if (z?.EncounterPool?.Entries == null) continue;
                foreach (var entry in z.EncounterPool.Entries)
                {
                    string name = !string.IsNullOrEmpty(entry.DisplayName)
                        ? entry.DisplayName
                        : entry.EnemyPrefab != null ? entry.EnemyPrefab.name : "Unknown";
                    if (!enemyIndexMap.ContainsKey(name))
                    {
                        enemyIndexMap[name] = enemyNames.Count;
                        enemyNames.Add(name);
                    }
                }
            }

            if (enemyNames.Count == 0) return;

            _heatmapEnemyNames = enemyNames.ToArray();
            _spawnHeatmap = new float[enemyNames.Count, _heatmapZoneCount];

            // For each zone, compute expected spawn frequencies
            for (int z = 0; z < _heatmapZoneCount && z < seq.Layers.Count; z++)
            {
                var layer = seq.Layers[z];
                if (layer.Entries == null || layer.Entries.Count == 0) continue;

                foreach (var seqEntry in layer.Entries)
                {
                    var zoneDef = seqEntry.Zone;
                    if (zoneDef?.EncounterPool?.Entries == null) continue;

                    float difficulty = _configA.GetDifficultyAtZone(z) * zoneDef.DifficultyMultiplier;
                    float totalWeight = 0f;

                    foreach (var e in zoneDef.EncounterPool.Entries)
                    {
                        if (e.MinDifficulty > 0 && difficulty < e.MinDifficulty) continue;
                        if (e.MaxDifficulty > 0 && difficulty > e.MaxDifficulty) continue;
                        totalWeight += e.Weight;
                    }

                    if (totalWeight <= 0f) continue;

                    foreach (var e in zoneDef.EncounterPool.Entries)
                    {
                        if (e.MinDifficulty > 0 && difficulty < e.MinDifficulty) continue;
                        if (e.MaxDifficulty > 0 && difficulty > e.MaxDifficulty) continue;

                        string name = !string.IsNullOrEmpty(e.DisplayName)
                            ? e.DisplayName
                            : e.EnemyPrefab != null ? e.EnemyPrefab.name : "Unknown";

                        if (enemyIndexMap.TryGetValue(name, out int idx))
                            _spawnHeatmap[idx, z] += e.Weight / totalWeight;
                    }
                }
            }
        }

        // ==================== Reward Distribution ====================

        private void DrawRewardDistribution()
        {
            EditorGUILayout.LabelField("Reward Distribution", EditorStyles.boldLabel);

            if (_context == null || _context.RewardPools.Length == 0)
            {
                EditorGUILayout.HelpBox("No RewardPools found in the project.", MessageType.Info);
                return;
            }

            foreach (var pool in _context.RewardPools)
            {
                if (pool?.Entries == null || pool.Entries.Count == 0) continue;

                EditorGUILayout.LabelField($"Pool: {pool.PoolName} ({pool.Entries.Count} entries, {pool.ChoiceCount} choices)", EditorStyles.miniLabel);

                float totalWeight = 0f;
                foreach (var e in pool.Entries)
                    totalWeight += e.Weight;
                if (totalWeight <= 0f) totalWeight = 1f;

                foreach (var e in pool.Entries)
                {
                    if (e.Reward == null) continue;
                    float pct = e.Weight / totalWeight;
                    var rect = EditorGUILayout.GetControlRect(false, 16);
                    var barRect = new Rect(rect.x + 100, rect.y, (rect.width - 150) * pct, rect.height - 2);
                    Color barColor = GetRarityColor(e.Reward.Rarity);
                    EditorGUI.DrawRect(barRect, barColor);

                    string label = e.Reward.DisplayName;
                    if (label.Length > 14) label = label.Substring(0, 12) + "..";
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, 98, rect.height), label, EditorStyles.miniLabel);
                    EditorGUI.LabelField(new Rect(rect.xMax - 45, rect.y, 45, rect.height), $"{pct:P0}", EditorStyles.miniLabel);
                }
                EditorGUILayout.Space(4);
            }
        }

        // ==================== Economy Flow ====================

        private void DrawEconomyFlow()
        {
            EditorGUILayout.LabelField("Economy Flow", EditorStyles.boldLabel);
            _configA = (RunConfigSO)EditorGUILayout.ObjectField("Run Config", _configA, typeof(RunConfigSO), false);

            if (_configA == null) { EditorGUILayout.HelpBox("Assign a RunConfig.", MessageType.Info); return; }

            EditorGUILayout.Space(4);
            int cumulative = _configA.StartingRunCurrency;
            EditorGUILayout.LabelField($"Starting: {cumulative}c", EditorStyles.miniLabel);

            for (int z = 0; z < _configA.ZoneCount; z++)
            {
                cumulative += _configA.RunCurrencyPerZoneClear;
                float diff = _configA.GetDifficultyAtZone(z);

                var rect = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.LabelField(new Rect(rect.x, rect.y, 45, rect.height), $"Z{z}");

                int maxExpected = (_configA.StartingRunCurrency + _configA.RunCurrencyPerZoneClear * _configA.ZoneCount);
                if (maxExpected <= 0) maxExpected = 1;
                float pct = (float)cumulative / maxExpected;

                var barRect = new Rect(rect.x + 50, rect.y + 2, (rect.width - 130) * pct, rect.height - 4);
                EditorGUI.DrawRect(barRect, new Color(0.9f, 0.8f, 0.2f, 0.6f));
                EditorGUI.LabelField(new Rect(rect.xMax - 75, rect.y, 75, rect.height),
                    $"{cumulative}c (+{_configA.RunCurrencyPerZoneClear})", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Total earned: {cumulative}c", EditorStyles.boldLabel);
            float metaEarned = cumulative * _configA.MetaCurrencyConversionRate;
            EditorGUILayout.LabelField($"Meta-currency (at {_configA.MetaCurrencyConversionRate:P0} rate): ~{metaEarned:F0}", EditorStyles.miniLabel);
        }

        // ==================== A/B Comparison ====================

        private void DrawABComparison()
        {
            EditorGUILayout.LabelField("A/B Comparison (Monte Carlo)", EditorStyles.boldLabel);
            _configA = (RunConfigSO)EditorGUILayout.ObjectField("Config A", _configA, typeof(RunConfigSO), false);
            _configB = (RunConfigSO)EditorGUILayout.ObjectField("Config B", _configB, typeof(RunConfigSO), false);
            _monteCarloRunCount = EditorGUILayout.IntSlider("Run Count", _monteCarloRunCount, 100, 5000);
            _compareAscension = (byte)EditorGUILayout.IntSlider("Ascension", _compareAscension, 0, 20);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _configA != null && !_simA.IsRunning;
            if (GUILayout.Button("Run A"))
                _simA.Start(_configA, _compareAscension, _monteCarloRunCount);
            GUI.enabled = _configB != null && !_simB.IsRunning;
            if (GUILayout.Button("Run B"))
                _simB.Start(_configB, _compareAscension, _monteCarloRunCount);
            GUI.enabled = _configA != null && _configB != null && !_simA.IsRunning && !_simB.IsRunning;
            if (GUILayout.Button("Run Both"))
            {
                _simA.Start(_configA, _compareAscension, _monteCarloRunCount);
                _simB.Start(_configB, _compareAscension, _monteCarloRunCount);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Store results when complete
            if (!_simA.IsRunning && _simA.Result != null) _resultsA = _simA.Result;
            if (!_simB.IsRunning && _simB.Result != null) _resultsB = _simB.Result;

            EditorGUILayout.Space(4);

            if (_resultsA != null || _resultsB != null)
                DrawComparisonTable();
        }

        private void DrawComparisonTable()
        {
            // Header
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField("Metric", EditorStyles.boldLabel, GUILayout.Width(140));
            EditorGUILayout.LabelField("Config A", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Config B", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Delta", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            float aScore = _resultsA?.AverageScore ?? 0;
            float bScore = _resultsB?.AverageScore ?? 0;
            DrawComparisonRow("Avg Score", aScore, bScore);

            float aCurrency = _resultsA?.AverageCurrencyEarned ?? 0;
            float bCurrency = _resultsB?.AverageCurrencyEarned ?? 0;
            DrawComparisonRow("Avg Currency", aCurrency, bCurrency);

            float aZones = _resultsA?.AverageZonesCleared ?? 0;
            float bZones = _resultsB?.AverageZonesCleared ?? 0;
            DrawComparisonRow("Avg Zones", aZones, bZones);

            float aUnlock = _resultsA?.EstimatedRunsToFullUnlock ?? 0;
            float bUnlock = _resultsB?.EstimatedRunsToFullUnlock ?? 0;
            if (aUnlock > 0 || bUnlock > 0)
                DrawComparisonRow("Runs to Unlock", aUnlock, bUnlock);
        }

        private static void DrawComparisonRow(string label, float a, float b)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(140));
            EditorGUILayout.LabelField(a > 0 ? $"{a:F1}" : "-", GUILayout.Width(100));
            EditorGUILayout.LabelField(b > 0 ? $"{b:F1}" : "-", GUILayout.Width(100));

            float delta = b - a;
            Color deltaColor = delta > 0 ? new Color(0.3f, 0.8f, 0.3f) : delta < 0 ? new Color(0.8f, 0.3f, 0.3f) : Color.gray;
            if (!_dashStylesInit)
            {
                _heatmapCellStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 8 };
                _deltaStyle = new GUIStyle(EditorStyles.label);
                _dashStylesInit = true;
            }
            _deltaStyle.normal.textColor = deltaColor;
            string arrow = delta > 0 ? "+" : "";
            EditorGUILayout.LabelField(a > 0 && b > 0 ? $"{arrow}{delta:F1}" : "-", _deltaStyle, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        // ==================== Elite Frequency ====================

        private void DrawEliteFrequency()
        {
            EditorGUILayout.LabelField("Elite Spawn Frequency", EditorStyles.boldLabel);
            _configA = (RunConfigSO)EditorGUILayout.ObjectField("Run Config", _configA, typeof(RunConfigSO), false);

            if (_configA == null || _configA.ZoneSequence == null)
            {
                EditorGUILayout.HelpBox("Assign a RunConfig with a ZoneSequence.", MessageType.Info);
                return;
            }

            var seq = _configA.ZoneSequence;
            EditorGUILayout.Space(4);

            for (int z = 0; z < _configA.ZoneCount && z < seq.Layers.Count; z++)
            {
                var layer = seq.Layers[z];
                if (layer.Entries == null || layer.Entries.Count == 0) continue;

                var zoneDef = layer.Entries[0].Zone;
                if (zoneDef?.SpawnDirectorConfig == null) continue;

                float diff = _configA.GetDifficultyAtZone(z) * zoneDef.DifficultyMultiplier;
                var dc = zoneDef.SpawnDirectorConfig;

                bool elitesEnabled = diff >= dc.EliteMinDifficulty;
                float eliteChance = elitesEnabled ? dc.EliteChance : 0f;

                var rect = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.LabelField(new Rect(rect.x, rect.y, 45, rect.height), $"Z{z}");

                Color barColor = elitesEnabled ? new Color(0.9f, 0.5f, 0.2f, 0.7f) : new Color(0.3f, 0.3f, 0.3f, 0.3f);
                var barRect = new Rect(rect.x + 50, rect.y + 2, (rect.width - 170) * eliteChance * 4f, rect.height - 4);
                EditorGUI.DrawRect(barRect, barColor);

                string status = elitesEnabled ? $"{eliteChance:P0} (diff {diff:F1} >= {dc.EliteMinDifficulty:F1})" : $"Locked (diff {diff:F1} < {dc.EliteMinDifficulty:F1})";
                EditorGUI.LabelField(new Rect(rect.xMax - 250, rect.y, 250, rect.height), status, EditorStyles.miniLabel);
            }
        }

        // ==================== Helpers ====================

        private static Color GetRarityColor(byte rarity)
        {
            return rarity switch
            {
                0 => new Color(0.6f, 0.6f, 0.6f, 0.5f), // Common
                1 => new Color(0.3f, 0.7f, 0.3f, 0.5f), // Uncommon
                2 => new Color(0.3f, 0.4f, 0.9f, 0.5f), // Rare
                3 => new Color(0.7f, 0.3f, 0.8f, 0.5f), // Epic
                4 => new Color(0.9f, 0.7f, 0.2f, 0.5f), // Legendary
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };
        }
    }

    public enum DashboardView
    {
        DifficultyComparison,
        SpawnHeatmap,
        RewardDistribution,
        EconomyFlow,
        ABComparison,
        EliteFrequency
    }
}
#endif
