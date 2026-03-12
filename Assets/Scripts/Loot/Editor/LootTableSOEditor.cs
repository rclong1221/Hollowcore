#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using DIG.Loot.Definitions;
using DIG.Loot.Components;
using DIG.Loot.Systems;

namespace DIG.Loot.Editor
{
    /// <summary>
    /// EPIC 16.6: Custom editor for LootTableSO with visual pool weights and drop simulation.
    /// </summary>
    [CustomEditor(typeof(LootTableSO))]
    public class LootTableSOEditor : UnityEditor.Editor
    {
        private bool _showSimulation;
        private int _simulationCount = 1000;
        private string _simulationResult;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var table = (LootTableSO)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Pool Weight Visualization", EditorStyles.boldLabel);

            if (table.Pools != null && table.Pools.Count > 0)
            {
                float totalWeight = 0f;
                foreach (var pool in table.Pools)
                {
                    if (pool != null)
                        totalWeight += pool.PoolWeight;
                }

                foreach (var pool in table.Pools)
                {
                    if (pool == null) continue;
                    float pct = totalWeight > 0 ? pool.PoolWeight / totalWeight : 0f;
                    var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(rect, pct, $"{pool.name}: {pct:P1} ({pool.Entries?.Count ?? 0} entries)");
                }
            }

            EditorGUILayout.Space(10);
            _showSimulation = EditorGUILayout.Foldout(_showSimulation, "Drop Simulation");
            if (_showSimulation)
            {
                _simulationCount = EditorGUILayout.IntField("Simulation Count", _simulationCount);
                _simulationCount = Mathf.Clamp(_simulationCount, 1, 100000);

                if (GUILayout.Button($"Simulate {_simulationCount} Drops"))
                {
                    RunSimulation(table);
                }

                if (!string.IsNullOrEmpty(_simulationResult))
                {
                    EditorGUILayout.HelpBox(_simulationResult, MessageType.Info);
                }
            }
        }

        private void RunSimulation(LootTableSO table)
        {
            var dropCounts = new System.Collections.Generic.Dictionary<string, int>();
            int totalDrops = 0;
            int emptyRolls = 0;

            for (int i = 0; i < _simulationCount; i++)
            {
                var results = new NativeList<LootDrop>(8, Allocator.Temp);
                var context = new LootContext
                {
                    Level = 1,
                    DifficultyMultiplier = 1f,
                    LuckModifier = 0f,
                    RandomSeed = (uint)(i + 1)
                };

                LootTableResolver.Resolve(table, context, ref results);

                if (results.Length == 0)
                {
                    emptyRolls++;
                }
                else
                {
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
                            dropCounts[key] = 0;
                        dropCounts[key] += drop.Quantity;
                        totalDrops++;
                    }
                }

                results.Dispose();
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Results from {_simulationCount} rolls:");
            sb.AppendLine($"  Total drops: {totalDrops} ({(float)totalDrops / _simulationCount:F2} avg/roll)");
            sb.AppendLine($"  Empty rolls: {emptyRolls} ({(float)emptyRolls / _simulationCount:P1})");
            sb.AppendLine("---");

            foreach (var kvp in dropCounts)
            {
                float avgQty = (float)kvp.Value / _simulationCount;
                sb.AppendLine($"  {kvp.Key}: {kvp.Value} total ({avgQty:F2} avg)");
            }

            _simulationResult = sb.ToString();
        }
    }
}
#endif
