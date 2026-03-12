using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Player.Systems
{
    /// <summary>
    /// Temporary diagnostic system to log ghost configuration details
    /// for all player entities on server and clients.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class DebugGhostConfigSystem : SystemBase
    {
        private double _lastLogTime;
        private bool _hasLoggedOnce;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            // Only log once at startup and then every 5 seconds
            if (_hasLoggedOnce && SystemAPI.Time.ElapsedTime - _lastLogTime < 5.0)
                return;

            _lastLogTime = SystemAPI.Time.ElapsedTime;
            _hasLoggedOnce = true;

            var worldName = World.Name;
            var isServer = World.IsServer();
            var isClient = World.IsClient();

            // Debug.Log($"[GhostConfig] === {worldName} (Server:{isServer}, Client:{isClient}) ===");

            foreach (var (tag, entity) in SystemAPI.Query<RefRO<PlayerTag>>().WithEntityAccess())
            {
                var hasSimulate = EntityManager.HasComponent<Simulate>(entity);
                var hasGhostOwnerIsLocal = EntityManager.HasComponent<GhostOwnerIsLocal>(entity);
                var hasGhostOwner = EntityManager.HasComponent<GhostOwner>(entity);
                var hasGhostInstance = EntityManager.HasComponent<GhostInstance>(entity);
                
                var ghostOwnerValue = hasGhostOwner ? EntityManager.GetComponentData<GhostOwner>(entity).NetworkId : -1;
                
                string ghostTypeInfo = "Unknown";
                if (hasGhostInstance)
                {
                    var ghostInstance = EntityManager.GetComponentData<GhostInstance>(entity);
                    ghostTypeInfo = $"GhostType={ghostInstance.ghostType}, GhostId={ghostInstance.ghostId}, SpawnTick={ghostInstance.spawnTick.SerializedData}";
                }

                // Debug.Log($"[GhostConfig] Entity {entity.Index}: " +
                //          $"HasSimulate={hasSimulate}, HasGhostOwnerIsLocal={hasGhostOwnerIsLocal}, " +
                //          $"GhostOwner.NetworkId={ghostOwnerValue}, {ghostTypeInfo}");
            }
        }
    }
}
