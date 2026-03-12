#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Music.Editor
{
    /// <summary>
    /// EPIC 17.5: Visual editor for intensity weight configuration.
    /// Preview intensity output for N enemies at each alert level.
    /// </summary>
    public class IntensityCurveModule : IMusicWorkstationModule
    {
        public string ModuleName => "Intensity Curve";

        private MusicConfigSO _config;

        // Simulation inputs
        private int _combatCount;
        private int _searchingCount;
        private int _suspiciousCount;
        private int _curiousCount;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Intensity Curve Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _config = EditorGUILayout.ObjectField("Config", _config, typeof(MusicConfigSO), false) as MusicConfigSO;

            if (_config == null)
            {
                _config = Resources.Load<MusicConfigSO>("MusicConfig");
                if (_config == null)
                {
                    EditorGUILayout.HelpBox("Assign a MusicConfigSO or create one at Resources/MusicConfig.", MessageType.Info);
                    return;
                }
            }

            // Weight configuration
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Alert Level Weights", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _config.IntensityWeightCombat = EditorGUILayout.Slider("COMBAT weight", _config.IntensityWeightCombat, 0f, 2f);
            _config.IntensityWeightSearching = EditorGUILayout.Slider("SEARCHING weight", _config.IntensityWeightSearching, 0f, 1f);
            _config.IntensityWeightSuspicious = EditorGUILayout.Slider("SUSPICIOUS weight", _config.IntensityWeightSuspicious, 0f, 1f);
            _config.IntensityWeightCurious = EditorGUILayout.Slider("CURIOUS weight", _config.IntensityWeightCurious, 0f, 0.5f);
            _config.MaxIntensityContributors = EditorGUILayout.IntSlider("Max Contributors", _config.MaxIntensityContributors, 1, 20);
            _config.CombatFadeSpeed = EditorGUILayout.Slider("Fade Speed", _config.CombatFadeSpeed, 0.1f, 10f);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_config);

            // Simulation
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Intensity Simulator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Adjust enemy counts below to preview resulting intensity.", MessageType.None);

            _combatCount = EditorGUILayout.IntSlider("COMBAT enemies", _combatCount, 0, 10);
            _searchingCount = EditorGUILayout.IntSlider("SEARCHING enemies", _searchingCount, 0, 10);
            _suspiciousCount = EditorGUILayout.IntSlider("SUSPICIOUS enemies", _suspiciousCount, 0, 10);
            _curiousCount = EditorGUILayout.IntSlider("CURIOUS enemies", _curiousCount, 0, 10);

            // Calculate
            float rawIntensity = _combatCount * _config.IntensityWeightCombat
                + _searchingCount * _config.IntensityWeightSearching
                + _suspiciousCount * _config.IntensityWeightSuspicious
                + _curiousCount * _config.IntensityWeightCurious;

            int totalContributors = _combatCount + _searchingCount + _suspiciousCount + _curiousCount;
            int capped = Mathf.Min(totalContributors, _config.MaxIntensityContributors);
            float cappedRaw = rawIntensity;
            if (totalContributors > _config.MaxIntensityContributors)
            {
                // Scale proportionally
                cappedRaw = rawIntensity * ((float)_config.MaxIntensityContributors / totalContributors);
            }

            float finalIntensity = Mathf.Clamp01(cappedRaw / _config.MaxIntensityContributors);

            // Display results
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Raw sum: {rawIntensity:F2} | Contributors: {totalContributors} (capped: {capped})");

            var rect = GUILayoutUtility.GetRect(200, 24);
            EditorGUI.ProgressBar(rect, finalIntensity, $"Final Intensity: {finalIntensity:P0}");

            // Show which stems would be active
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Stem Activation (default thresholds):", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  Base: ALWAYS | Perc (>=0.2): {(finalIntensity >= 0.2f ? "ON" : "off")} | Melody (>=0.5): {(finalIntensity >= 0.5f ? "ON" : "off")} | Intensity (>=0.8): {(finalIntensity >= 0.8f ? "ON" : "off")}");
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
