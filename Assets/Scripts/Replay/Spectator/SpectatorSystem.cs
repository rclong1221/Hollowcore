using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Client-side spectator system.
    /// When client connects as spectator, this system manages spectator state
    /// and drives the SpectatorCamera via a singleton component.
    ///
    /// SystemBase because it calls managed SpectatorCamera methods.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SpectatorClientSystem : SystemBase
    {
        private EntityQuery _ghostQuery;
        private EntityQuery _ownerQuery;
        private bool _isSpectator;
        private NativeList<ushort> _playerGhostIds;

        protected override void OnCreate()
        {
            _ghostQuery = GetEntityQuery(
                ComponentType.ReadOnly<GhostInstance>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            _ownerQuery = GetEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<GhostInstance>()
            );
            _playerGhostIds = new NativeList<ushort>(16, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_playerGhostIds.IsCreated)
                _playerGhostIds.Dispose();
        }

        protected override void OnUpdate()
        {
            if (!_isSpectator) return;

            // Update player list from ghost entities with GhostOwner
            RefreshPlayerList();

            // Handle camera mode cycling (Tab key)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SpectatorCamera.Instance?.CycleMode();
            }

            // Handle player cycling (1-9 keys)
            for (int i = 0; i < 9 && i < _playerGhostIds.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SpectatorCamera.Instance?.FollowGhostId(_playerGhostIds[i]);
                }
            }
        }

        public void EnterSpectatorMode()
        {
            _isSpectator = true;

            // Create SpectatorState singleton entity
            var entity = EntityManager.CreateEntity(typeof(SpectatorState));
            EntityManager.SetComponentData(entity, new SpectatorState
            {
                CameraMode = SpectatorCameraMode.FreeCam,
                FollowedGhostId = 0,
                FollowedPlayerIndex = 0
            });

            Debug.Log("[Spectator] Entered spectator mode.");
        }

        private void RefreshPlayerList()
        {
            _playerGhostIds.Clear();

            if (_ownerQuery.CalculateEntityCount() == 0) return;

            var ghosts = _ownerQuery.ToComponentDataArray<GhostInstance>(Allocator.Temp);
            for (int i = 0; i < ghosts.Length; i++)
                _playerGhostIds.Add((ushort)ghosts[i].ghostId);
            ghosts.Dispose();
        }
    }

    /// <summary>
    /// EPIC 18.10: Server-side system that handles SpectatorJoinRequest RPC.
    /// Marks connection with SpectatorTag and puts it in-game WITHOUT spawning a player entity.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SpectatorJoinServerSystem : SystemBase
    {
        private EntityQuery _requestQuery;

        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(
                ComponentType.ReadOnly<SpectatorJoinRequest>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            RequireForUpdate(_requestQuery);
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var networkIdLookup = GetComponentLookup<NetworkId>(true);

            // Use manual query iteration (not SystemAPI.Query) for IRpcCommand entities
            var entities = _requestQuery.ToEntityArray(Allocator.Temp);
            var requests = _requestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var connEntity = requests[i].SourceConnection;

                // Mark connection as in-game + spectator
                ecb.AddComponent<NetworkStreamInGame>(connEntity);
                ecb.AddComponent<SpectatorTag>(connEntity);

                if (networkIdLookup.HasComponent(connEntity))
                {
                    var netId = networkIdLookup[connEntity];
                    Debug.Log($"[SpectatorJoin] Client {netId.Value} joined as spectator.");
                }

                // DO NOT spawn player entity — that's the whole point
                ecb.DestroyEntity(entities[i]);
            }

            entities.Dispose();
            requests.Dispose();
            ecb.Playback(EntityManager);
        }
    }
}
