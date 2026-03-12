using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Analytics.Editor.Modules
{
    /// <summary>
    /// List active A/B tests with current variant assignments.
    /// Override dropdowns, randomize all, show feature flags, export JSON.
    /// </summary>
    public class ABTestOverrideModule : IAnalyticsWorkstationModule
    {
        private Vector2 _scroll;
        private ABTestConfig[] _cachedTests;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("A/B Test Override", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to manage A/B test overrides.", MessageType.Info);
                return;
            }

            var assignments = ABTestManager.GetAllAssignments();

            if (assignments.Count == 0)
            {
                EditorGUILayout.HelpBox("No active A/B tests. Add ABTestConfig assets to Resources/ABTests/.", MessageType.Info);
                return;
            }

            _cachedTests ??= Resources.LoadAll<ABTestConfig>("ABTests");

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Randomize All", GUILayout.Height(25)))
            {
                foreach (var test in _cachedTests)
                {
                    if (test == null || !test.IsActive || test.Variants == null || test.Variants.Length == 0) continue;
                    int idx = Random.Range(0, test.Variants.Length);
                    ABTestManager.ForceVariant(test.TestId, test.Variants[idx].VariantName);
                }
            }

            if (GUILayout.Button("Clear All Overrides", GUILayout.Height(25)))
            {
                foreach (var test in _cachedTests)
                {
                    if (test != null)
                        ABTestManager.ClearOverride(test.TestId);
                }
            }

            if (GUILayout.Button("Export JSON", GUILayout.Height(25)))
            {
                var json = new System.Text.StringBuilder();
                json.Append('{');
                bool first = true;
                foreach (var kv in assignments)
                {
                    if (!first) json.Append(',');
                    first = false;
                    json.Append($"\"{kv.Key}\":\"{kv.Value}\"");
                }
                json.Append('}');
                EditorGUIUtility.systemCopyBuffer = json.ToString();
                Debug.Log($"[Analytics] AB assignments copied: {json}");
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var test in _cachedTests)
            {
                if (test == null || !test.IsActive) continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Test: {test.TestId}", EditorStyles.boldLabel);

                string currentVariant = ABTestManager.GetVariant(test.TestId) ?? "(none)";
                EditorGUILayout.LabelField($"Current Variant: {currentVariant}");

                if (test.Variants != null && test.Variants.Length > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Override:", GUILayout.Width(60));
                    foreach (var v in test.Variants)
                    {
                        bool isCurrent = string.Equals(currentVariant, v.VariantName);
                        var prevBg = GUI.backgroundColor;
                        if (isCurrent) GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);

                        if (GUILayout.Button(v.VariantName, GUILayout.MinWidth(80)))
                        {
                            ABTestManager.ForceVariant(test.TestId, v.VariantName);
                        }

                        GUI.backgroundColor = prevBg;
                    }
                    EditorGUILayout.EndHorizontal();

                    // Feature flags
                    string variant = ABTestManager.GetVariant(test.TestId);
                    if (variant != null)
                    {
                        foreach (var v in test.Variants)
                        {
                            if (string.Equals(v.VariantName, variant) && v.FeatureFlags != null && v.FeatureFlags.Length > 0)
                            {
                                EditorGUILayout.LabelField($"  Feature Flags: {string.Join(", ", v.FeatureFlags)}", EditorStyles.miniLabel);
                                break;
                            }
                        }
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
