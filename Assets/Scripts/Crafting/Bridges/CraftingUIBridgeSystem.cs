using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Interaction;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Managed SystemBase in PresentationSystemGroup that bridges ECS crafting state to UI.
    /// Watches local player's StationSessionState — when at a station with CraftingStation,
    /// opens crafting UI and pushes queue/output data to CraftingUILink providers.
    /// Follows StationSessionBridgeSystem + CombatUIBridgeSystem patterns.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class CraftingUIBridgeSystem : SystemBase
    {
        private EntityQuery _registryQuery;
        private bool _wasAtCraftingStation;
        private Entity _currentStationEntity;
        private int _diagnosticFrameCounter;
        private const int DiagnosticGraceFrames = 60;

        private readonly List<CraftQueueEntry> _queueBuffer = new(4);
        private readonly List<CraftOutputEntry> _outputBuffer = new(4);

        protected override void OnCreate()
        {
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<RecipeRegistryManaged>());
        }

        protected override void OnUpdate()
        {
            if (_diagnosticFrameCounter < DiagnosticGraceFrames)
            {
                _diagnosticFrameCounter++;
            }

            // Process notification queue
            ProcessEventQueue();

            // Find local player's session state
            Entity localStationEntity = Entity.Null;
            bool isAtCraftingStation = false;

            foreach (var (sessionState, _) in
                SystemAPI.Query<RefRO<StationSessionState>, RefRO<GhostOwnerIsLocal>>())
            {
                if (!sessionState.ValueRO.IsInSession) break;

                var stationEntity = sessionState.ValueRO.SessionEntity;
                if (stationEntity == Entity.Null || !EntityManager.Exists(stationEntity)) break;
                if (!EntityManager.HasComponent<CraftingStation>(stationEntity)) break;

                localStationEntity = stationEntity;
                isAtCraftingStation = true;
                break;
            }

            // Detect transitions
            if (isAtCraftingStation && !_wasAtCraftingStation)
            {
                OnCraftingSessionEntered(localStationEntity);
            }
            else if (!isAtCraftingStation && _wasAtCraftingStation)
            {
                OnCraftingSessionExited();
            }

            _wasAtCraftingStation = isAtCraftingStation;
            _currentStationEntity = localStationEntity;

            // If at a crafting station, push queue/output data
            if (isAtCraftingStation)
            {
                PushStationData(localStationEntity);
            }
        }

        private void OnCraftingSessionEntered(Entity stationEntity)
        {
            var station = EntityManager.GetComponentData<CraftingStation>(stationEntity);

            if (EntityManager.HasComponent<InteractionSession>(stationEntity))
            {
                var session = EntityManager.GetComponentData<InteractionSession>(stationEntity);
                var link = CraftingUILink.Get(session.SessionID);
                link?.OpenCraftingUI(station.StationType, station.StationTier);
            }
        }

        private void OnCraftingSessionExited()
        {
            if (_currentStationEntity != Entity.Null && EntityManager.Exists(_currentStationEntity)
                && EntityManager.HasComponent<InteractionSession>(_currentStationEntity))
            {
                var session = EntityManager.GetComponentData<InteractionSession>(_currentStationEntity);
                var link = CraftingUILink.Get(session.SessionID);
                link?.CloseCraftingUI();
            }
        }

        private void PushStationData(Entity stationEntity)
        {
            if (!EntityManager.HasComponent<InteractionSession>(stationEntity)) return;
            var session = EntityManager.GetComponentData<InteractionSession>(stationEntity);
            var link = CraftingUILink.Get(session.SessionID);
            if (link == null) return;

            // Get registry for display names
            RecipeRegistryManaged registry = null;
            if (_registryQuery.CalculateEntityCount() > 0)
            {
                var registryEntity = _registryQuery.GetSingletonEntity();
                registry = EntityManager.GetComponentObject<RecipeRegistryManaged>(registryEntity);
            }

            // Push queue data
            if (EntityManager.HasBuffer<CraftQueueElement>(stationEntity))
            {
                var queue = EntityManager.GetBuffer<CraftQueueElement>(stationEntity, true);
                _queueBuffer.Clear();

                for (int i = 0; i < queue.Length; i++)
                {
                    var elem = queue[i];
                    var def = registry?.ManagedEntries.TryGetValue(elem.RecipeId, out var rd) == true ? rd : null;

                    float progress = elem.CraftTimeTotal > 0
                        ? elem.CraftTimeElapsed / elem.CraftTimeTotal
                        : 1f;

                    _queueBuffer.Add(new CraftQueueEntry
                    {
                        RecipeId = elem.RecipeId,
                        DisplayName = def?.DisplayName ?? $"Recipe #{elem.RecipeId}",
                        State = elem.State,
                        Progress = progress,
                        TimeRemaining = elem.CraftTimeTotal - elem.CraftTimeElapsed
                    });
                }

                link.RefreshQueue(_queueBuffer.ToArray());
            }

            // Push output data
            if (EntityManager.HasBuffer<CraftOutputElement>(stationEntity))
            {
                var outputs = EntityManager.GetBuffer<CraftOutputElement>(stationEntity, true);
                _outputBuffer.Clear();

                for (int i = 0; i < outputs.Length; i++)
                {
                    var output = outputs[i];
                    var def = registry?.ManagedEntries.TryGetValue(output.RecipeId, out var rd) == true ? rd : null;

                    _outputBuffer.Add(new CraftOutputEntry
                    {
                        RecipeId = output.RecipeId,
                        DisplayName = def?.DisplayName ?? $"Recipe #{output.RecipeId}",
                        Quantity = output.OutputQuantity,
                        OutputIndex = i
                    });
                }

                link.RefreshOutputs(_outputBuffer.ToArray());
            }
        }

        private void ProcessEventQueue()
        {
            while (CraftingEventQueue.TryDequeue(out var evt))
            {
                // UI notification consumers can be added here via a provider registry
                // For now, the events are consumed to prevent queue growth
            }
        }
    }
}
