using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Persistence.UI
{
    /// <summary>
    /// EPIC 16.15: Reads SaveComplete/LoadComplete transient entities and forwards
    /// notifications to registered UI providers. Runs in PresentationSystemGroup.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SaveUIBridgeSystem : SystemBase
    {
        private EntityQuery _saveCompleteQuery;
        private EntityQuery _loadCompleteQuery;

        private int _diagnosticFrameCounter;
        private const int DiagnosticGraceFrames = 120;

        protected override void OnCreate()
        {
            _saveCompleteQuery = GetEntityQuery(ComponentType.ReadOnly<SaveComplete>());
            _loadCompleteQuery = GetEntityQuery(ComponentType.ReadOnly<LoadComplete>());
        }

        protected override void OnUpdate()
        {
            // One-time diagnostic after grace period
            if (_diagnosticFrameCounter >= 0)
            {
                _diagnosticFrameCounter++;
                if (_diagnosticFrameCounter >= DiagnosticGraceFrames)
                {
                    _diagnosticFrameCounter = -1;
                    if (!SaveUIRegistry.HasNotifications)
                        Debug.LogWarning("[Persistence] No ISaveNotificationProvider registered. Save/load toasts will not display.");
                }
            }

            // Process save completions
            if (!_saveCompleteQuery.IsEmpty)
            {
                var saves = _saveCompleteQuery.ToComponentDataArray<SaveComplete>(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < saves.Length; i++)
                {
                    var sc = saves[i];
                    var notificationType = sc.TriggerSource switch
                    {
                        SaveTriggerSource.Autosave => SaveNotificationType.AutosaveCompleted,
                        SaveTriggerSource.Checkpoint => SaveNotificationType.CheckpointSaved,
                        _ => SaveNotificationType.SaveCompleted
                    };

                    if (SaveUIRegistry.HasNotifications)
                    {
                        SaveUIRegistry.Notifications.ShowNotification(new SaveNotification
                        {
                            Type = notificationType,
                            SlotIndex = sc.SlotIndex,
                            Timestamp = (float)SystemAPI.Time.ElapsedTime
                        });
                    }

                    if (SaveUIRegistry.HasProgress)
                        SaveUIRegistry.Progress.HideProgress();
                }
                saves.Dispose();

                EntityManager.DestroyEntity(_saveCompleteQuery);
            }

            // Process load completions
            if (!_loadCompleteQuery.IsEmpty)
            {
                var loads = _loadCompleteQuery.ToComponentDataArray<LoadComplete>(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < loads.Length; i++)
                {
                    var lc = loads[i];
                    var type = lc.Success
                        ? SaveNotificationType.LoadCompleted
                        : SaveNotificationType.LoadFailed;

                    if (SaveUIRegistry.HasNotifications)
                    {
                        SaveUIRegistry.Notifications.ShowNotification(new SaveNotification
                        {
                            Type = type,
                            SlotIndex = lc.SlotIndex,
                            Timestamp = (float)SystemAPI.Time.ElapsedTime
                        });
                    }

                    if (SaveUIRegistry.HasProgress)
                        SaveUIRegistry.Progress.HideProgress();
                }
                loads.Dispose();

                EntityManager.DestroyEntity(_loadCompleteQuery);
            }
        }
    }
}
