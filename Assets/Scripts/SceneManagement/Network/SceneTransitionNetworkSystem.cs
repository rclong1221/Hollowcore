using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.SceneManagement.Network
{
    /// <summary>
    /// EPIC 18.6 — SERVER: Broadcasts SceneTransitionRequest, collects SceneReadyAck
    /// from all clients, broadcasts SceneTransitionComplete when all have acknowledged.
    /// Pattern: CinematicRpcReceiveSystem (manual EntityQuery + ToComponentDataArray + destroy).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SceneTransitionServerSystem : SystemBase
    {
        private EntityQuery _ackQuery;
        private uint _currentTransitionId;
        private int _expectedAcks;
        private int _receivedAcks;
        private bool _waitingForAcks;
        private float _ackTimeout;
        private const float AckTimeoutSeconds = 30f;

        protected override void OnCreate()
        {
            _ackQuery = GetEntityQuery(
                ComponentType.ReadOnly<SceneReadyAck>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
        }

        protected override void OnUpdate()
        {
            if (!_waitingForAcks) return;

            // Collect acks
            if (_ackQuery.CalculateEntityCount() > 0)
            {
                var acks = _ackQuery.ToComponentDataArray<SceneReadyAck>(Allocator.Temp);
                var entities = _ackQuery.ToEntityArray(Allocator.Temp);

                for (int i = 0; i < acks.Length; i++)
                {
                    if (acks[i].TransitionId == _currentTransitionId)
                        _receivedAcks++;
                    EntityManager.DestroyEntity(entities[i]);
                }

                acks.Dispose();
                entities.Dispose();
            }

            // All clients ready → broadcast complete
            if (_receivedAcks >= _expectedAcks)
            {
                BroadcastComplete();
                _waitingForAcks = false;
                Debug.Log($"[SceneTransitionServer] All {_expectedAcks} clients ready for transition {_currentTransitionId}");
                return;
            }

            // Timeout
            _ackTimeout -= SystemAPI.Time.DeltaTime;
            if (_ackTimeout <= 0f)
            {
                Debug.LogWarning($"[SceneTransitionServer] Timeout waiting for acks ({_receivedAcks}/{_expectedAcks}). Proceeding.");
                BroadcastComplete();
                _waitingForAcks = false;
            }
        }

        /// <summary>
        /// Called by SceneService (managed code) to initiate server-side transition.
        /// </summary>
        public void BeginTransition(string stateId, int clientCount)
        {
            _currentTransitionId++;
            _expectedAcks = clientCount;
            _receivedAcks = 0;
            _waitingForAcks = true;
            _ackTimeout = AckTimeoutSeconds;

            var reqEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(reqEntity, new SceneTransitionRequest
            {
                TargetStateHash = Animator.StringToHash(stateId),
                TransitionId = _currentTransitionId
            });
            EntityManager.AddComponentData(reqEntity, new SendRpcCommandRequest());

            Debug.Log($"[SceneTransitionServer] Broadcast transition {_currentTransitionId} to state '{stateId}', expecting {clientCount} acks");
        }

        private void BroadcastComplete()
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new SceneTransitionComplete
            {
                TransitionId = _currentTransitionId
            });
            EntityManager.AddComponentData(entity, new SendRpcCommandRequest());
        }
    }

    /// <summary>
    /// EPIC 18.6 — CLIENT: Receives SceneTransitionRequest, notifies SceneService,
    /// sends SceneReadyAck when loading completes, listens for SceneTransitionComplete.
    /// Pattern: CinematicRpcReceiveSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SceneTransitionClientSystem : SystemBase
    {
        private EntityQuery _requestQuery;
        private EntityQuery _completeQuery;
        private uint _pendingTransitionId;
        private bool _ackSent;

        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(
                ComponentType.ReadOnly<SceneTransitionRequest>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _completeQuery = GetEntityQuery(
                ComponentType.ReadOnly<SceneTransitionComplete>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
        }

        protected override void OnUpdate()
        {
            // Process SceneTransitionRequest
            if (_requestQuery.CalculateEntityCount() > 0)
            {
                var requests = _requestQuery.ToComponentDataArray<SceneTransitionRequest>(Allocator.Temp);
                var entities = _requestQuery.ToEntityArray(Allocator.Temp);

                for (int i = 0; i < requests.Length; i++)
                {
                    _pendingTransitionId = requests[i].TransitionId;
                    _ackSent = false;
                    Debug.Log($"[SceneTransitionClient] Received transition request {_pendingTransitionId}");
                }

                for (int i = 0; i < entities.Length; i++)
                    EntityManager.DestroyEntity(entities[i]);

                requests.Dispose();
                entities.Dispose();
            }

            // Send ack when SceneService finishes loading
            if (!_ackSent && _pendingTransitionId > 0)
            {
                var sceneService = SceneService.Instance;
                if (sceneService != null && !sceneService.IsLoading)
                {
                    _ackSent = true;
                    SendAck();
                }
            }

            // Process SceneTransitionComplete
            if (_completeQuery.CalculateEntityCount() > 0)
            {
                var completes = _completeQuery.ToComponentDataArray<SceneTransitionComplete>(Allocator.Temp);
                var compEntities = _completeQuery.ToEntityArray(Allocator.Temp);

                for (int i = 0; i < completes.Length; i++)
                {
                    if (completes[i].TransitionId == _pendingTransitionId)
                    {
                        _pendingTransitionId = 0;
                        Debug.Log("[SceneTransitionClient] All clients ready — transition complete");
                    }
                }

                for (int i = 0; i < compEntities.Length; i++)
                    EntityManager.DestroyEntity(compEntities[i]);

                completes.Dispose();
                compEntities.Dispose();
            }
        }

        private void SendAck()
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new SceneReadyAck
            {
                TransitionId = _pendingTransitionId
            });
            EntityManager.AddComponentData(entity, new SendRpcCommandRequest());
            Debug.Log($"[SceneTransitionClient] Sent ack for transition {_pendingTransitionId}");
        }
    }
}
