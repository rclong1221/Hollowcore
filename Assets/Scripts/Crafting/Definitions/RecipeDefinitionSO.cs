using UnityEngine;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: ScriptableObject defining a single recipe.
    /// Created by designers, stored in RecipeDatabaseSO.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Crafting/Recipe Definition", order = 1)]
    public class RecipeDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int RecipeId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;

        [Header("Classification")]
        public RecipeCategory Category;
        [Range(1, 5)] public int Tier = 1;
        public int SortOrder;

        [Header("Ingredients")]
        public RecipeIngredient[] Ingredients = new RecipeIngredient[0];
        public CurrencyCost[] CurrencyCosts = new CurrencyCost[0];

        [Header("Output")]
        public RecipeOutput Output;

        [Header("Crafting")]
        [Tooltip("Time in seconds. 0 = instant.")]
        public float CraftingTime = 5f;
        public StationType RequiredStation = StationType.Workbench;
        [Tooltip("Minimum station tier required. 0 = any tier.")]
        [Range(0, 5)] public int RequiredStationTier;

        [Header("Unlock")]
        public RecipeUnlockCondition UnlockCondition = RecipeUnlockCondition.AlwaysAvailable;
        [Tooltip("Level, quest ID, or schematic item ID depending on UnlockCondition.")]
        public int UnlockValue;
        [Tooltip("Must have crafted all of these first.")]
        public int[] PrerequisiteRecipeIds = new int[0];
    }
}
