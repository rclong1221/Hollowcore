using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Detects newly spawned players matching known disconnected PlayerSaveId.
    /// If a save file exists, creates LoadRequest to restore their state.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SaveIdAssignmentSystem))]
    [UpdateBefore(typeof(LoadSystem))]
    public partial class PlayerReconnectSaveSystem : SystemBase
    {
        private EntityQuery _newPlayerQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<SaveManagerSingleton>();
        }

        protected override void OnUpdate()
        {
            var manager = SystemAPI.ManagedAPI.GetSingleton<SaveManagerSingleton>();
            if (!manager.IsInitialized) return;

            foreach (var (link, entity) in
                SystemAPI.Query<RefRO<SaveStateLink>>()
                    .WithAll<global::PlayerTag>()
                    .WithEntityAccess())
            {
                var childEntity = link.ValueRO.SaveChildEntity;
                if (childEntity == Entity.Null) continue;
                if (!EntityManager.HasComponent<PlayerSaveId>(childEntity)) continue;

                var saveId = EntityManager.GetComponentData<PlayerSaveId>(childEntity);
                if (saveId.PlayerId.Length == 0) continue;

                // Check if an autosave exists for this player
                string fileName = $"{saveId.PlayerId}_slot{manager.Config.AutosaveSlot}.dig";
                string filePath = Path.Combine(manager.SaveDirectory, fileName);

                if (!File.Exists(filePath)) continue;

                // Only auto-load once per entity — use a tag to prevent re-triggering
                if (EntityManager.HasComponent<SaveAutoLoadedTag>(entity)) continue;

                EntityManager.AddComponent<SaveAutoLoadedTag>(entity);

                var requestEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(requestEntity, new LoadRequest
                {
                    SlotIndex = manager.Config.AutosaveSlot,
                    TargetPlayerId = saveId.PlayerId
                });

                Debug.Log($"[Persistence] Auto-loading save for reconnected player: {saveId.PlayerId}");
            }
        }
    }

    /// <summary>Tag to prevent repeated auto-load attempts on the same player entity.</summary>
    internal struct SaveAutoLoadedTag : IComponentData { }
}
