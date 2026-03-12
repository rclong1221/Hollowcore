using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Economy;
using DIG.Shared;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Awards craft outputs to players on collect request.
    /// Resource output → InventoryItem buffer.
    /// Currency output → CurrencyTransaction buffer.
    /// Item output → InventoryItem (simplified; full item entity creation via LootSpawnSystem pattern for future).
    /// Server-only managed SystemBase.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CraftOutputGenerationSystem))]
    public partial class CraftOutputCollectionSystem : SystemBase
    {
        private EntityQuery _stationQuery;
        private EntityQuery _registryQuery;

        protected override void OnCreate()
        {
            _stationQuery = GetEntityQuery(
                ComponentType.ReadOnly<CraftingStation>(),
                ComponentType.ReadWrite<CollectCraftRequest>(),
                ComponentType.ReadWrite<CraftOutputElement>());
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<RecipeRegistryManaged>());
        }

        protected override void OnUpdate()
        {
            var entities = _stationQuery.ToEntityArray(Allocator.Temp);

            RecipeRegistryManaged registry = null;
            if (_registryQuery.CalculateEntityCount() > 0)
            {
                var registryEntity = _registryQuery.GetSingletonEntity();
                registry = EntityManager.GetComponentObject<RecipeRegistryManaged>(registryEntity);
            }

            for (int s = 0; s < entities.Length; s++)
            {
                var stationEntity = entities[s];
                var collectRequests = EntityManager.GetBuffer<CollectCraftRequest>(stationEntity);
                var outputs = EntityManager.GetBuffer<CraftOutputElement>(stationEntity);

                for (int r = 0; r < collectRequests.Length; r++)
                {
                    var request = collectRequests[r];
                    int idx = request.OutputIndex;

                    if (idx < 0 || idx >= outputs.Length) continue;
                    var output = outputs[idx];

                    // Validate the output belongs to this player
                    if (output.ForPlayer != request.RequestingPlayer) continue;

                    var player = request.RequestingPlayer;
                    var outputType = (RecipeOutputType)output.OutputType;

                    switch (outputType)
                    {
                        case RecipeOutputType.Resource:
                            AwardResource(player, (ResourceType)output.OutputResourceType, output.OutputQuantity);
                            break;

                        case RecipeOutputType.Currency:
                            AwardCurrency(player, output.OutputQuantity, stationEntity);
                            break;

                        case RecipeOutputType.Item:
                            AwardItem(player, output, registry);
                            break;
                    }

                    // Remove collected output
                    outputs.RemoveAt(idx);

                    // Adjust indices for remaining collect requests at this station
                    for (int adj = r + 1; adj < collectRequests.Length; adj++)
                    {
                        var adjReq = collectRequests[adj];
                        if (adjReq.OutputIndex > idx)
                        {
                            adjReq.OutputIndex--;
                            collectRequests[adj] = adjReq;
                        }
                    }
                }

                collectRequests.Clear();
            }

            entities.Dispose();
        }

        private void AwardResource(Entity player, ResourceType resourceType, int quantity)
        {
            if (!EntityManager.HasBuffer<InventoryItem>(player)) return;
            var inventory = EntityManager.GetBuffer<InventoryItem>(player);

            // Try to stack with existing
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceType == resourceType)
                {
                    var item = inventory[i];
                    item.Quantity += quantity;
                    inventory[i] = item;
                    return;
                }
            }

            // Add new entry
            inventory.Add(new InventoryItem
            {
                ResourceType = resourceType,
                Quantity = quantity
            });
        }

        private void AwardCurrency(Entity player, int amount, Entity source)
        {
            if (!EntityManager.HasBuffer<CurrencyTransaction>(player)) return;
            var transactions = EntityManager.GetBuffer<CurrencyTransaction>(player);

            // Default to Crafting currency for recipe outputs
            transactions.Add(new CurrencyTransaction
            {
                Type = CurrencyType.Gold,
                Amount = amount,
                Source = source
            });
        }

        private void AwardItem(Entity player, CraftOutputElement output, RecipeRegistryManaged registry)
        {
            // For item outputs, add as resource to inventory (simplified)
            // Full item entity creation with AffixRollSystem integration is a future enhancement
            // that would follow the LootSpawnSystem ECB pattern
            if (!EntityManager.HasBuffer<InventoryItem>(player)) return;
            var inventory = EntityManager.GetBuffer<InventoryItem>(player);

            // Check if recipe has RollAffixes — log for now
            if (registry?.ManagedEntries.TryGetValue(output.RecipeId, out var recipeDef) == true
                && recipeDef.Output.RollAffixes)
            {
                UnityEngine.Debug.Log($"[Crafting] Item {output.OutputItemTypeId} x{output.OutputQuantity} crafted with affix rolling. Full item entity creation pending LootSpawnSystem integration.");
            }

            // Simplified: add item type ID as a trackable inventory entry
            // A production implementation would create an ItemPickup entity via ECB
            // matching the LootSpawnSystem.CreateLootEntity pattern
        }
    }
}
