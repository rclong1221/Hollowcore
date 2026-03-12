using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Weather.Editor.Modules
{
    public class WeatherControlModule : IWeatherWorkstationModule
    {
        public void OnGUI()
        {
            EditorGUILayout.LabelField("Weather Controls", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to control weather.", MessageType.Info);
                return;
            }

            var world = WeatherWorkstationWindow.GetWeatherWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadWrite<WeatherState>());
            if (query.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("No WeatherState singleton found.", MessageType.Info);
                return;
            }

            var entity = query.GetSingletonEntity();
            var ws = em.GetComponentData<WeatherState>(entity);

            EditorGUILayout.Space(4);

            // Current state display
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.8f, 0.3f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;
            EditorGUILayout.LabelField($"Current: {ws.CurrentWeather}  ->  Next: {ws.NextWeather}");
            EditorGUILayout.LabelField($"Transition: {ws.TransitionProgress:P0}  |  Temp: {ws.Temperature:F1} C");
            EditorGUILayout.LabelField($"Wind: ({ws.WindDirectionX:F2}, {ws.WindDirectionY:F2}) @ {ws.WindSpeed:F1} m/s");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Force weather type
            var newWeather = (WeatherType)EditorGUILayout.EnumPopup("Force Weather", ws.CurrentWeather);
            if (newWeather != ws.CurrentWeather)
            {
                ws.CurrentWeather = newWeather;
                ws.NextWeather = newWeather;
                ws.TransitionProgress = 1.0f;
                em.SetComponentData(entity, ws);
            }

            EditorGUILayout.Space(4);

            // Intensity sliders
            float newRain = EditorGUILayout.Slider("Rain Intensity", ws.RainIntensity, 0f, 1f);
            if (!Mathf.Approximately(newRain, ws.RainIntensity))
            {
                ws.RainIntensity = newRain;
                em.SetComponentData(entity, ws);
            }

            float newSnow = EditorGUILayout.Slider("Snow Intensity", ws.SnowIntensity, 0f, 1f);
            if (!Mathf.Approximately(newSnow, ws.SnowIntensity))
            {
                ws.SnowIntensity = newSnow;
                em.SetComponentData(entity, ws);
            }

            float newFog = EditorGUILayout.Slider("Fog Density", ws.FogDensity, 0f, 1f);
            if (!Mathf.Approximately(newFog, ws.FogDensity))
            {
                ws.FogDensity = newFog;
                em.SetComponentData(entity, ws);
            }

            float newWind = EditorGUILayout.Slider("Wind Speed", ws.WindSpeed, 0f, 30f);
            if (!Mathf.Approximately(newWind, ws.WindSpeed))
            {
                ws.WindSpeed = newWind;
                em.SetComponentData(entity, ws);
            }

            float newTemp = EditorGUILayout.Slider("Temperature (C)", ws.Temperature, -30f, 50f);
            if (!Mathf.Approximately(newTemp, ws.Temperature))
            {
                ws.Temperature = newTemp;
                em.SetComponentData(entity, ws);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Trigger Lightning"))
            {
                ws.LightningTimer = 0.01f;
                em.SetComponentData(entity, ws);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
