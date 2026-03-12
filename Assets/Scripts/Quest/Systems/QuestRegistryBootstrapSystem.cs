using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: One-time bootstrap that loads QuestDatabaseSO from Resources/,
    /// builds the managed singleton, and self-disables.
    /// Follows ItemRegistryBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class QuestRegistryBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;

            var database = Resources.Load<QuestDatabaseSO>("QuestDatabase");
            if (database == null)
            {
                Debug.LogWarning("[QuestRegistry] No QuestDatabaseSO found at Resources/QuestDatabase. Quest system disabled.");
                Enabled = false;
                return;
            }

            database.BuildLookupTable();

            var managedMap = new Dictionary<int, QuestDefinitionSO>(database.Quests.Count);
            foreach (var quest in database.Quests)
            {
                if (quest == null) continue;
                managedMap[quest.QuestId] = quest;
            }

            var singletonEntity = EntityManager.CreateEntity();
            EntityManager.SetName(singletonEntity, "QuestRegistry");
            EntityManager.AddComponentObject(singletonEntity, new QuestRegistryManaged
            {
                Database = database,
                ManagedEntries = managedMap
            });

            Debug.Log($"[QuestRegistry] Loaded {managedMap.Count} quest definitions.");
            Enabled = false;
        }
    }
}
