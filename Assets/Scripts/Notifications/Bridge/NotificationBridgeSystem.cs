using Unity.Entities;
using UnityEngine;

namespace DIG.Notifications.Bridge
{
    /// <summary>
    /// EPIC 18.3: Managed bridge system that drains NotificationVisualQueue
    /// and optionally AchievementVisualQueue, LevelUpVisualQueue, QuestEventQueue
    /// into the unified NotificationService.
    /// Follows AchievementUIBridgeSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class NotificationBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!NotificationService.HasInstance) return;
            var service = NotificationService.Instance;

            CompleteDependency();

            // Drain the unified NotificationVisualQueue
            while (NotificationVisualQueue.TryDequeue(out var evt))
            {
                service.Show(new NotificationData
                {
                    Channel = evt.Channel,
                    Priority = evt.Priority,
                    Title = evt.Title.ToString(),
                    Body = evt.Body.ToString(),
                    StyleId = evt.StyleId.Length > 0 ? evt.StyleId.ToString() : null,
                    DeduplicationKey = evt.DeduplicationKey.Length > 0 ? evt.DeduplicationKey.ToString() : null,
                    Duration = evt.Duration,
                });
            }

            // Optionally drain AchievementVisualQueue
            if (service.UseUnifiedAchievements)
            {
                while (DIG.Achievement.AchievementVisualQueue.TryDequeue(out var achieveEvt))
                {
                    service.Show(new NotificationData
                    {
                        Channel = NotificationChannel.Toast,
                        Priority = NotificationPriority.High,
                        Title = achieveEvt.AchievementName.ToString(),
                        Body = achieveEvt.Description.ToString(),
                        StyleId = "Achievement",
                        DeduplicationKey = $"achieve_{achieveEvt.AchievementId}",
                    });
                }
            }

            // Optionally drain LevelUpVisualQueue
            if (service.UseUnifiedLevelUp)
            {
                while (DIG.Progression.LevelUpVisualQueue.TryDequeueLevelUp(out var levelEvt))
                {
                    service.Show(new NotificationData
                    {
                        Channel = NotificationChannel.CenterScreen,
                        Priority = NotificationPriority.Critical,
                        Title = $"Level {levelEvt.NewLevel}!",
                        Body = levelEvt.StatPointsAwarded > 0
                            ? $"+{levelEvt.StatPointsAwarded} Stat Points"
                            : "You leveled up!",
                        StyleId = "LevelUp",
                    });
                }
            }

            // Optionally drain QuestEventQueue
            if (service.UseUnifiedQuests)
            {
                while (DIG.Quest.QuestEventQueue.TryDequeue(out var questEvt))
                {
                    string title;
                    string body;
                    var priority = NotificationPriority.Normal;

                    switch (questEvt.Type)
                    {
                        case DIG.Quest.QuestUIEventType.QuestAccepted:
                            title = "Quest Accepted";
                            body = $"Quest #{questEvt.QuestId}";
                            break;
                        case DIG.Quest.QuestUIEventType.QuestCompleted:
                            title = "Quest Complete!";
                            body = $"Quest #{questEvt.QuestId}";
                            priority = NotificationPriority.High;
                            break;
                        case DIG.Quest.QuestUIEventType.ObjectiveUpdated:
                            title = "Objective Updated";
                            body = $"{questEvt.CurrentCount}/{questEvt.RequiredCount}";
                            break;
                        default:
                            title = "Quest Update";
                            body = "";
                            break;
                    }

                    service.Show(new NotificationData
                    {
                        Channel = NotificationChannel.Toast,
                        Priority = priority,
                        Title = title,
                        Body = body,
                        StyleId = "Quest",
                        DeduplicationKey = $"quest_{questEvt.QuestId}_{questEvt.Type}",
                    });
                }
            }
        }
    }
}
