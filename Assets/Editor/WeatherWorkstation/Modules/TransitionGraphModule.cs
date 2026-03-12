using UnityEditor;
using UnityEngine;

namespace DIG.Weather.Editor.Modules
{
    public class TransitionGraphModule : IWeatherWorkstationModule
    {
        private Season _selectedSeason = Season.Summer;
        private WeatherConfigSO _config;
        private Vector2 _scroll;

        private static readonly string[] WeatherNames =
        {
            "Clear", "PartCloudy", "Cloudy", "LightRain", "HeavyRain",
            "Thunder", "LightSnow", "HeavySnow", "Fog", "Sandstorm"
        };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Transition Graph", EditorStyles.boldLabel);

            _config = (WeatherConfigSO)EditorGUILayout.ObjectField(
                "Weather Config", _config, typeof(WeatherConfigSO), false);

            if (_config == null)
            {
                _config = Resources.Load<WeatherConfigSO>("WeatherConfig");
                if (_config == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign a WeatherConfigSO or place one at Resources/WeatherConfig.",
                        MessageType.Info);
                    return;
                }
            }

            _selectedSeason = (Season)EditorGUILayout.EnumPopup("Season", _selectedSeason);

            EditorGUILayout.Space(8);

            // Draw transition weight table
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Header row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("From \\ To", GUILayout.Width(80));
            for (int to = 0; to < 10; to++)
                EditorGUILayout.LabelField(WeatherNames[to], GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            // Data rows
            for (int from = 0; from < 10; from++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(WeatherNames[from], EditorStyles.boldLabel, GUILayout.Width(80));

                float[] weights = FindWeights(from);
                for (int to = 0; to < 10; to++)
                {
                    float w = weights != null && to < weights.Length ? weights[to] : 0f;

                    var prevBg = GUI.backgroundColor;
                    if (w > 0.3f) GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
                    else if (w > 0.1f) GUI.backgroundColor = new Color(0.9f, 0.9f, 0.4f);
                    else if (w > 0f) GUI.backgroundColor = new Color(0.9f, 0.7f, 0.4f);

                    EditorGUILayout.LabelField(w > 0f ? $"{w:F2}" : "-", GUILayout.Width(70));
                    GUI.backgroundColor = prevBg;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Green = high probability (>0.3), Yellow = medium (>0.1), Orange = low (>0). Dash = no transition.",
                MessageType.None);
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private float[] FindWeights(int fromWeather)
        {
            if (_config == null) return null;
            foreach (var entry in _config.TransitionProbabilities)
            {
                if ((int)entry.FromWeather == fromWeather && entry.Season == _selectedSeason)
                    return entry.Weights;
            }
            return null;
        }
    }
}
