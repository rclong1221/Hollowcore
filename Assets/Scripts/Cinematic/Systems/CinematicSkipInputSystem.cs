using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Client-side system reading skip key input during cinematic.
    /// Creates CinematicSkipRpc to send to server.
    /// Solo play (LocalSimulation): immediately sets CinematicState.IsPlaying = false.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CinematicSkipInputSystem : SystemBase
    {
        private EntityQuery _stateQuery;
        private EntityQuery _networkIdQuery;
        private bool _skipSent;

        protected override void OnCreate()
        {
            _stateQuery = GetEntityQuery(ComponentType.ReadWrite<CinematicState>());
            _networkIdQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkId>());
            RequireForUpdate(_stateQuery);
        }

        protected override void OnUpdate()
        {
            var stateEntity = _stateQuery.GetSingletonEntity();
            var state = EntityManager.GetComponentData<CinematicState>(stateEntity);

            // Only process when playing and can skip
            if (!state.IsPlaying || !state.CanSkip)
            {
                _skipSent = false;
                return;
            }

            // Check skip key (Escape or Space)
            if (!Input.GetKeyDown(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.Space))
                return;

            // Prevent double-send
            if (_skipSent) return;
            _skipSent = true;

            // Check if we're in LocalSimulation (solo play) — skip immediately
            bool isLocal = World.Flags.HasFlag(WorldFlags.GameServer) ||
                           (World.Flags & WorldFlags.GameClient) == 0;

            if (isLocal)
            {
                // Solo: immediate skip, no RPC roundtrip
                state.IsPlaying = false;
                EntityManager.SetComponentData(stateEntity, state);
                return;
            }

            // Multiplayer: send skip RPC to server
            int networkId = 0;
            if (_networkIdQuery.CalculateEntityCount() > 0)
            {
                networkId = _networkIdQuery.GetSingleton<NetworkId>().Value;
            }

            var rpcEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(rpcEntity, new CinematicSkipRpc
            {
                CinematicId = state.CurrentCinematicId,
                NetworkId = networkId
            });
            EntityManager.AddComponent<SendRpcCommandRequest>(rpcEntity);
        }
    }
}
