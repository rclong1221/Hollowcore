using UnityEditor;
using UnityEngine;

namespace DIG.Notifications.Editor.Modules
{
    /// <summary>
    /// EPIC 18.3: Preview module — fire test notifications in Play Mode with configurable fields.
    /// </summary>
    public class NotificationPreviewModule : INotificationModule
    {
        private NotificationChannel _channel = NotificationChannel.Toast;
        private NotificationPriority _priority = NotificationPriority.Normal;
        private string _title = "Test Notification";
        private string _body = "This is a test notification body.";
        private string _styleId = "";
        private string _dedupKey = "";
        private string _actionLabel = "";
        private float _duration;
        private int _burstCount = 1;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Notification Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to fire test notifications.", MessageType.Info);
                return;
            }

            if (!NotificationService.HasInstance)
            {
                EditorGUILayout.HelpBox("NotificationService is not initialized. Ensure NotificationServiceBootstrap is in the scene.", MessageType.Warning);
                return;
            }

            _channel = (NotificationChannel)EditorGUILayout.EnumPopup("Channel", _channel);
            _priority = (NotificationPriority)EditorGUILayout.EnumPopup("Priority", _priority);
            _title = EditorGUILayout.TextField("Title", _title);
            _body = EditorGUILayout.TextField("Body", _body);
            _styleId = EditorGUILayout.TextField("Style ID", _styleId);
            _dedupKey = EditorGUILayout.TextField("Dedup Key", _dedupKey);
            _actionLabel = EditorGUILayout.TextField("Action Button", _actionLabel);
            _duration = EditorGUILayout.FloatField("Duration (0=default)", _duration);
            _burstCount = EditorGUILayout.IntSlider("Burst Count", _burstCount, 1, 20);

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Fire Notification", GUILayout.Height(30)))
            {
                for (int i = 0; i < _burstCount; i++)
                {
                    string title = _burstCount > 1 ? $"{_title} #{i + 1}" : _title;
                    string dedupKey = string.IsNullOrEmpty(_dedupKey) ? null : _dedupKey;
                    string actionLabel = string.IsNullOrEmpty(_actionLabel) ? null : _actionLabel;
                    string styleId = string.IsNullOrEmpty(_styleId) ? null : _styleId;

                    NotificationService.Instance.Show(new NotificationData
                    {
                        Channel = _channel,
                        Priority = _priority,
                        Title = title,
                        Body = _body,
                        StyleId = styleId,
                        DeduplicationKey = dedupKey,
                        ActionButtonLabel = actionLabel,
                        Duration = _duration,
                        OnAction = actionLabel != null ? () => Debug.Log($"[NotificationPreview] Action clicked: {title}") : null,
                    });
                }
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dismiss All"))
                NotificationService.Instance.DismissAll();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("History", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total records: {NotificationService.Instance.HistoryCount}");
        }
    }
}
