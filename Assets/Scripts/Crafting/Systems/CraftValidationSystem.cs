using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Economy;
using DIG.Shared;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Validates CraftRequest, consumes ingredients atomically, enqueues CraftQueueElement.
    /// Server-only managed SystemBase.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CraftRpcReceiveSystem))]
    public partial class CraftValidationSystem : SystemBase
    {
        private EntityQuery _stationQuery;
        private EntityQuery _registryQuery;

        protected override void OnCreate()
        {
            _stationQuery = GetEntityQuery(
                ComponentType.ReadOnly<CraftingStation>(),
                ComponentType.ReadWrite<CraftRequest>(),
                ComponentType.ReadWrite<CraftQueueElement>());
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<RecipeRegistryManaged>());
        }

        protected override void OnUpdate()
        {
            if (_registryQuery.CalculateEntityCount() == 0) return;

            var registryEntity = _registryQuery.GetSingletonEntity();
            var registry = EntityManager.GetComponentObject<RecipeRegistryManaged>(registryEntity);

            var entities = _stationQuery.ToEntityArray(Allocator.Temp);

            for (int s = 0; s < entities.Length; s++)
            {
                var stationEntity = entities[s];
                var station = EntityManager.GetComponentData<CraftingStation>(stationEntity);
                var requests = EntityManager.GetBuffer<CraftRequest>(stationEntity);
                var queue = EntityManager.GetBuffer<CraftQueueElement>(stationEntity);

                for (int r = 0; r < requests.Length; r++)
                {
                    var request = requests[r];
                    var player = request.RequestingPlayer;

                    if (!registry.ManagedEntries.TryGetValue(request.RecipeId, out var recipeDef))
                    {
                        CraftingEventQueue.Enqueue(CraftingUIEventType.CraftFailed, request.RecipeId);
                        continue;
                    }

                    // Validate station type
                    if (recipeDef.RequiredStation != StationType.Any && recipeDef.RequiredStation != station.StationType)
                    {
                        CraftingEventQueue.Enqueue(CraftingUIEventType.CraftFailed, request.RecipeId);
                        continue;
                    }

                    // Validate station tier
                    if (recipeDef.RequiredStationTier > station.StationTier)
                    {
                        CraftingEventQueue.Enqueue(CraftingUIEventType.CraftFailed, request.RecipeId);
                        continue;
                    }

                    // Validate queue capacity (count for this player)
                    int playerQueueCount = 0;
                    for (int q = 0; q < queue.Length; q++)
                    {
                        if (queue[q].RequestingPlayer == player &&
                            (queue[q].State == CraftState.Queued || queue[q].State == CraftState.InProgress))
                            playerQueueCount++;
                    }
                    if (playerQueueCount >= station.MaxQueueSize)
                    {
                        CraftingEventQueue.Enqueue(CraftingUIEventType.CraftFailed, request.RecipeId);
                        continue;
                    }

                    // Validate player has knowledge of recipe (if not AlwaysAvailable)
                    if (recipeDef.UnlockCondition != RecipeUnlockCondition.AlwaysAvailable)
                    {
                        if (!PlayerKnowsRecipe(player, request.RecipeId))
                        {
                            CraftingEventQueue.Enqueue(CraftingUIEventType.CraftFailed, request.RecipeId);
                            continue;
                        }
                    }

                    // Validate ingredients
                    if (!HasIngredients(player, recipeDef))
                    {
                        CraftingEventQueue.Enqueue(CraftingUIEventType.InsufficientIngredients, request.RecipeId);
                        continue;
                    }

                    // Validate currency
                    if (!HasCurrency(player, recipeDef))
                    {
                        CraftingEventQueue.Enqueue(CraftingUIEventType.InsufficientIngredients, request.RecipeId);
                        continue;
                    }

                    // All checks passed — consume ingredients atomically
                    ConsumeIngredients(player, recipeDef);
                    ConsumeCurrency(player, recipeDef, stationEntity);

                    // Enqueue craft
                    float craftTime = recipeDef.CraftingTime;
                    queue.Add(new CraftQueueElement
                    {
                        RecipeId = request.RecipeId,
                        RequestingPlayer = player,
                        CraftTimeTotal = craftTime,
                        CraftTimeElapsed = 0f,
                        State = craftTime <= 0f ? CraftState.Complete : CraftState.Queued,
                        RandomSeed = (uint)(SystemAPI.Time.ElapsedTime * 1000 + request.RecipeId)
                    });

                    CraftingEventQueue.Enqueue(CraftingUIEventType.CraftStarted, request.RecipeId);
                }

                requests.Clear();
            }

            entities.Dispose();
        }

        private bool PlayerKnowsRecipe(Entity player, int recipeId)
        {
            if (!EntityManager.HasComponent<CraftingKnowledgeLink>(player)) return false;
            var link = EntityManager.GetComponentData<CraftingKnowledgeLink>(player);
            if (link.KnowledgeEntity == Entity.Null || !EntityManager.Exists(link.KnowledgeEntity)) return false;
            if (!EntityManager.HasBuffer<KnownRecipeElement>(link.KnowledgeEntity)) return false;

            var known = EntityManager.GetBuffer<KnownRecipeElement>(link.KnowledgeEntity, true);
            for (int i = 0; i < known.Length; i++)
            {
                if (known[i].RecipeId == recipeId) return true;
            }
            return false;
        }

        private bool HasIngredients(Entity player, RecipeDefinitionSO recipe)
        {
            if (!EntityManager.HasBuffer<InventoryItem>(player)) return false;
            var inventory = EntityManager.GetBuffer<InventoryItem>(player, true);

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.IngredientType == IngredientType.Resource)
                {
                    int have = 0;
                    for (int i = 0; i < inventory.Length; i++)
                    {
                        if (inventory[i].ResourceType == ingredient.ResourceType)
                            have += inventory[i].Quantity;
                    }
                    if (have < ingredient.Quantity) return false;
                }
                // Item ingredients checked via InventoryItem with matching ItemTypeId
                // would need CharacterItem query — simplified to resource-only for now
            }
            return true;
        }

        private bool HasCurrency(Entity player, RecipeDefinitionSO recipe)
        {
            if (recipe.CurrencyCosts == null || recipe.CurrencyCosts.Length == 0) return true;
            if (!EntityManager.HasComponent<CurrencyInventory>(player)) return false;
            var currency = EntityManager.GetComponentData<CurrencyInventory>(player);

            foreach (var cost in recipe.CurrencyCosts)
            {
                int balance = cost.CurrencyType switch
                {
                    CurrencyType.Gold => currency.Gold,
                    CurrencyType.Premium => currency.Premium,
                    CurrencyType.Crafting => currency.Crafting,
                    _ => 0
                };
                if (balance < cost.Amount) return false;
            }
            return true;
        }

        private void ConsumeIngredients(Entity player, RecipeDefinitionSO recipe)
        {
            if (!EntityManager.HasBuffer<InventoryItem>(player)) return;
            var inventory = EntityManager.GetBuffer<InventoryItem>(player);

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.IngredientType != IngredientType.Resource) continue;
                int remaining = ingredient.Quantity;

                for (int i = 0; i < inventory.Length && remaining > 0; i++)
                {
                    var item = inventory[i];
                    if (item.ResourceType != ingredient.ResourceType) continue;

                    int deduct = remaining > item.Quantity ? item.Quantity : remaining;
                    item.Quantity -= deduct;
                    remaining -= deduct;
                    inventory[i] = item;
                }
            }
        }

        private void ConsumeCurrency(Entity player, RecipeDefinitionSO recipe, Entity source)
        {
            if (recipe.CurrencyCosts == null || recipe.CurrencyCosts.Length == 0) return;
            if (!EntityManager.HasBuffer<CurrencyTransaction>(player)) return;
            var transactions = EntityManager.GetBuffer<CurrencyTransaction>(player);

            foreach (var cost in recipe.CurrencyCosts)
            {
                transactions.Add(new CurrencyTransaction
                {
                    Type = cost.CurrencyType,
                    Amount = -cost.Amount,
                    Source = source
                });
            }
        }
    }
}
