using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Blittable recipe entry for Burst-compatible validation.
    /// </summary>
    public struct RecipeRegistryEntry
    {
        public int RecipeId;
        public StationType RequiredStation;
        public int RequiredStationTier;
        public float CraftingTime;
        public int IngredientCount;
        public RecipeCategory Category;
    }

    /// <summary>
    /// EPIC 16.13: Managed singleton holding both blittable and managed recipe data.
    /// Created by RecipeRegistryBootstrapSystem.
    /// Access in managed systems via EntityManager.GetComponentObject.
    /// </summary>
    public class RecipeRegistryManaged : IComponentData
    {
        public RecipeDatabaseSO Database;
        public NativeHashMap<int, RecipeRegistryEntry> BlittableEntries;
        public Dictionary<int, RecipeDefinitionSO> ManagedEntries;
    }
}
