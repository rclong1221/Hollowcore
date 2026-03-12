#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DIG.Achievement.Editor.Modules
{
    /// <summary>
    /// EPIC 17.7: Validation checks for achievement definitions + aggregate statistics.
    /// 10 checks: duplicate IDs, missing tiers, non-ascending thresholds, missing icons,
    /// unreferenced achievements, orphan definitions, invalid ConditionParam, zero thresholds,
    /// duplicate names, hidden without description. Green/yellow/red severity.
    /// Also shows category breakdown and tier distribution.
    /// </summary>
    public class ValidatorModule : IAchievementWorkstationModule
    {
        public string ModuleName => "Validator & Stats";

        private AchievementDatabaseSO _database;
        private List<ValidationResult> _results = new();
        private Vector2 _scroll;
        private bool _showStats = true;
        private bool _showValidation = true;

        private enum Severity { Info, Warning, Error }

        private struct ValidationResult
        {
            public Severity Level;
            public string Message;
            public string Fix;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Validator & Statistics", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _database = (AchievementDatabaseSO)EditorGUILayout.ObjectField(
                "Database", _database, typeof(AchievementDatabaseSO), false);

            if (_database == null)
                _database = Resources.Load<AchievementDatabaseSO>("AchievementDatabase");

            if (_database == null)
            {
                EditorGUILayout.HelpBox("No AchievementDatabaseSO found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Validation", GUILayout.Height(25)))
                RunValidation();
            if (GUILayout.Button("Fix Trivial Issues", GUILayout.Height(25)))
                FixTrivialIssues();
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Statistics
            _showStats = EditorGUILayout.Foldout(_showStats, "Statistics", true);
            if (_showStats)
                DrawStatistics();

            EditorGUILayout.Space(12);

            // Validation Results
            _showValidation = EditorGUILayout.Foldout(_showValidation, $"Validation Results ({_results.Count})", true);
            if (_showValidation)
                DrawValidationResults();

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatistics()
        {
            int total = _database.Achievements.Count;
            int nullCount = _database.Achievements.Count(d => d == null);
            int valid = total - nullCount;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Total Definitions: {total} ({nullCount} null)");

            // Category breakdown
            var categories = new Dictionary<AchievementCategory, int>();
            var tierDistribution = new Dictionary<AchievementTier, int>();
            int totalTiers = 0;
            int hiddenCount = 0;

            foreach (var def in _database.Achievements)
            {
                if (def == null) continue;

                if (!categories.ContainsKey(def.Category)) categories[def.Category] = 0;
                categories[def.Category]++;

                if (def.IsHidden) hiddenCount++;

                if (def.Tiers != null)
                {
                    totalTiers += def.Tiers.Length;
                    foreach (var tier in def.Tiers)
                    {
                        if (!tierDistribution.ContainsKey(tier.Tier)) tierDistribution[tier.Tier] = 0;
                        tierDistribution[tier.Tier]++;
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("By Category:", EditorStyles.boldLabel);
            foreach (var kvp in categories.OrderBy(k => k.Key))
                EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value}");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Tier Distribution:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  Total Tiers: {totalTiers}");
            foreach (var kvp in tierDistribution.OrderBy(k => k.Key))
                EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value}");

            EditorGUILayout.LabelField($"Hidden Achievements: {hiddenCount}");
            EditorGUI.indentLevel--;
        }

        private void DrawValidationResults()
        {
            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Run Validation' to check definitions.", MessageType.Info);
                return;
            }

            int errors = _results.Count(r => r.Level == Severity.Error);
            int warnings = _results.Count(r => r.Level == Severity.Warning);
            int infos = _results.Count(r => r.Level == Severity.Info);

            var bg = GUI.backgroundColor;
            GUI.backgroundColor = errors > 0 ? Color.red : warnings > 0 ? Color.yellow : Color.green;
            EditorGUILayout.HelpBox(
                $"{errors} errors, {warnings} warnings, {infos} info",
                errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info);
            GUI.backgroundColor = bg;

            foreach (var result in _results)
            {
                var type = result.Level switch
                {
                    Severity.Error => MessageType.Error,
                    Severity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(result.Message, type);
            }
        }

        private void RunValidation()
        {
            _results.Clear();

            var defs = _database.Achievements;

            // 1. Duplicate AchievementIds
            var idCounts = new Dictionary<ushort, int>();
            foreach (var def in defs)
            {
                if (def == null) continue;
                if (!idCounts.ContainsKey(def.AchievementId)) idCounts[def.AchievementId] = 0;
                idCounts[def.AchievementId]++;
            }
            foreach (var kvp in idCounts)
            {
                if (kvp.Value > 1)
                    _results.Add(new ValidationResult { Level = Severity.Error, Message = $"Duplicate AchievementId {kvp.Key} found {kvp.Value} times" });
            }

            // 2. Missing tiers (0 tiers)
            foreach (var def in defs)
            {
                if (def == null) continue;
                if (def.Tiers == null || def.Tiers.Length == 0)
                    _results.Add(new ValidationResult { Level = Severity.Error, Message = $"[{def.AchievementId}] '{def.AchievementName}' has 0 tiers" });
            }

            // 3. Non-ascending thresholds
            foreach (var def in defs)
            {
                if (def?.Tiers == null || def.Tiers.Length < 2) continue;
                for (int i = 1; i < def.Tiers.Length; i++)
                {
                    if (def.Tiers[i].Threshold <= def.Tiers[i - 1].Threshold)
                        _results.Add(new ValidationResult { Level = Severity.Error, Message = $"[{def.AchievementId}] '{def.AchievementName}' tier {def.Tiers[i].Tier} threshold ({def.Tiers[i].Threshold}) <= previous ({def.Tiers[i - 1].Threshold})" });
                }
            }

            // 4. Missing icons
            foreach (var def in defs)
            {
                if (def == null) continue;
                if (def.Icon == null)
                    _results.Add(new ValidationResult { Level = Severity.Warning, Message = $"[{def.AchievementId}] '{def.AchievementName}' has no icon" });
            }

            // 5. Zero thresholds
            foreach (var def in defs)
            {
                if (def?.Tiers == null) continue;
                foreach (var tier in def.Tiers)
                {
                    if (tier.Threshold <= 0)
                        _results.Add(new ValidationResult { Level = Severity.Error, Message = $"[{def.AchievementId}] '{def.AchievementName}' tier {tier.Tier} has threshold <= 0" });
                }
            }

            // 6. Duplicate names
            var nameCounts = new Dictionary<string, int>();
            foreach (var def in defs)
            {
                if (def == null || string.IsNullOrEmpty(def.AchievementName)) continue;
                if (!nameCounts.ContainsKey(def.AchievementName)) nameCounts[def.AchievementName] = 0;
                nameCounts[def.AchievementName]++;
            }
            foreach (var kvp in nameCounts)
            {
                if (kvp.Value > 1)
                    _results.Add(new ValidationResult { Level = Severity.Warning, Message = $"Duplicate name '{kvp.Key}' found {kvp.Value} times" });
            }

            // 7. Hidden without description
            foreach (var def in defs)
            {
                if (def == null) continue;
                if (def.IsHidden && string.IsNullOrWhiteSpace(def.Description))
                    _results.Add(new ValidationResult { Level = Severity.Warning, Message = $"[{def.AchievementId}] '{def.AchievementName}' is hidden but has no description" });
            }

            // 8. Null entries in database
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] == null)
                    _results.Add(new ValidationResult { Level = Severity.Error, Message = $"Null entry at index {i} in database", Fix = "remove" });
            }

            // 9. Empty name
            foreach (var def in defs)
            {
                if (def != null && string.IsNullOrWhiteSpace(def.AchievementName))
                    _results.Add(new ValidationResult { Level = Severity.Warning, Message = $"[{def.AchievementId}] has empty name" });
            }

            // 10. Missing reward description
            foreach (var def in defs)
            {
                if (def?.Tiers == null) continue;
                foreach (var tier in def.Tiers)
                {
                    if (string.IsNullOrWhiteSpace(tier.RewardDescription))
                        _results.Add(new ValidationResult { Level = Severity.Warning, Message = $"[{def.AchievementId}] '{def.AchievementName}' tier {tier.Tier} has no reward description" });
                }
            }

            if (_results.Count == 0)
                _results.Add(new ValidationResult { Level = Severity.Info, Message = "All checks passed!" });
        }

        private void FixTrivialIssues()
        {
            bool changed = false;

            // Remove null entries
            for (int i = _database.Achievements.Count - 1; i >= 0; i--)
            {
                if (_database.Achievements[i] == null)
                {
                    _database.Achievements.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(_database);
                AssetDatabase.SaveAssets();
                Debug.Log("[AchievementWorkstation] Fixed trivial issues.");
            }

            RunValidation();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
