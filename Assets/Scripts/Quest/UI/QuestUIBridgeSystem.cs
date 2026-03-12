using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Managed SystemBase in PresentationSystemGroup that bridges ECS quest state to UI.
    /// Reads QuestProgress + ObjectiveProgress for the local player, pushes to QuestUIRegistry providers.
    /// Follows CombatUIBridgeSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class QuestUIBridgeSystem : SystemBase
    {
        private EntityQuery _questQuery;
        private EntityQuery _registryQuery;
        private int _diagnosticFrameCounter;
        private const int DiagnosticGraceFrames = 60;
        private Entity _localPlayerEntity;
        private readonly List<QuestLogEntry> _entryBuffer = new(8);

        protected override void OnCreate()
        {
            _questQuery = GetEntityQuery(
                ComponentType.ReadOnly<QuestProgress>(),
                ComponentType.ReadOnly<QuestPlayerLink>(),
                ComponentType.ReadOnly<ObjectiveProgress>()
            );
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<QuestRegistryManaged>());
        }

        protected override void OnUpdate()
        {
            // One-time diagnostics after grace period
            if (_diagnosticFrameCounter < DiagnosticGraceFrames)
            {
                _diagnosticFrameCounter++;
                if (_diagnosticFrameCounter == DiagnosticGraceFrames)
                    RunStartupDiagnostics();
            }

            // Process notification queue (always, even without log providers)
            ProcessNotificationQueue();

            bool hasProviders = QuestUIRegistry.HasQuestLog || QuestUIRegistry.HasObjectiveTracker;
            if (!hasProviders) return;

            // Find local player entity
            if (_localPlayerEntity == Entity.Null || !EntityManager.Exists(_localPlayerEntity))
            {
                _localPlayerEntity = FindLocalPlayer();
                if (_localPlayerEntity == Entity.Null) return;
            }

            // Gather active quests for this player
            QuestRegistryManaged registry = null;
            if (_registryQuery.CalculateEntityCount() > 0)
            {
                var registryEntity = _registryQuery.GetSingletonEntity();
                registry = EntityManager.GetComponentObject<QuestRegistryManaged>(registryEntity);
            }

            _entryBuffer.Clear();
            var entities = _questQuery.ToEntityArray(Allocator.Temp);
            var progressArray = _questQuery.ToComponentDataArray<QuestProgress>(Allocator.Temp);
            var links = _questQuery.ToComponentDataArray<QuestPlayerLink>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (links[i].PlayerEntity != _localPlayerEntity) continue;
                var progress = progressArray[i];
                if (progress.State != QuestState.Active && progress.State != QuestState.Completed)
                    continue;

                var objectives = EntityManager.GetBuffer<ObjectiveProgress>(entities[i], true);
                var def = registry?.Database.GetQuest(progress.QuestId);

                var entry = new QuestLogEntry
                {
                    QuestId = progress.QuestId,
                    DisplayName = def?.DisplayName ?? $"Quest #{progress.QuestId}",
                    Description = def?.Description ?? "",
                    Category = def?.Category ?? QuestCategory.Side,
                    State = progress.State,
                    TimeRemaining = progress.TimeRemaining,
                    Objectives = new ObjectiveEntry[objectives.Length]
                };

                for (int o = 0; o < objectives.Length; o++)
                {
                    var obj = objectives[o];
                    string objDesc = "";
                    if (def != null)
                    {
                        foreach (var objDef in def.Objectives)
                        {
                            if (objDef.ObjectiveId == obj.ObjectiveId)
                            {
                                objDesc = objDef.Description;
                                break;
                            }
                        }
                    }

                    entry.Objectives[o] = new ObjectiveEntry
                    {
                        ObjectiveId = obj.ObjectiveId,
                        Description = objDesc,
                        State = obj.State,
                        CurrentCount = obj.CurrentCount,
                        RequiredCount = obj.RequiredCount,
                        IsOptional = obj.IsOptional,
                        IsHidden = obj.IsHidden
                    };
                }

                _entryBuffer.Add(entry);
            }

            entities.Dispose();
            progressArray.Dispose();
            links.Dispose();

            // Push to providers
            if (QuestUIRegistry.HasQuestLog)
                QuestUIRegistry.QuestLog.UpdateQuestLog(_entryBuffer.ToArray());

            if (QuestUIRegistry.HasObjectiveTracker && _entryBuffer.Count > 0)
                QuestUIRegistry.ObjectiveTracker.UpdateTrackedObjectives(_entryBuffer[0]);
        }

        private void ProcessNotificationQueue()
        {
            if (!QuestUIRegistry.HasNotifications) return;

            QuestRegistryManaged registry = null;
            if (_registryQuery.CalculateEntityCount() > 0)
            {
                var registryEntity = _registryQuery.GetSingletonEntity();
                registry = EntityManager.GetComponentObject<QuestRegistryManaged>(registryEntity);
            }

            while (QuestEventQueue.TryDequeue(out var evt))
            {
                string questName = registry?.Database.GetQuest(evt.QuestId)?.DisplayName ?? $"Quest #{evt.QuestId}";

                switch (evt.Type)
                {
                    case QuestUIEventType.QuestAccepted:
                        QuestUIRegistry.Notifications.ShowQuestAccepted(questName);
                        break;
                    case QuestUIEventType.QuestCompleted:
                    case QuestUIEventType.QuestTurnedIn:
                        QuestUIRegistry.Notifications.ShowQuestCompleted(questName);
                        break;
                    case QuestUIEventType.QuestFailed:
                        QuestUIRegistry.Notifications.ShowQuestFailed(questName);
                        break;
                    case QuestUIEventType.ObjectiveUpdated:
                        QuestUIRegistry.Notifications.ShowObjectiveProgress("", evt.CurrentCount, evt.RequiredCount);
                        break;
                }
            }
        }

        private Entity FindLocalPlayer()
        {
            foreach (var (_, entity) in SystemAPI.Query<RefRO<Unity.NetCode.GhostOwnerIsLocal>>().WithEntityAccess())
            {
                if (EntityManager.HasComponent<PlayerTag>(entity))
                    return entity;
            }
            return Entity.Null;
        }

        private void RunStartupDiagnostics()
        {
            if (!QuestUIRegistry.HasQuestLog)
                UnityEngine.Debug.LogWarning("[QuestUI] No IQuestLogProvider registered. Quest log will not display.");
            if (!QuestUIRegistry.HasNotifications)
                UnityEngine.Debug.LogWarning("[QuestUI] No IQuestNotificationProvider registered. Quest notifications will not display.");
        }

    }
}
