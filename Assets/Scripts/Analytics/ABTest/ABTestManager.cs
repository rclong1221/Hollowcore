using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Analytics
{
    /// <summary>
    /// Deterministic A/B test variant assignment.
    /// Persists assignments in PlayerPrefs for session consistency.
    /// </summary>
    public static class ABTestManager
    {
        private static Dictionary<string, string> _assignments = new();
        private static Dictionary<string, string> _overrides = new();
        private static ABTestConfig[] _activeTests;
        private static HashSet<string> _enabledFeatures = new(StringComparer.OrdinalIgnoreCase);

        private const string PrefsPrefix = "ab_test_";
        private const string OverrideSuffix = "_override";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _assignments = new Dictionary<string, string>();
            _overrides = new Dictionary<string, string>();
            _activeTests = null;
            _enabledFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public static void Initialize(ABTestConfig[] tests, string playerId)
        {
            _activeTests = tests ?? Array.Empty<ABTestConfig>();
            _assignments.Clear();
            _overrides.Clear();
            _enabledFeatures.Clear();

            foreach (var test in _activeTests)
            {
                if (test == null || !test.IsActive || string.IsNullOrEmpty(test.TestId))
                    continue;

                if (!IsWithinDateRange(test))
                    continue;

                string overrideKey = PrefsPrefix + test.TestId + OverrideSuffix;
                if (PlayerPrefs.HasKey(overrideKey))
                {
                    string ov = PlayerPrefs.GetString(overrideKey);
                    _overrides[test.TestId] = ov;
                    _assignments[test.TestId] = ov;
                    ApplyFeatureFlags(test, ov);
                    continue;
                }

                string prefsKey = PrefsPrefix + test.TestId;
                if (PlayerPrefs.HasKey(prefsKey))
                {
                    string stored = PlayerPrefs.GetString(prefsKey);
                    if (IsValidVariant(test, stored))
                    {
                        _assignments[test.TestId] = stored;
                        ApplyFeatureFlags(test, stored);
                        continue;
                    }
                }

                string variant = AssignVariant(test, playerId);
                _assignments[test.TestId] = variant;
                PlayerPrefs.SetString(prefsKey, variant);
                ApplyFeatureFlags(test, variant);
            }

            PlayerPrefs.Save();
        }

        public static bool IsVariant(string testId, string variantName)
        {
            return _assignments.TryGetValue(testId, out var assigned)
                   && string.Equals(assigned, variantName, StringComparison.Ordinal);
        }

        public static string GetVariant(string testId)
        {
            return _assignments.TryGetValue(testId, out var v) ? v : null;
        }

        public static Dictionary<string, string> GetAllAssignments()
        {
            return new Dictionary<string, string>(_assignments);
        }

        public static void ForceVariant(string testId, string variantName)
        {
            _overrides[testId] = variantName;
            _assignments[testId] = variantName;
            PlayerPrefs.SetString(PrefsPrefix + testId + OverrideSuffix, variantName);
            PlayerPrefs.Save();
            RebuildFeatureFlags();
        }

        public static void ClearOverride(string testId)
        {
            _overrides.Remove(testId);
            string key = PrefsPrefix + testId + OverrideSuffix;
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        public static bool IsFeatureEnabled(string featureFlagKey)
        {
            return _enabledFeatures.Contains(featureFlagKey);
        }

        private static string AssignVariant(ABTestConfig test, string playerId)
        {
            if (test.Variants == null || test.Variants.Length == 0)
                return "control";

            int hash = StableHash(playerId + test.TestId);
            float totalWeight = 0f;
            foreach (var v in test.Variants)
                totalWeight += v.Weight;

            if (totalWeight <= 0f)
                return test.Variants[0].VariantName;

            float target = (float)((uint)hash % 10000) / 10000f * totalWeight;
            float cumulative = 0f;
            foreach (var v in test.Variants)
            {
                cumulative += v.Weight;
                if (target < cumulative)
                    return v.VariantName;
            }

            return test.Variants[^1].VariantName;
        }

        private static int StableHash(string input)
        {
            unchecked
            {
                int hash = 5381;
                foreach (char c in input)
                    hash = ((hash << 5) + hash) ^ c;
                return hash;
            }
        }

        private static bool IsValidVariant(ABTestConfig test, string variantName)
        {
            if (test.Variants == null) return false;
            foreach (var v in test.Variants)
            {
                if (string.Equals(v.VariantName, variantName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsWithinDateRange(ABTestConfig test)
        {
            var now = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(test.StartDate) &&
                DateTime.TryParse(test.StartDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out var start) &&
                now < start)
                return false;

            if (!string.IsNullOrEmpty(test.EndDate) &&
                DateTime.TryParse(test.EndDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out var end) &&
                now > end)
                return false;

            return true;
        }

        private static void ApplyFeatureFlags(ABTestConfig test, string variantName)
        {
            if (test.Variants == null) return;
            foreach (var v in test.Variants)
            {
                if (string.Equals(v.VariantName, variantName, StringComparison.Ordinal) && v.FeatureFlags != null)
                {
                    foreach (var ff in v.FeatureFlags)
                    {
                        if (!string.IsNullOrEmpty(ff))
                            _enabledFeatures.Add(ff);
                    }
                    break;
                }
            }
        }

        private static void RebuildFeatureFlags()
        {
            _enabledFeatures.Clear();
            if (_activeTests == null) return;
            foreach (var test in _activeTests)
            {
                if (test == null || !test.IsActive) continue;
                if (_assignments.TryGetValue(test.TestId, out var variant))
                    ApplyFeatureFlags(test, variant);
            }
        }
    }
}
