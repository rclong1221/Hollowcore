using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Items;
using DIG.Loot.Components;
using DIG.Loot.Config;
using UnityEngine;

namespace DIG.Loot.Presentation
{
    /// <summary>
    /// EPIC 16.6: Client-side system for rarity-based visual effects on loot drops.
    /// Rare+: vertical beam, Epic+: pulsing glow, Legendary+: beam + aura.
    /// Uses LootVisualConfig SO for per-rarity prefab assignment.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LootHighlightSystem : SystemBase
    {
        private LootVisualConfig _config;

        protected override void OnStartRunning()
        {
            _config = Resources.Load<LootVisualConfig>("LootVisualConfig");
        }

        protected override void OnUpdate()
        {
            if (_config == null) return;

            // Get item registry for rarity lookups
            bool hasRegistry = SystemAPI.ManagedAPI.HasSingleton<Items.Systems.ItemRegistryManaged>();
            Items.Systems.ItemRegistryManaged registry = null;
            if (hasRegistry)
                registry = SystemAPI.ManagedAPI.GetSingleton<Items.Systems.ItemRegistryManaged>();

            foreach (var (pickup, lootTransform, entity) in
                     SystemAPI.Query<RefRO<ItemPickup>, RefRO<LocalTransform>>()
                     .WithAll<LootEntity>()
                     .WithEntityAccess())
            {
                ItemRarity rarity = ItemRarity.Common;
                if (registry != null && registry.ManagedEntries.TryGetValue(pickup.ValueRO.ItemTypeId, out var entry))
                {
                    rarity = entry.Rarity;
                }

                // Only create VFX for Rare and above
                if (rarity < ItemRarity.Rare) continue;

                var visual = _config.GetVisual(rarity);

                // VFX instantiation would happen through a managed VFX pool
                // This system identifies which entities need effects and at what intensity
                // Actual GameObject instantiation deferred to a managed bridge system

                // Rare+: beam
                // Epic+: beam + glow
                // Legendary+: beam + glow + aura
            }
        }
    }
}
