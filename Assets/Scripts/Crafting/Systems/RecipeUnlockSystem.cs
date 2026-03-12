using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Processes RecipeUnlockRequest buffers on crafting knowledge child entities.
    /// Adds new RecipeId entries to KnownRecipeElement if not already known.
    /// Server-only.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CraftCancellationSystem))]
    public partial class RecipeUnlockSystem : SystemBase
    {
        private EntityQuery _knowledgeQuery;

        protected override void OnCreate()
        {
            _knowledgeQuery = GetEntityQuery(
                ComponentType.ReadOnly<CraftingKnowledgeTag>(),
                ComponentType.ReadWrite<RecipeUnlockRequest>(),
                ComponentType.ReadWrite<KnownRecipeElement>());
        }

        protected override void OnUpdate()
        {
            var entities = _knowledgeQuery.ToEntityArray(Allocator.Temp);

            for (int e = 0; e < entities.Length; e++)
            {
                var entity = entities[e];
                var requests = EntityManager.GetBuffer<RecipeUnlockRequest>(entity);
                var known = EntityManager.GetBuffer<KnownRecipeElement>(entity);

                for (int r = 0; r < requests.Length; r++)
                {
                    int recipeId = requests[r].RecipeId;

                    // Check for duplicates
                    bool alreadyKnown = false;
                    for (int k = 0; k < known.Length; k++)
                    {
                        if (known[k].RecipeId == recipeId)
                        {
                            alreadyKnown = true;
                            break;
                        }
                    }

                    if (!alreadyKnown)
                    {
                        known.Add(new KnownRecipeElement { RecipeId = recipeId });
                        CraftingEventQueue.Enqueue(CraftingUIEventType.RecipeUnlocked, recipeId);
                    }
                }

                requests.Clear();
            }

            entities.Dispose();
        }
    }
}
