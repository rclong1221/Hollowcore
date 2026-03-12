#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// Encounter Pool Editor module.
    /// Edit EncounterPoolSO assets inline — enemy prefabs, spawn costs, weight sliders,
    /// difficulty-gated entries, elite eligibility, and seed-based spawn preview.
    /// </summary>
    public class EncounterPoolModule : IRunWorkstationModule
    {
        public string TabName => "Encounter Pools";

        private EncounterPoolSO _encounterPool;
        private SpawnDirectorConfigSO _directorConfig;
        private UnityEditor.Editor _poolEditor;
        private UnityEditor.Editor _directorEditor;
        private Vector2 _scrollPos;
        private uint _previewSeed = 12345;
        private float _previewDifficulty = 1f;
        private float _previewBudget = 200f;
        private bool _showPreview;
        private bool _showWeights = true;
        private bool _showDirectorConfig;
        private bool _showCostBreakdown = true;

        // Cached display data — rebuilt on pool change
        private EncounterPoolSO _cachedPool;
        private int _cachedEntryCount;
        private string[] _cachedLabels;
        private string[] _cachedWeightLabels;
        private string[] _cachedCostLabels;
        private float[] _cachedPcts;
        private string _cachedSummary;

        public void OnEnable() { }
        public void OnDisable()
        {
            if (_poolEditor != null)
                Object.DestroyImmediate(_poolEditor);
            if (_directorEditor != null)
                Object.DestroyImmediate(_directorEditor);
        }

        private void RebuildDisplayCache()
        {
            if (_encounterPool == null || _encounterPool.Entries == null || _encounterPool.Entries.Count == 0)
            {
                _cachedPool = _encounterPool;
                _cachedEntryCount = 0;
                return;
            }

            int count = _encounterPool.Entries.Count;

            // Check if cache is still valid
            if (_cachedPool == _encounterPool && _cachedEntryCount == count
                && _cachedLabels != null && _cachedLabels.Length == count)
            {
                // Quick dirty check: re-validate only if SO was modified
                if (!EditorUtility.IsDirty(_encounterPool))
                    return;
            }

            _cachedPool = _encounterPool;
            _cachedEntryCount = count;
            _cachedLabels = new string[count];
            _cachedWeightLabels = new string[count];
            _cachedCostLabels = new string[count];
            _cachedPcts = new float[count];

            float totalWeight = 0f;
            int cheapest = int.MaxValue, mostExpensive = 0;

            for (int i = 0; i < count; i++)
                totalWeight += _encounterPool.Entries[i].Weight;
            if (totalWeight <= 0f) totalWeight = 1f;

            for (int i = 0; i < count; i++)
            {
                var entry = _encounterPool.Entries[i];
                float pct = entry.Weight / totalWeight;
                _cachedPcts[i] = pct;

                string label = !string.IsNullOrEmpty(entry.DisplayName)
                    ? entry.DisplayName
                    : entry.EnemyPrefab != null ? entry.EnemyPrefab.name : $"Entry {i}";

                _cachedLabels[i] = label;
                _cachedWeightLabels[i] = $"  {label}: {pct:P1} (cost {entry.SpawnCost})";

                string diffRange = entry.MaxDifficulty > 0
                    ? $"{entry.MinDifficulty:F1}-{entry.MaxDifficulty:F1}"
                    : $"{entry.MinDifficulty:F1}+";
                string elite = entry.CanBeElite ? "Yes" : "No";
                string maxAlive = entry.MaxAlive > 0 ? entry.MaxAlive.ToString() : "Unlimited";
                _cachedCostLabels[i] = $"{entry.SpawnCost} | {diffRange} | {elite} | {maxAlive}";

                int cost = entry.SpawnCost;
                if (cost < cheapest) cheapest = cost;
                if (cost > mostExpensive) mostExpensive = cost;
            }

            _cachedSummary = $"  Cheapest: {cheapest}  |  Most expensive: {mostExpensive}  |  Entries: {count}";
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Encounter Pool Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _encounterPool = (EncounterPoolSO)EditorGUILayout.ObjectField(
                "Encounter Pool", _encounterPool, typeof(EncounterPoolSO), false);

            if (_encounterPool == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an EncounterPoolSO to edit its entries.\n" +
                    "Create one via Assets > Create > DIG > Roguelite > Encounter Pool.",
                    MessageType.Info);
                return;
            }

            // Rebuild cached display strings if pool changed or was modified
            RebuildDisplayCache();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Weight distribution
            _showWeights = EditorGUILayout.Foldout(_showWeights, "Weight Distribution", true);
            if (_showWeights)
                DrawWeightBars();

            EditorGUILayout.Space(4);

            // Cost breakdown
            _showCostBreakdown = EditorGUILayout.Foldout(_showCostBreakdown, "Cost & Difficulty Breakdown", true);
            if (_showCostBreakdown)
                DrawCostBreakdown();

            EditorGUILayout.Space(8);

            // Director config
            _showDirectorConfig = EditorGUILayout.Foldout(_showDirectorConfig, "Spawn Director Config", true);
            if (_showDirectorConfig)
            {
                EditorGUI.indentLevel++;
                _directorConfig = (SpawnDirectorConfigSO)EditorGUILayout.ObjectField(
                    "Director Config", _directorConfig, typeof(SpawnDirectorConfigSO), false);
                if (_directorConfig != null)
                {
                    if (_directorEditor == null || _directorEditor.target != _directorConfig)
                    {
                        if (_directorEditor != null)
                            Object.DestroyImmediate(_directorEditor);
                        _directorEditor = UnityEditor.Editor.CreateEditor(_directorConfig);
                    }
                    _directorEditor.OnInspectorGUI();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // Pool inspector
            if (_poolEditor == null || _poolEditor.target != _encounterPool)
            {
                if (_poolEditor != null)
                    Object.DestroyImmediate(_poolEditor);
                _poolEditor = UnityEditor.Editor.CreateEditor(_encounterPool);
            }
            _poolEditor.OnInspectorGUI();

            EditorGUILayout.Space(8);

            // Spawn preview
            _showPreview = EditorGUILayout.Foldout(_showPreview, "Spawn Preview", true);
            if (_showPreview)
                DrawSpawnPreview();

            EditorGUILayout.EndScrollView();
        }

        private void DrawWeightBars()
        {
            EditorGUI.indentLevel++;

            if (_cachedEntryCount == 0)
            {
                EditorGUILayout.LabelField("No entries.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            for (int i = 0; i < _cachedEntryCount; i++)
            {
                var entry = _encounterPool.Entries[i];
                var rect = EditorGUILayout.GetControlRect(false, 20);
                var barRect = new Rect(rect.x, rect.y + 1, rect.width * _cachedPcts[i], rect.height - 2);

                Color barColor = entry.CanBeElite
                    ? new Color(0.8f, 0.5f, 0.2f, 0.5f)
                    : new Color(0.4f, 0.6f, 0.9f, 0.5f);
                EditorGUI.DrawRect(barRect, barColor);
                EditorGUI.LabelField(rect, _cachedWeightLabels[i]);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawCostBreakdown()
        {
            EditorGUI.indentLevel++;

            if (_cachedEntryCount == 0)
            {
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUILayout.LabelField("Entry", "Cost | Diff Range | Elite | Max Alive", EditorStyles.miniLabel);

            for (int i = 0; i < _cachedEntryCount; i++)
                EditorGUILayout.LabelField($"  {_cachedLabels[i]}", _cachedCostLabels[i]);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(_cachedSummary, EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
        }

        private void DrawSpawnPreview()
        {
            EditorGUI.indentLevel++;

            _previewSeed = (uint)EditorGUILayout.IntField("Seed", (int)_previewSeed);
            _previewDifficulty = EditorGUILayout.Slider("Effective Difficulty", _previewDifficulty, 0.5f, 10f);
            _previewBudget = EditorGUILayout.Slider("Spawn Budget", _previewBudget, 10f, 1000f);

            if (GUILayout.Button("Simulate Spawn Wave"))
            {
                var rng = new Unity.Mathematics.Random(_previewSeed | 1);
                float budget = _previewBudget;
                int spawned = 0;

                Debug.Log($"[EncounterPool Preview] Seed={_previewSeed}, Difficulty={_previewDifficulty:F1}, Budget={_previewBudget:F0}");

                while (budget > 0 && spawned < 50)
                {
                    // Filter entries by difficulty
                    float totalWeight = 0f;
                    for (int i = 0; i < _encounterPool.Entries.Count; i++)
                    {
                        var e = _encounterPool.Entries[i];
                        if (e.MinDifficulty > 0 && _previewDifficulty < e.MinDifficulty) continue;
                        if (e.MaxDifficulty > 0 && _previewDifficulty > e.MaxDifficulty) continue;
                        if (e.SpawnCost > budget) continue;
                        totalWeight += e.Weight;
                    }

                    if (totalWeight <= 0f) break;

                    float roll = rng.NextFloat() * totalWeight;
                    float acc = 0f;
                    int selected = -1;

                    for (int i = 0; i < _encounterPool.Entries.Count; i++)
                    {
                        var e = _encounterPool.Entries[i];
                        if (e.MinDifficulty > 0 && _previewDifficulty < e.MinDifficulty) continue;
                        if (e.MaxDifficulty > 0 && _previewDifficulty > e.MaxDifficulty) continue;
                        if (e.SpawnCost > budget) continue;

                        acc += e.Weight;
                        if (roll <= acc) { selected = i; break; }
                    }

                    if (selected < 0) break;

                    var entry = _encounterPool.Entries[selected];
                    string name = !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName : $"Entry {selected}";
                    budget -= entry.SpawnCost;
                    spawned++;
                    Debug.Log($"  #{spawned}: {name} (cost={entry.SpawnCost}, remaining={budget:F0})");
                }

                Debug.Log($"  Total spawned: {spawned}, remaining budget: {budget:F0}");
            }

            EditorGUI.indentLevel--;
        }
    }
}
#endif
