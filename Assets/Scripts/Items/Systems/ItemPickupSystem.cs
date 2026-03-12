using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Shared;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Detects and processes item pickups from the world.
    /// Handles both auto-pickup (walk over) and interaction-based pickups.
    /// EPIC 16.6: TryAddToInventory now routes to resource inventory or equipment slots.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ItemPickupSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            // Resolve item registry (needed for weight/category lookups)
            NativeHashMap<int, ItemRegistryEntry> registryMap = default;
            bool hasRegistry = SystemAPI.ManagedAPI.HasSingleton<ItemRegistryManaged>();
            if (hasRegistry)
            {
                var registry = SystemAPI.ManagedAPI.GetSingleton<ItemRegistryManaged>();
                registryMap = registry.BlittableEntries;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process auto-pickups (proximity-based)
            foreach (var (pickup, pickupTransform, pickupEntity) in
                     SystemAPI.Query<RefRO<ItemPickup>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                if (pickup.ValueRO.RequiresInteraction)
                    continue;

                float radius = pickup.ValueRO.PickupRadius;
                var pickupPos = pickupTransform.ValueRO.Position;

                foreach (var (playerTransform, itemSetBuffer, inventoryBuffer, capacity, playerEntity) in
                         SystemAPI.Query<RefRO<LocalTransform>, DynamicBuffer<ItemSetEntry>,
                                         DynamicBuffer<InventoryItem>, RefRW<InventoryCapacity>>()
                         .WithAll<HasEquipment, global::PlayerTag>()
                         .WithEntityAccess())
                {
                    float distSq = math.distancesq(pickupPos, playerTransform.ValueRO.Position);
                    if (distSq <= radius * radius)
                    {
                        bool added = TryAddToInventory(
                            pickup.ValueRO, registryMap, hasRegistry,
                            itemSetBuffer, inventoryBuffer, ref capacity.ValueRW);

                        if (added)
                        {
                            ecb.DestroyEntity(pickupEntity);
                        }
                        break; // Only one player can pick up
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static bool TryAddToInventory(
            ItemPickup pickup,
            NativeHashMap<int, ItemRegistryEntry> registryMap,
            bool hasRegistry,
            DynamicBuffer<ItemSetEntry> itemSetBuffer,
            DynamicBuffer<InventoryItem> inventoryBuffer,
            ref InventoryCapacity capacity)
        {
            // If we have registry data, look up item metadata
            ItemRegistryEntry entry = default;
            bool hasEntry = hasRegistry && registryMap.IsCreated && registryMap.TryGetValue(pickup.ItemTypeId, out entry);

            // Weight check (if registry available)
            if (hasEntry && entry.Weight > 0f)
            {
                float totalWeight = capacity.CurrentWeight + entry.Weight * pickup.Quantity;
                if (totalWeight > capacity.MaxWeight)
                    return false; // Too heavy
            }

            // Route based on item category
            ItemCategory category = hasEntry ? entry.Category : ItemCategory.None;

            switch (category)
            {
                case ItemCategory.Weapon:
                case ItemCategory.Tool:
                case ItemCategory.Equipment:
                    return TryAddEquipment(pickup, itemSetBuffer, hasEntry ? entry.Weight : 0f, ref capacity);

                default:
                    // Resources, consumables, ammo → add to resource inventory
                    return TryAddResource(pickup, inventoryBuffer, hasEntry ? entry.Weight : 0f, ref capacity);
            }
        }

        private static bool TryAddEquipment(
            ItemPickup pickup,
            DynamicBuffer<ItemSetEntry> itemSetBuffer,
            float weight,
            ref InventoryCapacity capacity)
        {
            // Find first available QuickSlot (1-9)
            int availableSlot = FindAvailableQuickSlot(itemSetBuffer);
            if (availableSlot == 0)
                return false; // No available slots

            // Add to item set buffer
            itemSetBuffer.Add(new ItemSetEntry
            {
                SetName = "Pickup",
                ItemEntity = Entity.Null, // Entity will be spawned by ItemSpawnSystem
                Order = itemSetBuffer.Length,
                QuickSlot = availableSlot,
                IsDefault = false
            });

            // Update weight
            capacity.CurrentWeight += weight * pickup.Quantity;
            capacity.IsOverencumbered = capacity.CurrentWeight > capacity.MaxWeight;

            return true;
        }

        private static bool TryAddResource(
            ItemPickup pickup,
            DynamicBuffer<InventoryItem> inventoryBuffer,
            float weight,
            ref InventoryCapacity capacity)
        {
            // Try to find existing stack
            for (int i = 0; i < inventoryBuffer.Length; i++)
            {
                var item = inventoryBuffer[i];
                if ((int)item.ResourceType == pickup.ItemTypeId)
                {
                    item.Quantity += pickup.Quantity;
                    inventoryBuffer[i] = item;
                    capacity.CurrentWeight += weight * pickup.Quantity;
                    capacity.IsOverencumbered = capacity.CurrentWeight > capacity.MaxWeight;
                    return true;
                }
            }

            // No existing stack — add new entry
            inventoryBuffer.Add(new InventoryItem
            {
                ResourceType = ResourceType.None,
                Quantity = pickup.Quantity
            });

            capacity.CurrentWeight += weight * pickup.Quantity;
            capacity.IsOverencumbered = capacity.CurrentWeight > capacity.MaxWeight;
            return true;
        }

        private static int FindAvailableQuickSlot(DynamicBuffer<ItemSetEntry> itemSetBuffer)
        {
            // Track used slots
            bool slot1 = false, slot2 = false, slot3 = false, slot4 = false, slot5 = false;
            bool slot6 = false, slot7 = false, slot8 = false, slot9 = false;

            for (int i = 0; i < itemSetBuffer.Length; i++)
            {
                switch (itemSetBuffer[i].QuickSlot)
                {
                    case 1: slot1 = true; break;
                    case 2: slot2 = true; break;
                    case 3: slot3 = true; break;
                    case 4: slot4 = true; break;
                    case 5: slot5 = true; break;
                    case 6: slot6 = true; break;
                    case 7: slot7 = true; break;
                    case 8: slot8 = true; break;
                    case 9: slot9 = true; break;
                }
            }

            if (!slot1) return 1;
            if (!slot2) return 2;
            if (!slot3) return 3;
            if (!slot4) return 4;
            if (!slot5) return 5;
            if (!slot6) return 6;
            if (!slot7) return 7;
            if (!slot8) return 8;
            if (!slot9) return 9;
            return 0; // All slots full
        }
    }
}
