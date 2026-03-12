#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using DIG.Items;
using DIG.Loot.Components;
using DIG.Loot.Definitions;
using DIG.Loot.Systems;

namespace DIG.Loot.Editor
{
    /// <summary>
    /// EPIC 16.6: Editor window for simulating loot drops with adjustable parameters.
    /// </summary>
    public class LootSimulatorWindow : EditorWindow
    {
        private LootTableSO _selectedTable;
        private int _rollCount = 1000;
        private int _level = 1;
        private float _difficulty = 1f;
        private float _luck = 0f;
        private Vector2 _scrollPos;
        private string _results;

        [MenuItem("DIG/Loot Simulator")]
        public static void ShowWindow()
        {
            GetWindow<LootSimulatorWindow>("Loot Simulator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Loot Drop Simulator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _selectedTable = (LootTableSO)EditorGUILayout.ObjectField("Loot Table", _selectedTable, typeof(LootTableSO), false);
            _rollCount = EditorGUILayout.IntSlider("Roll Count", _rollCount, 1, 100000);
            _level = EditorGUILayout.IntSlider("Enemy Level", _level, 1, 100);
            _difficulty = EditorGUILayout.Slider("Difficulty Multiplier", _difficulty, 0.1f, 5f);
            _luck = EditorGUILayout.Slider("Luck Modifier", _luck, -1f, 1f);

            EditorGUILayout.Space(5);

            GUI.enabled = _selectedTable != null;
            if (GUILayout.Button("Run Simulation", GUILayout.Height(30)))
            {
                RunSimulation();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);

            if (!string.IsNullOrEmpty(_results))
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
                EditorGUILayout.TextArea(_results, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void RunSimulation()
        {
            if (_selectedTable == null) return;

            var dropCounts = new System.Collections.Generic.Dictionary<string, DropStat>();
            int totalDrops = 0;
            int emptyRolls = 0;
            var rarityCounts = new int[6]; // Common through Unique

            for (int i = 0; i < _rollCount; i++)
            {
                var results = new NativeList<LootDrop>(8, Allocator.Temp);
                var context = new LootContext
                {
                    Level = _level,
                    DifficultyMultiplier = _difficulty,
                    LuckModifier = _luck,
                    RandomSeed = (uint)(i * 7 + 13)
                };

                LootTableResolver.Resolve(_selectedTable, context, ref results);

                if (results.Length == 0)
                {
                    emptyRolls++;
                }

                for (int j = 0; j < results.Length; j++)
                {
                    var drop = results[j];
                    string key = drop.Type switch
                    {
                        LootEntryType.Item => $"Item #{drop.ItemTypeId}",
                        LootEntryType.Resource => $"Resource: {drop.Resource}",
                        LootEntryType.Currency => $"Currency: {drop.Currency}",
                        _ => "Unknown"
                    };

                    if (!dropCounts.ContainsKey(key))
                        dropCounts[key] = new DropStat();

                    var stat = dropCounts[key];
                    stat.Count++;
                    stat.TotalQuantity += drop.Quantity;
                    dropCounts[key] = stat;

                    totalDrops++;

                    if (drop.Type == LootEntryType.Item && (int)drop.Rarity < rarityCounts.Length)
                        rarityCounts[(int)drop.Rarity]++;
                }

                results.Dispose();
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Simulation: {_selectedTable.TableName} ===");
            sb.AppendLine($"Rolls: {_rollCount} | Level: {_level} | Difficulty: {_difficulty:F1} | Luck: {_luck:F2}");
            sb.AppendLine($"Total drops: {totalDrops} ({(float)totalDrops / _rollCount:F2} avg/roll)");
            sb.AppendLine($"Empty rolls: {emptyRolls} ({(float)emptyRolls / _rollCount:P1})");
            sb.AppendLine();

            sb.AppendLine("--- Rarity Distribution ---");
            string[] rarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Unique" };
            for (int r = 0; r < rarityCounts.Length; r++)
            {
                if (rarityCounts[r] > 0)
                    sb.AppendLine($"  {rarityNames[r]}: {rarityCounts[r]} ({(float)rarityCounts[r] / Mathf.Max(1, totalDrops):P1})");
            }
            sb.AppendLine();

            sb.AppendLine("--- Item Breakdown ---");
            foreach (var kvp in dropCounts)
            {
                float dropRate = (float)kvp.Value.Count / _rollCount;
                float avgQty = (float)kvp.Value.TotalQuantity / kvp.Value.Count;
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count} drops ({dropRate:P1} rate, {avgQty:F1} avg qty)");
            }

            _results = sb.ToString();
        }

        private struct DropStat
        {
            public int Count;
            public int TotalQuantity;
        }
    }
}
#endif
