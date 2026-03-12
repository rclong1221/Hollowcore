using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Crafting.Editor.Modules
{
    /// <summary>
    /// EPIC 16.13: Balance simulator module — Monte Carlo resource sink analysis.
    /// Shows expected resource consumption for crafting N items of each recipe.
    /// </summary>
    public class BalanceSimulatorModule : ICraftingModule
    {
        private RecipeDatabaseSO _database;
        private int _simulationCount = 100;
        private Vector2 _scroll;
        private List<SimResult> _results = new();

        private struct SimResult
        {
            public string RecipeName;
            public int RecipeId;
            public RecipeCategory Category;
            public Dictionary<string, long> TotalResourceCost;
            public Dictionary<string, long> TotalCurrencyCost;
            public float TotalTime;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Balance Simulator", EditorStyles.boldLabel);

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

            EditorGUILayout.BeginHorizontal();
            _simulationCount = EditorGUILayout.IntField("Craft Count", _simulationCount);
            _simulationCount = Mathf.Max(1, _simulationCount);
            if (GUILayout.Button("Run Simulation", GUILayout.Width(120)))
                RunSimulation();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Run Simulation' to analyze resource sinks.", MessageType.Info);
                return;
            }

            // Results
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField($"Results for {_simulationCount} crafts per recipe:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            foreach (var result in _results)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"[{result.RecipeId}] {result.RecipeName} ({result.Category})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Total Time: {result.TotalTime:F0}s ({result.TotalTime / 60:F1}min)", EditorStyles.miniLabel);

                if (result.TotalResourceCost.Count > 0)
                {
                    EditorGUILayout.LabelField("  Resources:", EditorStyles.miniLabel);
                    foreach (var kvp in result.TotalResourceCost)
                        EditorGUILayout.LabelField($"    {kvp.Key}: {kvp.Value}", EditorStyles.miniLabel);
                }

                if (result.TotalCurrencyCost.Count > 0)
                {
                    EditorGUILayout.LabelField("  Currency:", EditorStyles.miniLabel);
                    foreach (var kvp in result.TotalCurrencyCost)
                        EditorGUILayout.LabelField($"    {kvp.Key}: {kvp.Value}", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(UnityEditor.SceneView sceneView) { }

        private void RunSimulation()
        {
            _results.Clear();
            if (_database == null) return;

            foreach (var recipe in _database.Recipes)
            {
                if (recipe == null) continue;

                var result = new SimResult
                {
                    RecipeName = recipe.DisplayName ?? $"Recipe #{recipe.RecipeId}",
                    RecipeId = recipe.RecipeId,
                    Category = recipe.Category,
                    TotalResourceCost = new Dictionary<string, long>(),
                    TotalCurrencyCost = new Dictionary<string, long>(),
                    TotalTime = recipe.CraftingTime * _simulationCount
                };

                if (recipe.Ingredients != null)
                {
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        string key = ingredient.IngredientType == IngredientType.Resource
                            ? ingredient.ResourceType.ToString()
                            : $"Item#{ingredient.ItemTypeId}";
                        long total = (long)ingredient.Quantity * _simulationCount;

                        if (result.TotalResourceCost.ContainsKey(key))
                            result.TotalResourceCost[key] += total;
                        else
                            result.TotalResourceCost[key] = total;
                    }
                }

                if (recipe.CurrencyCosts != null)
                {
                    foreach (var cost in recipe.CurrencyCosts)
                    {
                        string key = cost.CurrencyType.ToString();
                        long total = (long)cost.Amount * _simulationCount;

                        if (result.TotalCurrencyCost.ContainsKey(key))
                            result.TotalCurrencyCost[key] += total;
                        else
                            result.TotalCurrencyCost[key] = total;
                    }
                }

                _results.Add(result);
            }
        }
    }
}
