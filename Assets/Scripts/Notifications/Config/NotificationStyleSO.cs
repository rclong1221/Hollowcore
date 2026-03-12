using UnityEngine;

namespace DIG.Notifications.Config
{
    /// <summary>
    /// EPIC 18.3: Visual style definition for a notification type.
    /// Create via Assets > Create > DIG > Notifications > Style.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Notifications/Style", fileName = "NewNotificationStyle")]
    public class NotificationStyleSO : ScriptableObject
    {
        [Header("Colors")]
        public Color BackgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
        public Color BorderColor = new Color(0.3f, 0.6f, 1f, 0.8f);
        public Color TitleColor = Color.white;
        public Color BodyColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        public Color IconTint = Color.white;

        [Header("Timing")]
        [Tooltip("Default display duration in seconds. Overridden by NotificationData.Duration if > 0.")]
        public float DefaultDuration = 4f;

        [Tooltip("CSS transition duration for enter/exit animations in seconds.")]
        public float AnimationDuration = 0.3f;

        [Header("Audio")]
        [Tooltip("Sound played when this notification appears. Overridden by NotificationData.Sound.")]
        public AudioClip Sound;

        [Range(0f, 1f)]
        public float SoundVolume = 0.5f;

        [Header("Priority")]
        [Tooltip("Default priority when NotificationData.Priority is not set.")]
        public NotificationPriority DefaultPriority = NotificationPriority.Normal;
    }
}
