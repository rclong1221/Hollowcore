using System.Collections.Generic;
using UnityEngine;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Central registry of all recipes. Place one instance at Resources/RecipeDatabase.
    /// Follows ItemDatabaseSO pattern.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Crafting/Recipe Database", order = 0)]
    public class RecipeDatabaseSO : ScriptableObject
    {
        [SerializeField] public List<RecipeDefinitionSO> Recipes = new();

        private Dictionary<int, RecipeDefinitionSO> _lookupTable;

        public void BuildLookupTable()
        {
            _lookupTable = new Dictionary<int, RecipeDefinitionSO>(Recipes.Count);
            foreach (var recipe in Recipes)
            {
                if (recipe == null) continue;
                if (_lookupTable.ContainsKey(recipe.RecipeId))
                {
                    Debug.LogWarning($"[RecipeDatabase] Duplicate RecipeId {recipe.RecipeId}: '{recipe.DisplayName}' conflicts with '{_lookupTable[recipe.RecipeId].DisplayName}'");
                    continue;
                }
                _lookupTable[recipe.RecipeId] = recipe;
            }
        }

        public RecipeDefinitionSO GetRecipe(int recipeId)
        {
            if (_lookupTable == null) BuildLookupTable();
            return _lookupTable.TryGetValue(recipeId, out var recipe) ? recipe : null;
        }

        public bool HasRecipe(int recipeId)
        {
            if (_lookupTable == null) BuildLookupTable();
            return _lookupTable.ContainsKey(recipeId);
        }

        public List<RecipeDefinitionSO> GetRecipesByCategory(RecipeCategory category)
        {
            var result = new List<RecipeDefinitionSO>();
            foreach (var recipe in Recipes)
            {
                if (recipe != null && recipe.Category == category)
                    result.Add(recipe);
            }
            return result;
        }

        public List<RecipeDefinitionSO> GetRecipesByStation(StationType stationType, int stationTier)
        {
            var result = new List<RecipeDefinitionSO>();
            foreach (var recipe in Recipes)
            {
                if (recipe == null) continue;
                if (recipe.RequiredStation != StationType.Any && recipe.RequiredStation != stationType)
                    continue;
                if (recipe.RequiredStationTier > stationTier)
                    continue;
                result.Add(recipe);
            }
            return result;
        }
    }
}
