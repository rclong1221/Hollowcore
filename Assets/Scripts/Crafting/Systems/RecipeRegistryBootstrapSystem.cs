using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Loads RecipeDatabaseSO from Resources and creates managed + blittable registries.
    /// Runs once on startup. Follows ItemRegistryBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class RecipeRegistryBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;

            var database = Resources.Load<RecipeDatabaseSO>("RecipeDatabase");
            if (database == null)
            {
                Debug.LogWarning("[RecipeRegistry] No RecipeDatabaseSO found at Resources/RecipeDatabase. Recipe registry will be empty.");
                Enabled = false;
                return;
            }

            database.BuildLookupTable();

            var blittableMap = new NativeHashMap<int, RecipeRegistryEntry>(database.Recipes.Count, Allocator.Persistent);
            var managedMap = new Dictionary<int, RecipeDefinitionSO>(database.Recipes.Count);

            foreach (var recipe in database.Recipes)
            {
                if (recipe == null) continue;

                blittableMap[recipe.RecipeId] = new RecipeRegistryEntry
                {
                    RecipeId = recipe.RecipeId,
                    RequiredStation = recipe.RequiredStation,
                    RequiredStationTier = recipe.RequiredStationTier,
                    CraftingTime = recipe.CraftingTime,
                    IngredientCount = recipe.Ingredients?.Length ?? 0,
                    Category = recipe.Category
                };

                managedMap[recipe.RecipeId] = recipe;
            }

            var singletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentObject(singletonEntity, new RecipeRegistryManaged
            {
                Database = database,
                BlittableEntries = blittableMap,
                ManagedEntries = managedMap
            });

            Debug.Log($"[RecipeRegistry] Loaded {blittableMap.Count} recipes from RecipeDatabase.");

            Enabled = false;
        }

        protected override void OnDestroy()
        {
            var query = GetEntityQuery(ComponentType.ReadOnly<RecipeRegistryManaged>());
            if (query.CalculateEntityCount() > 0)
            {
                var entity = query.GetSingletonEntity();
                var managed = EntityManager.GetComponentObject<RecipeRegistryManaged>(entity);
                if (managed?.BlittableEntries.IsCreated == true)
                    managed.BlittableEntries.Dispose();
            }
        }
    }
}
