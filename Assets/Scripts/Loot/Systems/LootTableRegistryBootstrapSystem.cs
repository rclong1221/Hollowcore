using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Loot.Definitions;

namespace DIG.Loot.Systems
{
    /// <summary>
    /// EPIC 16.6: Managed singleton mapping LootTableId (baked InstanceID) to LootTableSO.
    /// Loaded from Resources/LootTables/ folder on startup.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class LootTableRegistryBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;

            // Load all LootTableSO from Resources
            var tables = Resources.LoadAll<LootTableSO>("LootTables");
            var registry = new Dictionary<int, LootTableSO>();

            foreach (var table in tables)
            {
                int id = table.GetInstanceID();
                registry[id] = table;
            }

            var singletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentObject(singletonEntity, new LootTableRegistryManaged
            {
                Tables = registry
            });

            if (tables.Length > 0)
                UnityEngine.Debug.Log($"[LootTableRegistry] Loaded {tables.Length} loot tables from Resources/LootTables/.");

            Enabled = false;
        }
    }

    /// <summary>
    /// Managed singleton holding LootTableSO references keyed by InstanceID.
    /// Access via SystemAPI.ManagedAPI.GetSingleton&lt;LootTableRegistryManaged&gt;().
    /// </summary>
    public class LootTableRegistryManaged : IComponentData
    {
        public Dictionary<int, LootTableSO> Tables;
    }
}
