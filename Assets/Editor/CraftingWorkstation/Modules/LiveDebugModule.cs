using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Crafting.Editor.Modules
{
    /// <summary>
    /// EPIC 16.13: Play-mode live debug — shows all crafting station entities,
    /// queue progress bars, output buffers.
    /// Follows QuestLiveDebugModule pattern.
    /// </summary>
    public class LiveDebugModule : ICraftingModule
    {
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Live Crafting Debug", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see live crafting data.", MessageType.Info);
                return;
            }

            var world = CraftingWorkstationWindow.GetCraftingWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;

            // Get registry for names
            RecipeRegistryManaged registry = null;
            var registryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RecipeRegistryManaged>());
            if (registryQuery.CalculateEntityCount() > 0)
            {
                var registryEntity = registryQuery.GetSingletonEntity();
                registry = em.GetComponentObject<RecipeRegistryManaged>(registryEntity);
            }

            // Query all crafting stations
            var stationQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CraftingStation>(),
                ComponentType.ReadOnly<CraftQueueElement>(),
                ComponentType.ReadOnly<CraftOutputElement>());

            var entities = stationQuery.ToEntityArray(Allocator.Temp);
            var stations = stationQuery.ToComponentDataArray<CraftingStation>(Allocator.Temp);

            EditorGUILayout.LabelField($"Active Crafting Stations: {entities.Length}");
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < entities.Length; i++)
            {
                var station = stations[i];
                EditorGUILayout.BeginVertical("box");

                // Header
                var stationColor = GetStationColor(station.StationType);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = stationColor;
                EditorGUILayout.LabelField($"{station.StationType} T{station.StationTier} (Speed: {station.SpeedMultiplier:F1}x, MaxQueue: {station.MaxQueueSize})", EditorStyles.boldLabel);
                GUI.backgroundColor = prevBg;

                // Queue
                if (em.HasBuffer<CraftQueueElement>(entities[i]))
                {
                    var queue = em.GetBuffer<CraftQueueElement>(entities[i], true);
                    if (queue.Length > 0)
                    {
                        EditorGUILayout.LabelField($"  Queue ({queue.Length} items):", EditorStyles.miniLabel);
                        for (int q = 0; q < queue.Length; q++)
                        {
                            var elem = queue[q];
                            var def = registry?.ManagedEntries.TryGetValue(elem.RecipeId, out var rd) == true ? rd : null;
                            string recipeName = def?.DisplayName ?? $"Recipe #{elem.RecipeId}";

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"    [{elem.State}] {recipeName}", GUILayout.Width(250));

                            float progress = elem.CraftTimeTotal > 0 ? elem.CraftTimeElapsed / elem.CraftTimeTotal : 0f;
                            var barRect = GUILayoutUtility.GetRect(100, 16, GUILayout.Width(150));
                            EditorGUI.ProgressBar(barRect, progress, $"{elem.CraftTimeElapsed:F1}/{elem.CraftTimeTotal:F1}s");

                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }

                // Outputs
                if (em.HasBuffer<CraftOutputElement>(entities[i]))
                {
                    var outputs = em.GetBuffer<CraftOutputElement>(entities[i], true);
                    if (outputs.Length > 0)
                    {
                        EditorGUILayout.LabelField($"  Outputs ({outputs.Length} ready):", EditorStyles.miniLabel);
                        for (int o = 0; o < outputs.Length; o++)
                        {
                            var output = outputs[o];
                            var def = registry?.ManagedEntries.TryGetValue(output.RecipeId, out var rd) == true ? rd : null;
                            string recipeName = def?.DisplayName ?? $"Recipe #{output.RecipeId}";
                            EditorGUILayout.LabelField($"    {recipeName} x{output.OutputQuantity} (Type: {(RecipeOutputType)output.OutputType})", EditorStyles.miniLabel);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            entities.Dispose();
            stations.Dispose();
        }

        public void OnSceneGUI(UnityEditor.SceneView sceneView) { }

        private static Color GetStationColor(StationType type) => type switch
        {
            StationType.Workbench => new Color(0.6f, 0.4f, 0.2f),
            StationType.Forge => new Color(1f, 0.4f, 0.2f),
            StationType.AlchemyTable => new Color(0.3f, 0.8f, 0.3f),
            StationType.Armory => new Color(0.4f, 0.5f, 0.8f),
            StationType.Engineering => new Color(0.7f, 0.7f, 0.3f),
            _ => Color.gray
        };
    }
}
