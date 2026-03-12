#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Crafting.Editor
{
    /// <summary>
    /// EPIC 16.13: Custom inspector for RecipeDefinitionSO.
    /// Shows ingredient slots, output preview, cost summary, validation warnings.
    /// </summary>
    [CustomEditor(typeof(RecipeDefinitionSO))]
    public class RecipeDefinitionSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var recipe = (RecipeDefinitionSO)target;

            // Validation header
            DrawValidation(recipe);

            EditorGUILayout.Space(4);

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            // Summary box
            DrawSummary(recipe);
        }

        private void DrawValidation(RecipeDefinitionSO recipe)
        {
            bool hasErrors = false;

            if (recipe.RecipeId <= 0)
            {
                EditorGUILayout.HelpBox("RecipeId must be > 0.", MessageType.Error);
                hasErrors = true;
            }

            if (string.IsNullOrEmpty(recipe.DisplayName))
            {
                EditorGUILayout.HelpBox("DisplayName is empty.", MessageType.Warning);
            }

            if (recipe.Ingredients == null || recipe.Ingredients.Length == 0)
            {
                EditorGUILayout.HelpBox("Recipe has no ingredients.", MessageType.Warning);
            }
            else
            {
                foreach (var ing in recipe.Ingredients)
                {
                    if (ing.Quantity <= 0)
                    {
                        EditorGUILayout.HelpBox($"Ingredient has zero/negative quantity.", MessageType.Error);
                        hasErrors = true;
                        break;
                    }
                }
            }

            if (recipe.Output.OutputType == RecipeOutputType.Item && recipe.Output.ItemTypeId <= 0)
            {
                EditorGUILayout.HelpBox("Output is Item type but ItemTypeId is 0.", MessageType.Error);
                hasErrors = true;
            }

            if (recipe.Output.Quantity <= 0)
            {
                EditorGUILayout.HelpBox("Output quantity must be > 0.", MessageType.Error);
                hasErrors = true;
            }

            if (recipe.Output.RollAffixes && recipe.Output.MinRarity > recipe.Output.MaxRarity)
            {
                EditorGUILayout.HelpBox("MinRarity > MaxRarity for affix rolling.", MessageType.Error);
                hasErrors = true;
            }

            if (!hasErrors)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                EditorGUILayout.LabelField("Recipe valid", EditorStyles.helpBox);
                GUI.backgroundColor = prevBg;
            }
        }

        private void DrawSummary(RecipeDefinitionSO recipe)
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // Ingredients summary
            EditorGUILayout.LabelField("Costs:", EditorStyles.miniLabel);
            if (recipe.Ingredients != null)
            {
                foreach (var ing in recipe.Ingredients)
                {
                    string label = ing.IngredientType == IngredientType.Resource
                        ? $"  {ing.ResourceType} x{ing.Quantity}"
                        : $"  Item #{ing.ItemTypeId} x{ing.Quantity}";
                    EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                }
            }
            if (recipe.CurrencyCosts != null)
            {
                foreach (var cost in recipe.CurrencyCosts)
                    EditorGUILayout.LabelField($"  {cost.CurrencyType}: {cost.Amount}", EditorStyles.miniLabel);
            }

            // Output summary
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Produces:", EditorStyles.miniLabel);
            string outputLabel = recipe.Output.OutputType switch
            {
                RecipeOutputType.Item => $"  Item #{recipe.Output.ItemTypeId} x{recipe.Output.Quantity}" +
                    (recipe.Output.RollAffixes ? $" (Affixes, Rarity {recipe.Output.MinRarity}-{recipe.Output.MaxRarity})" : ""),
                RecipeOutputType.Resource => $"  {recipe.Output.ResourceType} x{recipe.Output.Quantity}",
                RecipeOutputType.Currency => $"  Currency x{recipe.Output.Quantity}",
                _ => "  Unknown"
            };
            EditorGUILayout.LabelField(outputLabel, EditorStyles.miniLabel);

            // Station requirement
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Station: {recipe.RequiredStation} T{recipe.RequiredStationTier}+", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Time: {recipe.CraftingTime:F1}s", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
