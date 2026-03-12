using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Client-side system that receives CinematicPlayRpc, CinematicEndRpc,
    /// and CinematicSkipUpdateRpc from the server and writes CinematicState singleton.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CinematicRpcReceiveSystem : SystemBase
    {
        private EntityQuery _playRpcQuery;
        private EntityQuery _endRpcQuery;
        private EntityQuery _skipUpdateRpcQuery;
        private EntityQuery _stateQuery;

        protected override void OnCreate()
        {
            _playRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<CinematicPlayRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            _endRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<CinematicEndRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            _skipUpdateRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<CinematicSkipUpdateRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            _stateQuery = GetEntityQuery(ComponentType.ReadWrite<CinematicState>());
            RequireForUpdate(_stateQuery);
        }

        protected override void OnUpdate()
        {
            var stateEntity = _stateQuery.GetSingletonEntity();

            // Process CinematicPlayRpc
            if (_playRpcQuery.CalculateEntityCount() > 0)
            {
                var playRpcs = _playRpcQuery.ToComponentDataArray<CinematicPlayRpc>(Allocator.Temp);
                var playEntities = _playRpcQuery.ToEntityArray(Allocator.Temp);

                // Use first play RPC (should only be one per frame)
                if (playRpcs.Length > 0)
                {
                    var rpc = playRpcs[0];
                    var state = EntityManager.GetComponentData<CinematicState>(stateEntity);
                    state.IsPlaying = true;
                    state.CurrentCinematicId = rpc.CinematicId;
                    state.CinematicType = rpc.CinematicType;
                    state.CanSkip = rpc.SkipPolicy != SkipPolicy.NoSkip;
                    state.Duration = rpc.Duration;
                    state.TotalPlayersInScene = rpc.TotalPlayers;
                    state.ElapsedTime = 0f;
                    state.SkipVotesReceived = 0;
                    state.BlendProgress = 0f;
                    EntityManager.SetComponentData(stateEntity, state);
                }

                for (int i = 0; i < playEntities.Length; i++)
                    EntityManager.DestroyEntity(playEntities[i]);

                playRpcs.Dispose();
                playEntities.Dispose();
            }

            // Process CinematicEndRpc
            if (_endRpcQuery.CalculateEntityCount() > 0)
            {
                var endRpcs = _endRpcQuery.ToComponentDataArray<CinematicEndRpc>(Allocator.Temp);
                var endEntities = _endRpcQuery.ToEntityArray(Allocator.Temp);

                if (endRpcs.Length > 0)
                {
                    var state = EntityManager.GetComponentData<CinematicState>(stateEntity);
                    state.IsPlaying = false;
                    EntityManager.SetComponentData(stateEntity, state);
                }

                for (int i = 0; i < endEntities.Length; i++)
                    EntityManager.DestroyEntity(endEntities[i]);

                endRpcs.Dispose();
                endEntities.Dispose();
            }

            // Process CinematicSkipUpdateRpc
            if (_skipUpdateRpcQuery.CalculateEntityCount() > 0)
            {
                var skipRpcs = _skipUpdateRpcQuery.ToComponentDataArray<CinematicSkipUpdateRpc>(Allocator.Temp);
                var skipEntities = _skipUpdateRpcQuery.ToEntityArray(Allocator.Temp);

                if (skipRpcs.Length > 0)
                {
                    var state = EntityManager.GetComponentData<CinematicState>(stateEntity);
                    state.SkipVotesReceived = skipRpcs[0].SkipVotesReceived;
                    EntityManager.SetComponentData(stateEntity, state);
                }

                for (int i = 0; i < skipEntities.Length; i++)
                    EntityManager.DestroyEntity(skipEntities[i]);

                skipRpcs.Dispose();
                skipEntities.Dispose();
            }
        }
    }
}
