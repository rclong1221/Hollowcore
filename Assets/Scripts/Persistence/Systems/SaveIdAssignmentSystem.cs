using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Lobby;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Populates PlayerSaveId from GhostOwner.NetworkId on newly spawned players.
    /// Server only.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SaveSystem))]
    public partial class SaveIdAssignmentSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<SaveManagerSingleton>();
        }

        protected override void OnUpdate()
        {
            foreach (var (link, entity) in
                SystemAPI.Query<RefRO<SaveStateLink>>()
                    .WithAll<global::PlayerTag>()
                    .WithEntityAccess())
            {
                var childEntity = link.ValueRO.SaveChildEntity;
                if (childEntity == Entity.Null) continue;
                if (!EntityManager.HasComponent<PlayerSaveId>(childEntity)) continue;

                var saveId = EntityManager.GetComponentData<PlayerSaveId>(childEntity);
                if (saveId.PlayerId.Length > 0) continue; // Already assigned

                // EPIC 17.4: Use persistent PlayerId from LobbySpawnData if available
                if (EntityManager.HasComponent<GhostOwner>(entity))
                {
                    var ghost = EntityManager.GetComponentData<GhostOwner>(entity);

                    if (SystemAPI.HasSingleton<LobbySpawnData>())
                    {
                        var spawnData = SystemAPI.GetSingleton<LobbySpawnData>();
                        // Slot assignment matches connection order: NetworkId 1→slot 0, 2→slot 1, etc.
                        int slotIndex = ghost.NetworkId - 1;
                        var persistentId = spawnData.GetPersistentIdForSlot(slotIndex);
                        if (persistentId.Length > 0)
                            saveId.PlayerId = persistentId;
                        else
                            saveId.PlayerId = new FixedString64Bytes($"player_{ghost.NetworkId}");
                    }
                    else
                    {
                        saveId.PlayerId = new FixedString64Bytes($"player_{ghost.NetworkId}");
                    }
                }
                else
                {
                    saveId.PlayerId = new FixedString64Bytes("local");
                }

                EntityManager.SetComponentData(childEntity, saveId);
            }
        }
    }
}
