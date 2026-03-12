using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Weather.Editor.Modules
{
    public class GameplayInspectorModule : IWeatherWorkstationModule
    {
        private Vector2 _scroll;

        private static readonly string[] WeatherNames =
        {
            "Clear", "PartlyCloudy", "Cloudy", "LightRain", "HeavyRain",
            "Thunderstorm", "LightSnow", "HeavySnow", "Fog", "Sandstorm"
        };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Gameplay Inspector", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see live gameplay modifiers.", MessageType.Info);
                return;
            }

            var world = WeatherWorkstationWindow.GetWeatherWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Weather state
            DrawWeatherState(em);

            EditorGUILayout.Space(8);

            // Player modifiers
            DrawPlayerModifiers(em);

            EditorGUILayout.Space(8);

            // Weather zones
            DrawWeatherZones(em);

            EditorGUILayout.Space(8);

            // Gameplay params table
            DrawGameplayParamsTable(em);

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawWeatherState(EntityManager em)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<WeatherState>());
            if (query.CalculateEntityCount() == 0) return;

            var ws = query.GetSingleton<WeatherState>();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Active Weather State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Current: {ws.CurrentWeather}  ->  Next: {ws.NextWeather}");
            EditorGUILayout.LabelField($"Rain: {ws.RainIntensity:F2}  Snow: {ws.SnowIntensity:F2}  Fog: {ws.FogDensity:F2}");
            EditorGUILayout.LabelField($"Wind: {ws.WindSpeed:F1} m/s  Temp: {ws.Temperature:F1} C");

            // Wetness
            var wetnessQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WeatherWetness>());
            if (wetnessQuery.CalculateEntityCount() > 0)
            {
                var wetness = wetnessQuery.GetSingleton<WeatherWetness>();
                EditorGUILayout.LabelField($"Surface Wetness: {wetness.Value:F2}");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPlayerModifiers(EntityManager em)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Player Modifiers", EditorStyles.boldLabel);

            var visionQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WeatherVisionModifier>());
            var visionEntities = visionQuery.ToEntityArray(Allocator.Temp);
            var visionMods = visionQuery.ToComponentDataArray<WeatherVisionModifier>(Allocator.Temp);

            for (int i = 0; i < visionEntities.Length; i++)
            {
                EditorGUILayout.LabelField(
                    $"Entity #{visionEntities[i].Index}: Vision Range x{visionMods[i].RangeMultiplier:F2}");
            }
            visionEntities.Dispose();
            visionMods.Dispose();

            var moveQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WeatherMovementModifier>());
            var moveEntities = moveQuery.ToEntityArray(Allocator.Temp);
            var moveMods = moveQuery.ToComponentDataArray<WeatherMovementModifier>(Allocator.Temp);

            for (int i = 0; i < moveEntities.Length; i++)
            {
                EditorGUILayout.LabelField(
                    $"Entity #{moveEntities[i].Index}: Move Speed x{moveMods[i].SpeedMultiplier:F2}");
            }
            moveEntities.Dispose();
            moveMods.Dispose();

            if (visionQuery.CalculateEntityCount() == 0 && moveQuery.CalculateEntityCount() == 0)
                EditorGUILayout.LabelField("No entities with weather gameplay modifiers.");

            EditorGUILayout.EndVertical();
        }

        private void DrawWeatherZones(EntityManager em)
        {
            var overrideQuery = em.CreateEntityQuery(ComponentType.ReadOnly<LocalWeatherOverride>());
            if (overrideQuery.CalculateEntityCount() == 0) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Weather Zone Overrides", EditorStyles.boldLabel);

            var overrides = overrideQuery.ToComponentDataArray<LocalWeatherOverride>(Allocator.Temp);
            for (int i = 0; i < overrides.Length; i++)
            {
                var ov = overrides[i];
                if (ov.HasOverride)
                {
                    EditorGUILayout.LabelField(
                        $"Zone Override: {ov.OverrideWeather} (Biome {ov.BiomeType}, Blend {ov.BlendWeight:F2})");
                }
                else
                {
                    EditorGUILayout.LabelField("No active zone override (using global weather).");
                }
            }
            overrides.Dispose();

            EditorGUILayout.EndVertical();
        }

        private void DrawGameplayParamsTable(EntityManager em)
        {
            var mgrQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WeatherManagerSingleton>());
            if (mgrQuery.CalculateEntityCount() == 0) return;

            var mgr = mgrQuery.GetSingleton<WeatherManagerSingleton>();
            if (!mgr.WeatherParams.IsCreated) return;

            ref var paramsBlob = ref mgr.WeatherParams.Value;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Gameplay Params (from BlobAsset)", EditorStyles.boldLabel);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Weather", GUILayout.Width(100));
            EditorGUILayout.LabelField("Vision", GUILayout.Width(60));
            EditorGUILayout.LabelField("Speed", GUILayout.Width(60));
            EditorGUILayout.LabelField("Friction", GUILayout.Width(60));
            EditorGUILayout.LabelField("Noise", GUILayout.Width(60));
            EditorGUILayout.LabelField("Projectile", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < paramsBlob.GameplayParams.Length && i < WeatherNames.Length; i++)
            {
                ref var gp = ref paramsBlob.GameplayParams[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(WeatherNames[i], GUILayout.Width(100));
                EditorGUILayout.LabelField($"{gp.VisionRangeMultiplier:F2}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{gp.MovementSpeedMultiplier:F2}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{gp.SurfaceFrictionMultiplier:F2}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{gp.NoiseMultiplier:F2}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{gp.ProjectileSpeedMultiplier:F2}", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
