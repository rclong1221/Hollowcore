using UnityEngine;

namespace DIG.Notifications.Config
{
    /// <summary>
    /// EPIC 18.3: Master notification system configuration.
    /// Create via Assets > Create > DIG > Notifications > Config.
    /// Place in Assets/Resources/ as "NotificationConfig".
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Notifications/Config", fileName = "NotificationConfig")]
    public class NotificationConfigSO : ScriptableObject
    {
        [Header("Channel Configuration")]
        public NotificationChannelConfig ToastConfig = new NotificationChannelConfig
        {
            MaxVisible = 3,
            MaxQueueSize = 10,
            Position = NotificationPosition.TopRight,
            StackDirection = StackDirection.Down,
            DefaultDuration = 4f,
        };

        public NotificationChannelConfig BannerConfig = new NotificationChannelConfig
        {
            MaxVisible = 1,
            MaxQueueSize = 5,
            Position = NotificationPosition.TopCenter,
            StackDirection = StackDirection.Down,
            DefaultDuration = 3f,
        };

        public NotificationChannelConfig CenterConfig = new NotificationChannelConfig
        {
            MaxVisible = 1,
            MaxQueueSize = 3,
            Position = NotificationPosition.Center,
            StackDirection = StackDirection.Down,
            DefaultDuration = 0f, // Center screen persists until dismissed
        };

        [Header("History")]
        [Tooltip("Number of recent notifications to keep in the history ring buffer.")]
        public int HistoryRingSize = 50;

        [Header("Default Styles")]
        [Tooltip("Default style when no StyleId is specified on NotificationData.")]
        public NotificationStyleSO DefaultToastStyle;
        public NotificationStyleSO DefaultBannerStyle;
        public NotificationStyleSO DefaultCenterStyle;

        [Header("ECS Bridge — Opt-in Migration")]
        [Tooltip("When true, NotificationBridgeSystem drains AchievementVisualQueue instead of the existing AchievementUIBridgeSystem.")]
        public bool UseUnifiedAchievements;

        [Tooltip("When true, NotificationBridgeSystem drains LevelUpVisualQueue instead of the existing ProgressionUIBridgeSystem.")]
        public bool UseUnifiedLevelUp;

        [Tooltip("When true, NotificationBridgeSystem drains QuestEventQueue.")]
        public bool UseUnifiedQuests;
    }
}
