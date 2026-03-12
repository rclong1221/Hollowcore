using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Items.Definitions;

namespace DIG.Items.Systems
{
    /// <summary>
    /// EPIC 16.6: Loads ItemDatabaseSO from Resources and creates managed + blittable registries.
    /// Runs once on startup. Other systems access the singleton for item lookups.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ItemRegistryBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;

            var database = Resources.Load<ItemDatabaseSO>("ItemDatabase");
            if (database == null)
            {
                Debug.LogWarning("[ItemRegistry] No ItemDatabaseSO found at Resources/ItemDatabase. Item registry will be empty.");
                return;
            }

            database.BuildLookupTable();

            // Build blittable registry for Burst systems
            var blittableMap = new NativeHashMap<int, ItemRegistryEntry>(database.Entries.Count, Allocator.Persistent);
            var managedMap = new Dictionary<int, ItemEntrySO>(database.Entries.Count);

            foreach (var entry in database.Entries)
            {
                if (entry == null) continue;

                blittableMap[entry.ItemTypeId] = new ItemRegistryEntry
                {
                    ItemTypeId = entry.ItemTypeId,
                    Category = entry.Category,
                    Rarity = entry.Rarity,
                    IsStackable = entry.IsStackable,
                    MaxStack = entry.MaxStack,
                    Weight = entry.Weight
                };

                managedMap[entry.ItemTypeId] = entry;
            }

            // Store as managed singleton (accessible via SystemAPI.ManagedAPI)
            var singletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentObject(singletonEntity, new ItemRegistryManaged
            {
                Database = database,
                BlittableEntries = blittableMap,
                ManagedEntries = managedMap
            });

            Debug.Log($"[ItemRegistry] Loaded {blittableMap.Count} items from ItemDatabase.");

            Enabled = false; // Only run once
        }

        protected override void OnDestroy()
        {
            // Clean up native collection
            foreach (var managed in SystemAPI.Query<ItemRegistryManaged>())
            {
                if (managed.BlittableEntries.IsCreated)
                    managed.BlittableEntries.Dispose();
            }
        }
    }

    /// <summary>
    /// Managed singleton holding both blittable and managed item data.
    /// Access via SystemAPI.ManagedAPI.GetSingleton&lt;ItemRegistryManaged&gt;().
    /// </summary>
    public class ItemRegistryManaged : IComponentData
    {
        public ItemDatabaseSO Database;
        public NativeHashMap<int, ItemRegistryEntry> BlittableEntries;
        public Dictionary<int, ItemEntrySO> ManagedEntries;
    }
}
