using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Items;
using DIG.Items.Definitions;
using DIG.Items.Systems;
using DIG.Loot.Components;
using DIG.Loot.Definitions;

namespace DIG.Loot.Systems
{
    /// <summary>
    /// EPIC 16.6: Spawns loot entities from PendingLootSpawn buffers.
    /// Instantiates WorldPrefab from ItemRegistry, adds pickup/lifetime/ownership components.
    /// Max 32 spawns per frame to prevent hitching.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DeathLootSystem))]
    public partial class LootSpawnSystem : SystemBase
    {
        private const int MaxSpawnsPerFrame = 32;

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.HasSingleton<ItemRegistryManaged>())
                return;

            var registry = SystemAPI.ManagedAPI.GetSingleton<ItemRegistryManaged>();
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int spawnCount = 0;

            foreach (var (pendingBuffer, entity) in
                     SystemAPI.Query<DynamicBuffer<PendingLootSpawn>>()
                     .WithEntityAccess())
            {
                if (pendingBuffer.Length == 0) continue;

                // Process entries
                int processed = 0;
                for (int i = 0; i < pendingBuffer.Length && spawnCount < MaxSpawnsPerFrame; i++)
                {
                    var pending = pendingBuffer[i];
                    processed++;

                    // Resource drops don't need a world prefab — they're added directly to nearby players
                    // For now, skip resource/currency types that have no physical loot entity
                    if (pending.Type == LootEntryType.Resource || pending.Type == LootEntryType.Currency)
                    {
                        // TODO: Create resource pickup entities or directly award when pickup system handles it
                        continue;
                    }

                    // Lookup item entry for world prefab
                    if (!registry.ManagedEntries.TryGetValue(pending.ItemTypeId, out var itemEntry))
                        continue;
                    if (itemEntry.WorldPrefab == null)
                        continue;

                    // We need a prefab entity — for now use ECB to create entity with components
                    // In production, this would instantiate a baked prefab entity
                    var lootEntity = ecb.CreateEntity();

                    ecb.AddComponent(lootEntity, LocalTransform.FromPosition(pending.SpawnPosition + new float3(0f, 0.5f, 0f)));

                    ecb.AddComponent(lootEntity, new ItemPickup
                    {
                        ItemTypeId = pending.ItemTypeId,
                        Quantity = pending.Quantity,
                        PickupRadius = 2.0f,
                        RequiresInteraction = false
                    });

                    ecb.AddComponent(lootEntity, new LootEntity());

                    ecb.AddComponent(lootEntity, new LootLifetimeECS
                    {
                        SpawnTime = currentTime,
                        Lifetime = GetLifetimeByRarity(pending.Rarity)
                    });

                    ecb.AddComponent(lootEntity, new LootOwnership
                    {
                        Mode = LootOwnershipMode.FreeForAll,
                        OwnerEntity = Entity.Null,
                        OwnershipTimer = 0f
                    });

                    spawnCount++;
                }

                // Remove processed entries
                if (processed >= pendingBuffer.Length)
                {
                    pendingBuffer.Clear();
                }
                else if (processed > 0)
                {
                    // Remove from front
                    pendingBuffer.RemoveRange(0, processed);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static float GetLifetimeByRarity(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => 60f,
                ItemRarity.Uncommon => 90f,
                ItemRarity.Rare => 120f,
                ItemRarity.Epic => 300f,
                ItemRarity.Legendary => 300f,
                ItemRarity.Unique => 600f,
                _ => 60f
            };
        }
    }
}
