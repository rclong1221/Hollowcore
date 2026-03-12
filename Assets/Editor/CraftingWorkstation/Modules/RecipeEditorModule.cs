using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Crafting.Editor.Modules
{
    /// <summary>
    /// EPIC 16.13: Recipe editor module — list/search/filter recipes, edit inline.
    /// Follows QuestEditorModule pattern.
    /// </summary>
    public class RecipeEditorModule : ICraftingModule
    {
        private RecipeDatabaseSO _database;
        private string _searchFilter = "";
        private RecipeCategory _categoryFilter = (RecipeCategory)255; // All
        private StationType _stationFilter = (StationType)255; // All
        private Vector2 _listScroll;
        private int _selectedIndex = -1;
        private UnityEditor.Editor _recipeEditor;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Recipe Editor", EditorStyles.boldLabel);

            _database = (RecipeDatabaseSO)EditorGUILayout.ObjectField("Database", _database, typeof(RecipeDatabaseSO), false);
            if (_database == null)
            {
                _database = Resources.Load<RecipeDatabaseSO>("RecipeDatabase");
                if (_database == null)
                {
                    EditorGUILayout.HelpBox("No RecipeDatabaseSO found. Create one at Resources/RecipeDatabase.", MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(4);

            // Filters
            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            _categoryFilter = (RecipeCategory)EditorGUILayout.EnumPopup("Category", _categoryFilter);
            _stationFilter = (StationType)EditorGUILayout.EnumPopup("Station", _stationFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Layout
            EditorGUILayout.BeginHorizontal();

            // Left: recipe list
            EditorGUILayout.BeginVertical("box", GUILayout.Width(280));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(400));

            var filtered = GetFilteredRecipes();
            for (int i = 0; i < filtered.Count; i++)
            {
                var recipe = filtered[i];
                bool isSelected = _selectedIndex == i;
                var style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"[{recipe.RecipeId}] {recipe.DisplayName}", style))
                    _selectedIndex = i;

                var catColor = GetCategoryColor(recipe.Category);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = catColor;
                GUILayout.Label(recipe.Category.ToString(), EditorStyles.miniLabel, GUILayout.Width(70));
                GUI.backgroundColor = prevBg;

                GUILayout.Label($"T{recipe.Tier}", EditorStyles.miniLabel, GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ New Recipe"))
                CreateNewRecipe();

            EditorGUILayout.EndVertical();

            // Right: selected recipe inspector
            EditorGUILayout.BeginVertical("box");
            if (_selectedIndex >= 0 && _selectedIndex < filtered.Count)
            {
                var selected = filtered[_selectedIndex];
                if (_recipeEditor == null || _recipeEditor.target != selected)
                    _recipeEditor = UnityEditor.Editor.CreateEditor(selected);

                _recipeEditor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.HelpBox("Select a recipe from the list.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(UnityEditor.SceneView sceneView) { }

        private List<RecipeDefinitionSO> GetFilteredRecipes()
        {
            var result = new List<RecipeDefinitionSO>();
            if (_database == null) return result;

            foreach (var recipe in _database.Recipes)
            {
                if (recipe == null) continue;

                if ((byte)_categoryFilter != 255 && recipe.Category != _categoryFilter)
                    continue;

                if ((byte)_stationFilter != 255 && recipe.RequiredStation != _stationFilter)
                    continue;

                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    bool match = recipe.DisplayName != null &&
                        recipe.DisplayName.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase);
                    match |= recipe.RecipeId.ToString().Contains(_searchFilter);
                    if (!match) continue;
                }

                result.Add(recipe);
            }

            result.Sort((a, b) =>
            {
                int catCmp = a.Category.CompareTo(b.Category);
                if (catCmp != 0) return catCmp;
                int sortCmp = a.SortOrder.CompareTo(b.SortOrder);
                if (sortCmp != 0) return sortCmp;
                return a.RecipeId.CompareTo(b.RecipeId);
            });

            return result;
        }

        private void CreateNewRecipe()
        {
            if (_database == null) return;

            var path = EditorUtility.SaveFilePanelInProject(
                "New Recipe Definition", "NewRecipe", "asset", "Choose location for new recipe");
            if (string.IsNullOrEmpty(path)) return;

            var recipe = ScriptableObject.CreateInstance<RecipeDefinitionSO>();
            recipe.RecipeId = GetNextRecipeId();
            recipe.DisplayName = "New Recipe";
            AssetDatabase.CreateAsset(recipe, path);
            _database.Recipes.Add(recipe);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
        }

        private int GetNextRecipeId()
        {
            int max = 0;
            foreach (var r in _database.Recipes)
                if (r != null && r.RecipeId > max) max = r.RecipeId;
            return max + 1;
        }

        private static Color GetCategoryColor(RecipeCategory cat) => cat switch
        {
            RecipeCategory.Weapons => new Color(1f, 0.4f, 0.4f),
            RecipeCategory.Armor => new Color(0.4f, 0.6f, 1f),
            RecipeCategory.Consumables => new Color(0.4f, 1f, 0.4f),
            RecipeCategory.Ammo => new Color(1f, 0.8f, 0.3f),
            RecipeCategory.Materials => new Color(0.7f, 0.7f, 0.7f),
            RecipeCategory.Tools => new Color(0.8f, 0.5f, 0.2f),
            RecipeCategory.Upgrades => new Color(0.8f, 0.4f, 1f),
            _ => Color.white
        };
    }
}
