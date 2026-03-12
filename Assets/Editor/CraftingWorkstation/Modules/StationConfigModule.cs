using UnityEditor;
using UnityEngine;

namespace DIG.Crafting.Editor.Modules
{
    /// <summary>
    /// EPIC 16.13: Station config module — shows scene stations, recipe availability per station.
    /// </summary>
    public class StationConfigModule : ICraftingModule
    {
        private RecipeDatabaseSO _database;
        private Vector2 _scroll;
        private StationType _stationTypeFilter = StationType.Any;
        private int _stationTierFilter = 1;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Station Configuration", EditorStyles.boldLabel);

            _database = (RecipeDatabaseSO)EditorGUILayout.ObjectField("Database", _database, typeof(RecipeDatabaseSO), false);
            if (_database == null)
            {
                _database = Resources.Load<RecipeDatabaseSO>("RecipeDatabase");
                if (_database == null)
                {
                    EditorGUILayout.HelpBox("No RecipeDatabaseSO found.", MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(4);

            // Station filter
            EditorGUILayout.BeginHorizontal();
            _stationTypeFilter = (StationType)EditorGUILayout.EnumPopup("Station Type", _stationTypeFilter);
            _stationTierFilter = EditorGUILayout.IntSlider("Station Tier", _stationTierFilter, 1, 5);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Scene stations
            EditorGUILayout.LabelField("Scene Stations:", EditorStyles.boldLabel);
            var stationAuthorings = Object.FindObjectsByType<CraftingStationAuthoring>(FindObjectsSortMode.None);
            if (stationAuthorings.Length == 0)
            {
                EditorGUILayout.HelpBox("No CraftingStationAuthoring found in scene.", MessageType.Info);
            }
            else
            {
                foreach (var station in stationAuthorings)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"{station.gameObject.name}", EditorStyles.boldLabel, GUILayout.Width(200));
                    EditorGUILayout.LabelField($"{station.StationType} T{station.StationTier}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"Speed: {station.SpeedMultiplier:F1}x", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"Queue: {station.MaxQueueSize}", GUILayout.Width(60));
                    if (GUILayout.Button("Select", GUILayout.Width(50)))
                        Selection.activeGameObject = station.gameObject;
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(8);

            // Available recipes for selected station config
            EditorGUILayout.LabelField($"Available Recipes for {_stationTypeFilter} T{_stationTierFilter}:", EditorStyles.boldLabel);

            var available = _database.GetRecipesByStation(_stationTypeFilter, _stationTierFilter);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));

            if (available.Count == 0)
            {
                EditorGUILayout.HelpBox("No recipes available for this station configuration.", MessageType.Info);
            }
            else
            {
                foreach (var recipe in available)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"[{recipe.RecipeId}] {recipe.DisplayName}", GUILayout.Width(250));
                    EditorGUILayout.LabelField($"{recipe.Category}", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"T{recipe.Tier}", GUILayout.Width(30));
                    EditorGUILayout.LabelField($"{recipe.CraftingTime:F1}s", GUILayout.Width(50));

                    string ingredientSummary = "";
                    if (recipe.Ingredients != null)
                    {
                        foreach (var ing in recipe.Ingredients)
                        {
                            if (ingredientSummary.Length > 0) ingredientSummary += ", ";
                            ingredientSummary += ing.IngredientType == IngredientType.Resource
                                ? $"{ing.ResourceType}x{ing.Quantity}"
                                : $"Item#{ing.ItemTypeId}x{ing.Quantity}";
                        }
                    }
                    EditorGUILayout.LabelField(ingredientSummary, EditorStyles.miniLabel);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(UnityEditor.SceneView sceneView) { }
    }
}
