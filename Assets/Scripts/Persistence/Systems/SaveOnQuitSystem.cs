using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Performs a blocking save on application shutdown.
    /// Only synchronous I/O path in the entire persistence system.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SaveOnQuitSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<SaveManagerSingleton>();
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            if (!SystemAPI.ManagedAPI.HasSingleton<SaveManagerSingleton>()) return;
            var manager = SystemAPI.ManagedAPI.GetSingleton<SaveManagerSingleton>();
            if (!manager.IsInitialized || !manager.Config.ShutdownSaveEnabled) return;

            // Create save request and process it inline
            var requestEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(requestEntity, new SaveRequest
            {
                SlotIndex = manager.Config.AutosaveSlot,
                TriggerSource = SaveTriggerSource.Shutdown
            });

            // Flush any pending async writes
            SaveFileWriter.FlushBlocking();
            Debug.Log("[Persistence] Shutdown save completed.");
        }
    }
}
