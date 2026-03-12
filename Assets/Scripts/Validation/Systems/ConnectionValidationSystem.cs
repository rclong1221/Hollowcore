using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Connection gate that checks ban list before allowing players in-game.
    /// Runs before GoInGameServerSystem. If a connecting player is banned,
    /// their connection is disconnected before spawning.
    /// Also flushes deferred ban list writes once per frame.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(GoInGameServerSystem))]
    public partial class ConnectionValidationSystem : SystemBase
    {
        private EntityQuery _pendingConnectionsQuery;

        protected override void OnCreate()
        {
            _pendingConnectionsQuery = GetEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.Exclude<NetworkStreamInGame>(),
                ComponentType.Exclude<NetworkStreamRequestDisconnect>());
        }

        protected override void OnUpdate()
        {
            // Flush deferred ban list writes (background thread, no hitch)
            BanListManager.SaveIfDirty();

            if (_pendingConnectionsQuery.CalculateEntityCount() == 0) return;
            if (BanListManager.ActiveBanCount == 0) return;

            var entities = _pendingConnectionsQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var networkIds = _pendingConnectionsQuery.ToComponentDataArray<NetworkId>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                int netId = networkIds[i].Value;

                if (BanListManager.IsBanned(netId))
                {
                    EntityManager.AddComponentData(entities[i],
                        new NetworkStreamRequestDisconnect
                        {
                            Reason = NetworkStreamDisconnectReason.ClosedByRemote
                        });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Validation] Rejected banned connection: netid_{netId}");
#endif
                }
            }

            entities.Dispose();
            networkIds.Dispose();
        }

        protected override void OnDestroy()
        {
            BanListManager.FlushBlocking();
        }
    }
}
