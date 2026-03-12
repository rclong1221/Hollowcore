using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Economy;
using DIG.Shared;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Cancels queued crafts and refunds ingredients.
    /// Only cancels Queued state (not InProgress) to prevent timing exploits.
    /// Server-only managed SystemBase.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CraftOutputCollectionSystem))]
    public partial class CraftCancellationSystem : SystemBase
    {
        private EntityQuery _stationQuery;
        private EntityQuery _registryQuery;

        protected override void OnCreate()
        {
            _stationQuery = GetEntityQuery(
                ComponentType.ReadOnly<CraftingStation>(),
                ComponentType.ReadWrite<CancelCraftRequest>(),
                ComponentType.ReadWrite<CraftQueueElement>());
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<RecipeRegistryManaged>());
        }

        protected override void OnUpdate()
        {
            RecipeRegistryManaged registry = null;
            if (_registryQuery.CalculateEntityCount() > 0)
            {
                var registryEntity = _registryQuery.GetSingletonEntity();
                registry = EntityManager.GetComponentObject<RecipeRegistryManaged>(registryEntity);
            }
            if (registry == null) return;

            var entities = _stationQuery.ToEntityArray(Allocator.Temp);

            for (int s = 0; s < entities.Length; s++)
            {
                var stationEntity = entities[s];
                var cancelRequests = EntityManager.GetBuffer<CancelCraftRequest>(stationEntity);
                var queue = EntityManager.GetBuffer<CraftQueueElement>(stationEntity);

                for (int r = 0; r < cancelRequests.Length; r++)
                {
                    var request = cancelRequests[r];
                    int idx = request.QueueIndex;

                    if (idx < 0 || idx >= queue.Length) continue;
                    var elem = queue[idx];

                    // Only cancel Queued items — InProgress cannot be cancelled
                    if (elem.State != CraftState.Queued) continue;

                    // Validate ownership
                    if (elem.RequestingPlayer != request.RequestingPlayer) continue;

                    // Refund ingredients
                    if (registry.ManagedEntries.TryGetValue(elem.RecipeId, out var recipeDef))
                    {
                        RefundIngredients(request.RequestingPlayer, recipeDef);
                        RefundCurrency(request.RequestingPlayer, recipeDef, stationEntity);
                    }

                    queue.RemoveAt(idx);

                    // Adjust indices for remaining cancel requests
                    for (int adj = r + 1; adj < cancelRequests.Length; adj++)
                    {
                        var adjReq = cancelRequests[adj];
                        if (adjReq.QueueIndex > idx)
                        {
                            adjReq.QueueIndex--;
                            cancelRequests[adj] = adjReq;
                        }
                    }
                }

                cancelRequests.Clear();
            }

            entities.Dispose();
        }

        private void RefundIngredients(Entity player, RecipeDefinitionSO recipe)
        {
            if (!EntityManager.HasBuffer<InventoryItem>(player)) return;
            var inventory = EntityManager.GetBuffer<InventoryItem>(player);

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.IngredientType != IngredientType.Resource) continue;

                bool found = false;
                for (int i = 0; i < inventory.Length; i++)
                {
                    if (inventory[i].ResourceType == ingredient.ResourceType)
                    {
                        var item = inventory[i];
                        item.Quantity += ingredient.Quantity;
                        inventory[i] = item;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    inventory.Add(new InventoryItem
                    {
                        ResourceType = ingredient.ResourceType,
                        Quantity = ingredient.Quantity
                    });
                }
            }
        }

        private void RefundCurrency(Entity player, RecipeDefinitionSO recipe, Entity source)
        {
            if (recipe.CurrencyCosts == null || recipe.CurrencyCosts.Length == 0) return;
            if (!EntityManager.HasBuffer<CurrencyTransaction>(player)) return;
            var transactions = EntityManager.GetBuffer<CurrencyTransaction>(player);

            foreach (var cost in recipe.CurrencyCosts)
            {
                transactions.Add(new CurrencyTransaction
                {
                    Type = cost.CurrencyType,
                    Amount = cost.Amount, // Positive = refund
                    Source = source
                });
            }
        }
    }
}
