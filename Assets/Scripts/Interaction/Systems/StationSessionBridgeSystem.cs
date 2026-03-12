using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using DIG.Interaction.Bridges;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 2: Managed bridge system for station session UI.
    ///
    /// Watches for StationSessionState.IsInSession changes on the local player
    /// and calls StationUILink.OpenUI() / CloseUI() accordingly.
    ///
    /// Follows the same static registry pattern as InteractableHybridBridgeSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class StationSessionBridgeSystem : SystemBase
    {
        /// <summary>
        /// Static registry mapping SessionID -> StationUILink.
        /// Populated by StationUILink.OnEnable/OnDisable.
        /// </summary>
        private static readonly Dictionary<int, StationUILink> s_SessionIdToLink = new();

        /// <summary>
        /// Track previous session state to detect transitions.
        /// </summary>
        private bool _wasInSession;
        private Entity _previousStationEntity;

        /// <summary>
        /// Register a station UI link by its session ID.
        /// Called by StationUILink.OnEnable.
        /// </summary>
        public static void RegisterStationUI(int sessionId, StationUILink link)
        {
            if (link != null)
            {
                s_SessionIdToLink[sessionId] = link;
            }
        }

        /// <summary>
        /// Unregister a station UI link.
        /// Called by StationUILink.OnDisable.
        /// </summary>
        public static void UnregisterStationUI(int sessionId)
        {
            s_SessionIdToLink.Remove(sessionId);
        }

        /// <summary>
        /// Get a station UI link by session ID.
        /// </summary>
        public static StationUILink GetStationUI(int sessionId)
        {
            s_SessionIdToLink.TryGetValue(sessionId, out var link);
            return link;
        }

        protected override void OnUpdate()
        {
            // Find the local player's session state
            foreach (var (sessionState, interactAbility) in
                     SystemAPI.Query<RefRO<StationSessionState>, RefRO<InteractAbility>>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                var session = sessionState.ValueRO;
                bool isInSession = session.IsInSession;

                // Detect session entry
                if (isInSession && !_wasInSession)
                {
                    OnSessionEntered(session.SessionEntity);
                }
                // Detect session exit
                else if (!isInSession && _wasInSession)
                {
                    OnSessionExited(_previousStationEntity);
                }

                _wasInSession = isInSession;
                _previousStationEntity = session.SessionEntity;
                return; // Only process local player
            }

            // If no local player found but was in session, handle cleanup
            if (_wasInSession)
            {
                OnSessionExited(_previousStationEntity);
                _wasInSession = false;
                _previousStationEntity = Entity.Null;
            }
        }

        private void OnSessionEntered(Entity stationEntity)
        {
            if (stationEntity == Entity.Null)
                return;

            if (!SystemAPI.HasComponent<InteractionSession>(stationEntity))
                return;

            var interactionSession = SystemAPI.GetComponent<InteractionSession>(stationEntity);
            int sessionId = interactionSession.SessionID;

            var link = GetStationUI(sessionId);
            if (link != null)
            {
                link.OpenUI(interactionSession.SessionType);
            }
        }

        private void OnSessionExited(Entity stationEntity)
        {
            if (stationEntity == Entity.Null)
                return;

            if (!SystemAPI.HasComponent<InteractionSession>(stationEntity))
                return;

            var interactionSession = SystemAPI.GetComponent<InteractionSession>(stationEntity);
            int sessionId = interactionSession.SessionID;

            var link = GetStationUI(sessionId);
            if (link != null)
            {
                link.CloseUI();
            }
        }
    }
}
