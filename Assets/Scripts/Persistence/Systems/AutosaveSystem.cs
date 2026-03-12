using Unity.Entities;
using Unity.NetCode;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Ticks autosave timer and creates SaveRequest(Autosave) when interval reached.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SaveSystem))]
    public partial class AutosaveSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<SaveManagerSingleton>();
        }

        protected override void OnUpdate()
        {
            var manager = SystemAPI.ManagedAPI.GetSingleton<SaveManagerSingleton>();
            if (!manager.IsInitialized) return;

            float interval = manager.Config.AutosaveIntervalSeconds;
            if (interval <= 0f) return;

            float dt = SystemAPI.Time.DeltaTime;
            manager.ElapsedPlaytime += dt;
            manager.TimeSinceLastSave += dt;
            manager.TimeSinceLastCheckpoint += dt;

            if (manager.TimeSinceLastSave >= interval)
            {
                var requestEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(requestEntity, new SaveRequest
                {
                    SlotIndex = manager.Config.AutosaveSlot,
                    TriggerSource = SaveTriggerSource.Autosave
                });
            }
        }
    }
}
