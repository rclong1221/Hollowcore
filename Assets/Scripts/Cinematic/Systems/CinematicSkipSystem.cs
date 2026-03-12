using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Server-side system that receives CinematicSkipRpc from clients,
    /// tallies votes against SkipPolicy, and broadcasts CinematicEndRpc when threshold met.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CinematicSkipSystem : SystemBase
    {
        private EntityQuery _skipRpcQuery;
        private NativeHashSet<int> _skipVoters;
        private int _activeCinematicId;
        private SkipPolicy _activePolicy;
        private int _totalPlayers;
        private bool _isTracking;

        protected override void OnCreate()
        {
            _skipRpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<CinematicSkipRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            _skipVoters = new NativeHashSet<int>(8, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_skipVoters.IsCreated)
                _skipVoters.Dispose();
        }

        /// <summary>Called by CinematicTriggerSystem when a cinematic starts.</summary>
        public void BeginTracking(int cinematicId, SkipPolicy policy, int totalPlayers)
        {
            _activeCinematicId = cinematicId;
            _activePolicy = policy;
            _totalPlayers = totalPlayers;
            _isTracking = true;
            _skipVoters.Clear();
        }

        /// <summary>Called when cinematic ends.</summary>
        public void StopTracking()
        {
            _isTracking = false;
            _skipVoters.Clear();
        }

        /// <summary>Called when a player disconnects during cinematic.</summary>
        public void OnPlayerDisconnect(int networkId)
        {
            if (!_isTracking) return;
            _skipVoters.Remove(networkId);
            _totalPlayers = Mathf.Max(1, _totalPlayers - 1);

            // Re-evaluate skip threshold with new total
            EvaluateThreshold();
        }

        protected override void OnUpdate()
        {
            if (!_isTracking) return;
            if (_skipRpcQuery.CalculateEntityCount() == 0) return;

            var skipRpcs = _skipRpcQuery.ToComponentDataArray<CinematicSkipRpc>(Allocator.Temp);
            var skipEntities = _skipRpcQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < skipRpcs.Length; i++)
            {
                var rpc = skipRpcs[i];

                // Validate: active cinematic, NoSkip policy rejects
                if (rpc.CinematicId != _activeCinematicId) continue;
                if (_activePolicy == SkipPolicy.NoSkip) continue;

                // Dedup: same player can't double-vote
                if (!_skipVoters.Add(rpc.NetworkId)) continue;

                // Broadcast updated vote count
                BroadcastSkipUpdate();
            }

            // Destroy processed RPC entities
            for (int i = 0; i < skipEntities.Length; i++)
                EntityManager.DestroyEntity(skipEntities[i]);

            skipRpcs.Dispose();
            skipEntities.Dispose();

            // Evaluate if threshold met
            EvaluateThreshold();
        }

        private void EvaluateThreshold()
        {
            if (!_isTracking) return;

            int votes = _skipVoters.Count;
            bool thresholdMet = false;

            switch (_activePolicy)
            {
                case SkipPolicy.AnyoneCanSkip:
                    thresholdMet = votes >= 1;
                    break;
                case SkipPolicy.MajorityVote:
                    thresholdMet = votes > _totalPlayers / 2;
                    break;
                case SkipPolicy.AllMustSkip:
                    thresholdMet = votes >= _totalPlayers;
                    break;
                case SkipPolicy.NoSkip:
                    thresholdMet = false;
                    break;
            }

            if (thresholdMet)
            {
                BroadcastEnd(wasSkipped: true);
                StopTracking();

                // Notify trigger system
                var triggerSystem = World.GetExistingSystemManaged<CinematicTriggerSystem>();
                triggerSystem?.NotifyCinematicEnded();
            }
        }

        private void BroadcastSkipUpdate()
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new CinematicSkipUpdateRpc
            {
                CinematicId = _activeCinematicId,
                SkipVotesReceived = (byte)_skipVoters.Count
            });
            EntityManager.AddComponent<SendRpcCommandRequest>(entity);
        }

        private void BroadcastEnd(bool wasSkipped)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new CinematicEndRpc
            {
                CinematicId = _activeCinematicId,
                WasSkipped = wasSkipped
            });
            EntityManager.AddComponent<SendRpcCommandRequest>(entity);
        }
    }
}
