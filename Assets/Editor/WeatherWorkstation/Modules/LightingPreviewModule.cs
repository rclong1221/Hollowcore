using UnityEditor;
using UnityEngine;

namespace DIG.Weather.Editor.Modules
{
    public class LightingPreviewModule : IWeatherWorkstationModule
    {
        private DayNightConfigSO _config;
        private float _previewTime = 12f;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Lighting Preview", EditorStyles.boldLabel);

            _config = (DayNightConfigSO)EditorGUILayout.ObjectField(
                "Day-Night Config", _config, typeof(DayNightConfigSO), false);

            if (_config == null)
            {
                _config = Resources.Load<DayNightConfigSO>("DayNightConfig");
                if (_config == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign a DayNightConfigSO or place one at Resources/DayNightConfig.",
                        MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(4);

            _previewTime = EditorGUILayout.Slider("Preview Time", _previewTime, 0f, 24f);
            float t = _previewTime / 24f;

            EditorGUILayout.Space(8);

            // Sun color gradient preview
            EditorGUILayout.LabelField("Sun Color Gradient", EditorStyles.miniLabel);
            if (_config.SunColorGradient != null)
            {
                var rect = GUILayoutUtility.GetRect(400, 30, GUILayout.ExpandWidth(true));
                DrawGradientBar(rect, _config.SunColorGradient, t);
            }

            EditorGUILayout.Space(4);

            // Ambient color gradient preview
            EditorGUILayout.LabelField("Ambient Color Gradient", EditorStyles.miniLabel);
            if (_config.AmbientColorGradient != null)
            {
                var rect = GUILayoutUtility.GetRect(400, 30, GUILayout.ExpandWidth(true));
                DrawGradientBar(rect, _config.AmbientColorGradient, t);
            }

            EditorGUILayout.Space(4);

            // Sun intensity curve preview
            EditorGUILayout.LabelField("Sun Intensity Curve", EditorStyles.miniLabel);
            if (_config.SunIntensityCurve != null)
            {
                var rect = GUILayoutUtility.GetRect(400, 60, GUILayout.ExpandWidth(true));
                EditorGUI.CurveField(rect, _config.SunIntensityCurve);
            }

            EditorGUILayout.Space(4);

            // Star visibility curve
            EditorGUILayout.LabelField("Star Visibility Curve", EditorStyles.miniLabel);
            if (_config.StarVisibilityCurve != null)
            {
                var rect = GUILayoutUtility.GetRect(400, 60, GUILayout.ExpandWidth(true));
                EditorGUI.CurveField(rect, _config.StarVisibilityCurve);
            }

            EditorGUILayout.Space(8);

            // Current values at preview time
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Time: {FormatTime(_previewTime)}", EditorStyles.boldLabel);

            if (_config.SunColorGradient != null)
            {
                var sc = _config.SunColorGradient.Evaluate(t);
                EditorGUILayout.ColorField("Sun Color", sc);
            }
            if (_config.AmbientColorGradient != null)
            {
                var ac = _config.AmbientColorGradient.Evaluate(t);
                EditorGUILayout.ColorField("Ambient Color", ac);
            }
            if (_config.SunIntensityCurve != null)
                EditorGUILayout.LabelField($"Sun Intensity: {_config.SunIntensityCurve.Evaluate(t):F2}");
            if (_config.StarVisibilityCurve != null)
                EditorGUILayout.LabelField($"Star Visibility: {_config.StarVisibilityCurve.Evaluate(t):F2}");

            EditorGUILayout.EndVertical();

            // Edit-mode preview button
            EditorGUILayout.Space(8);
            if (!Application.isPlaying && GUILayout.Button("Apply Preview to Scene"))
            {
                ApplyPreviewToScene(t);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawGradientBar(Rect rect, Gradient gradient, float marker)
        {
            // Draw gradient bar
            int steps = (int)rect.width;
            for (int i = 0; i < steps; i++)
            {
                float gt = i / (float)steps;
                EditorGUI.DrawRect(
                    new Rect(rect.x + i, rect.y, 1, rect.height),
                    gradient.Evaluate(gt));
            }

            // Draw marker
            float markerX = rect.x + marker * rect.width;
            EditorGUI.DrawRect(new Rect(markerX - 1, rect.y, 3, rect.height), Color.white);
        }

        private static string FormatTime(float hour)
        {
            int h = (int)hour;
            int m = (int)((hour - h) * 60);
            return $"{h:D2}:{m:D2}";
        }

        private void ApplyPreviewToScene(float t)
        {
            var light = Object.FindAnyObjectByType<Light>();
            if (light != null && _config != null)
            {
                if (_config.SunColorGradient != null)
                    light.color = _config.SunColorGradient.Evaluate(t);
                if (_config.SunIntensityCurve != null)
                    light.intensity = _config.SunIntensityCurve.Evaluate(t);

                float sunAngle = Mathf.Sin(t * Mathf.PI) * _config.SunPitchMax;
                float sunYaw = t * 360f - 90f;
                light.transform.rotation = Quaternion.Euler(sunAngle, sunYaw, 0f);

                if (_config.AmbientColorGradient != null)
                    RenderSettings.ambientLight = _config.AmbientColorGradient.Evaluate(t);

                SceneView.RepaintAll();
            }
        }
    }
}
