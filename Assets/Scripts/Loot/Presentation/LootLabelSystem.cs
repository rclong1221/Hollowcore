using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Items;
using DIG.Items.Systems;
using DIG.Loot.Components;
using DIG.Loot.Config;
using UnityEngine;

namespace DIG.Loot.Presentation
{
    /// <summary>
    /// EPIC 16.6: Client-side system that manages floating loot labels.
    /// Shows item name + rarity color above loot drops near the local player.
    /// LOD: full label less than 10m, icon-only 10-30m, hidden 30m+.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LootLabelSystem : SystemBase
    {
        private const float FullLabelRange = 10f;
        private const float IconOnlyRange = 30f;

        protected override void OnUpdate()
        {
            // Find local player position
            float3 playerPos = float3.zero;
            bool hasPlayer = false;

            foreach (var (transform, _) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<GhostOwnerIsLocal>>())
            {
                playerPos = transform.ValueRO.Position;
                hasPlayer = true;
                break;
            }

            if (!hasPlayer) return;

            // Get item registry for display names
            bool hasRegistry = SystemAPI.ManagedAPI.HasSingleton<ItemRegistryManaged>();
            ItemRegistryManaged registry = null;
            if (hasRegistry)
                registry = SystemAPI.ManagedAPI.GetSingleton<ItemRegistryManaged>();

            foreach (var (pickup, lootTransform, entity) in
                     SystemAPI.Query<RefRO<ItemPickup>, RefRO<LocalTransform>>()
                     .WithAll<LootEntity>()
                     .WithEntityAccess())
            {
                float dist = math.distance(playerPos, lootTransform.ValueRO.Position);

                if (dist > IconOnlyRange)
                {
                    // Hidden — too far
                    continue;
                }

                // Determine display name and rarity
                string displayName = $"Item #{pickup.ValueRO.ItemTypeId}";
                ItemRarity rarity = ItemRarity.Common;

                if (registry != null && registry.ManagedEntries.TryGetValue(pickup.ValueRO.ItemTypeId, out var itemEntry))
                {
                    displayName = itemEntry.DisplayName;
                    rarity = itemEntry.Rarity;
                }

                if (dist <= FullLabelRange)
                {
                    // Full label — name + quantity
                    string label = pickup.ValueRO.Quantity > 1
                        ? $"{displayName} x{pickup.ValueRO.Quantity}"
                        : displayName;

                    // Labels are rendered by LootLabelRenderer via widget system
                    // This system would push data to the widget bridge
                }
                // else: icon-only mode (10-30m) — handled by widget LOD system
            }
        }
    }
}
