using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 7: Managed bridge system for cooperative interaction UI.
    ///
    /// Handles:
    /// - Detecting CoopInteraction.CurrentPlayers changes for "Waiting for X more" UI
    /// - Detecting AllPlayersReady transitions for "Ready!" indicator
    /// - Detecting CoopComplete/CoopFailed for success/failure feedback
    /// - Broadcasting ChannelProgress for parallel channeling UI bars
    ///
    /// Uses static event queue pattern (same as DamageVisualQueue, MinigameBridgeSystem).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CoopUIBridgeSystem : SystemBase
    {
        /// <summary>
        /// Static event queue for UI notifications.
        /// Game-specific UI reads and displays these.
        /// </summary>
        private static readonly Queue<CoopUIEvent> s_EventQueue = new();

        /// <summary>
        /// Track previous state per entity for transition detection.
        /// </summary>
        private readonly Dictionary<Entity, CoopUISnapshot> _previousState = new();

        private struct CoopUISnapshot
        {
            public int CurrentPlayers;
            public bool AllPlayersReady;
            public bool CoopComplete;
            public bool CoopFailed;
            public float ChannelProgress;
        }

        // --- Static API ---

        public static bool TryDequeueEvent(out CoopUIEvent evt)
        {
            if (s_EventQueue.Count > 0)
            {
                evt = s_EventQueue.Dequeue();
                return true;
            }
            evt = default;
            return false;
        }

        public static int EventCount => s_EventQueue.Count;

        protected override void OnUpdate()
        {
            var currentEntities = new HashSet<Entity>();

            foreach (var (coop, entity) in
                     SystemAPI.Query<RefRO<CoopInteraction>>()
                     .WithEntityAccess())
            {
                currentEntities.Add(entity);

                bool changed = false;
                if (_previousState.TryGetValue(entity, out var prev))
                {
                    changed = prev.CurrentPlayers != coop.ValueRO.CurrentPlayers
                              || prev.AllPlayersReady != coop.ValueRO.AllPlayersReady
                              || prev.CoopComplete != coop.ValueRO.CoopComplete
                              || prev.CoopFailed != coop.ValueRO.CoopFailed
                              || (coop.ValueRO.ChannelProgress > 0 &&
                                  prev.ChannelProgress != coop.ValueRO.ChannelProgress);
                }
                else
                {
                    // First time seeing this entity — emit event if any players
                    changed = coop.ValueRO.CurrentPlayers > 0;
                }

                if (changed)
                {
                    s_EventQueue.Enqueue(new CoopUIEvent
                    {
                        CoopEntity = entity,
                        CurrentPlayers = coop.ValueRO.CurrentPlayers,
                        RequiredPlayers = coop.ValueRO.RequiredPlayers,
                        AllReady = coop.ValueRO.AllPlayersReady,
                        Complete = coop.ValueRO.CoopComplete,
                        Failed = coop.ValueRO.CoopFailed,
                        ChannelProgress = coop.ValueRO.ChannelProgress
                    });
                }

                _previousState[entity] = new CoopUISnapshot
                {
                    CurrentPlayers = coop.ValueRO.CurrentPlayers,
                    AllPlayersReady = coop.ValueRO.AllPlayersReady,
                    CoopComplete = coop.ValueRO.CoopComplete,
                    CoopFailed = coop.ValueRO.CoopFailed,
                    ChannelProgress = coop.ValueRO.ChannelProgress
                };
            }

            // Clean up destroyed entities
            var staleEntities = new List<Entity>();
            foreach (var kvp in _previousState)
            {
                if (!currentEntities.Contains(kvp.Key))
                    staleEntities.Add(kvp.Key);
            }
            foreach (var stale in staleEntities)
            {
                _previousState.Remove(stale);
            }
        }
    }
}
