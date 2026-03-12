using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Server-side system detecting cinematic trigger conditions.
    /// Two sources: CinematicTrigger entities (zone proximity) and
    /// EncounterTriggerSystem integration (PlayCinematic action).
    /// Broadcasts CinematicPlayRpc to all connected clients.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CinematicTriggerSystem : SystemBase
    {
        private EntityQuery _triggerQuery;
        private EntityQuery _playerQuery;
        private EntityQuery _connectionQuery;

        // Server-side tracking of active cinematic
        private bool _cinematicActive;
        private int _activeCinematicId;
        private float _activeElapsed;
        private float _activeDuration;
        private int _totalPlayers;

        protected override void OnCreate()
        {
            _triggerQuery = GetEntityQuery(
                ComponentType.ReadWrite<CinematicTrigger>(),
                ComponentType.ReadOnly<LocalToWorld>()
            );
            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalToWorld>()
            );
            _connectionQuery = GetEntityQuery(
                ComponentType.ReadOnly<Unity.NetCode.NetworkId>()
            );

            RequireForUpdate(_triggerQuery);
        }

        protected override void OnUpdate()
        {
            // Don't trigger new cinematics while one is active
            if (_cinematicActive) return;

            var playerEntities = _playerQuery.ToEntityArray(Allocator.Temp);
            var playerTransforms = _playerQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            if (playerEntities.Length == 0)
            {
                playerEntities.Dispose();
                playerTransforms.Dispose();
                return;
            }

            var triggerEntities = _triggerQuery.ToEntityArray(Allocator.Temp);
            var triggers = _triggerQuery.ToComponentDataArray<CinematicTrigger>(Allocator.Temp);
            var triggerTransforms = _triggerQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            for (int t = 0; t < triggers.Length; t++)
            {
                var trigger = triggers[t];
                if (trigger.PlayOnce && trigger.HasPlayed) continue;

                float3 triggerPos = triggerTransforms[t].Position;
                float radiusSq = trigger.TriggerRadius * trigger.TriggerRadius;

                // Check if any player is within radius
                bool playerInRange = false;
                for (int p = 0; p < playerTransforms.Length; p++)
                {
                    float3 diff = playerTransforms[p].Position - triggerPos;
                    if (math.lengthsq(diff) <= radiusSq)
                    {
                        playerInRange = true;
                        break;
                    }
                }

                if (!playerInRange) continue;

                // Fire cinematic
                int totalPlayers = _connectionQuery.CalculateEntityCount();
                if (totalPlayers == 0) totalPlayers = 1; // Local simulation

                BroadcastPlayRpc(trigger.CinematicId, trigger.CinematicType,
                    trigger.SkipPolicy, 0f, (byte)totalPlayers);

                // Mark as played
                trigger.HasPlayed = true;
                triggers[t] = trigger;
                EntityManager.SetComponentData(triggerEntities[t], trigger);
            }

            triggerEntities.Dispose();
            triggers.Dispose();
            triggerTransforms.Dispose();
            playerEntities.Dispose();
            playerTransforms.Dispose();
        }

        /// <summary>
        /// Static-like entry point for EncounterTriggerSystem to trigger a cinematic.
        /// Called from the PlayCinematic case handler.
        /// </summary>
        public void TriggerCinematicFromEncounter(int cinematicId, CinematicType type,
            SkipPolicy skipPolicy, float duration)
        {
            if (_cinematicActive) return;

            int totalPlayers = _connectionQuery.CalculateEntityCount();
            if (totalPlayers == 0) totalPlayers = 1;

            BroadcastPlayRpc(cinematicId, type, skipPolicy, duration, (byte)totalPlayers);
        }

        private void BroadcastPlayRpc(int cinematicId, CinematicType type,
            SkipPolicy skipPolicy, float duration, byte totalPlayers)
        {
            var rpcEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(rpcEntity, new CinematicPlayRpc
            {
                CinematicId = cinematicId,
                CinematicType = type,
                SkipPolicy = skipPolicy,
                Duration = duration,
                TotalPlayers = totalPlayers
            });
            EntityManager.AddComponent<Unity.NetCode.SendRpcCommandRequest>(rpcEntity);

            _cinematicActive = true;
            _activeCinematicId = cinematicId;
            _activeElapsed = 0f;
            _activeDuration = duration;
            _totalPlayers = totalPlayers;

            // Notify server-side tracking systems
            var endSystem = World.GetExistingSystemManaged<CinematicEndSystem>();
            endSystem?.BeginTracking(cinematicId, duration);

            var skipSystem = World.GetExistingSystemManaged<CinematicSkipSystem>();
            skipSystem?.BeginTracking(cinematicId, skipPolicy, totalPlayers);
        }

        /// <summary>Called by CinematicEndSystem when cinematic completes.</summary>
        public void NotifyCinematicEnded()
        {
            _cinematicActive = false;
            _activeCinematicId = 0;
        }

        public bool IsCinematicActive => _cinematicActive;
        public int ActiveCinematicId => _activeCinematicId;
        public int TotalPlayers => _totalPlayers;
    }
}
