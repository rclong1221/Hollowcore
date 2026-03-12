using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Weather.Editor.Modules
{
    public class TimeControlModule : IWeatherWorkstationModule
    {
        public void OnGUI()
        {
            EditorGUILayout.LabelField("Time Controls", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to control time.", MessageType.Info);
                return;
            }

            var world = WeatherWorkstationWindow.GetWeatherWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadWrite<WorldTimeState>());
            if (query.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("No WorldTimeState singleton found. Add WeatherBootstrapAuthoring to your subscene.", MessageType.Info);
                return;
            }

            var entity = query.GetSingletonEntity();
            var ts = em.GetComponentData<WorldTimeState>(entity);

            EditorGUILayout.Space(4);

            // Current state display
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;
            EditorGUILayout.LabelField($"Day {ts.DayCount + 1}  |  {GetTimeString(ts.TimeOfDay)}  |  {GetPeriodName(ts.TimeOfDay)}");
            EditorGUILayout.LabelField($"Season: {ts.Season}  |  Time Scale: {ts.TimeScale:F1}x  |  {(ts.IsPaused ? "PAUSED" : "Running")}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Time of Day slider
            float newTime = EditorGUILayout.Slider("Time of Day", ts.TimeOfDay, 0f, 23.99f);
            if (!Mathf.Approximately(newTime, ts.TimeOfDay))
            {
                ts.TimeOfDay = newTime;
                em.SetComponentData(entity, ts);
            }

            // Time Scale slider
            float newScale = EditorGUILayout.Slider("Time Scale", ts.TimeScale, 0f, 10f);
            if (!Mathf.Approximately(newScale, ts.TimeScale))
            {
                ts.TimeScale = newScale;
                em.SetComponentData(entity, ts);
            }

            // Pause toggle
            bool newPaused = EditorGUILayout.Toggle("Paused", ts.IsPaused);
            if (newPaused != ts.IsPaused)
            {
                ts.IsPaused = newPaused;
                em.SetComponentData(entity, ts);
            }

            // Season dropdown
            var newSeason = (Season)EditorGUILayout.EnumPopup("Season", ts.Season);
            if (newSeason != ts.Season)
            {
                ts.Season = newSeason;
                em.SetComponentData(entity, ts);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Quick Jump", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dawn (6:00)")) { ts.TimeOfDay = 6f; em.SetComponentData(entity, ts); }
            if (GUILayout.Button("Noon (12:00)")) { ts.TimeOfDay = 12f; em.SetComponentData(entity, ts); }
            if (GUILayout.Button("Dusk (18:00)")) { ts.TimeOfDay = 18f; em.SetComponentData(entity, ts); }
            if (GUILayout.Button("Midnight (0:00)")) { ts.TimeOfDay = 0f; em.SetComponentData(entity, ts); }
            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private static string GetTimeString(float timeOfDay)
        {
            int hours = (int)timeOfDay;
            int minutes = (int)((timeOfDay - hours) * 60);
            return $"{hours:D2}:{minutes:D2}";
        }

        private static string GetPeriodName(float hour)
        {
            if (hour < 5f) return "Night";
            if (hour < 7f) return "Dawn";
            if (hour < 10f) return "Morning";
            if (hour < 14f) return "Midday";
            if (hour < 17f) return "Afternoon";
            if (hour < 19f) return "Dusk";
            if (hour < 22f) return "Evening";
            return "Late Night";
        }
    }
}
