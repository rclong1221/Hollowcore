using System;
using System.Collections.Generic;
using DIG.Notifications.Channels;
using DIG.Notifications.Config;
using UnityEngine;

namespace DIG.Notifications
{
    /// <summary>
    /// EPIC 18.3: Singleton MonoBehaviour managing all notification channels.
    /// Provides priority queuing, deduplication, history, and audio.
    /// </summary>
    public class NotificationService : MonoBehaviour
    {
        public static NotificationService Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        private NotificationConfigSO _config;
        private AudioSource _audioSource;

        // Channels
        private INotificationChannel _toastChannel;
        private INotificationChannel _bannerChannel;
        private INotificationChannel _centerChannel;

        // Handle ID counter
        private int _nextHandleId = 1;

        // Priority queue: (data, style, assignedHandleId, arrivalOrder)
        private readonly List<QueuedNotification> _queue = new();

        // Deduplication map: dedup key → handle ID
        private readonly Dictionary<string, int> _dedupMap = new();

        // Active notification data for OnDismiss callbacks: handle ID → data
        private readonly Dictionary<int, NotificationData> _activeData = new();

        // History ring buffer
        private NotificationRecord[] _history;
        private int _historyHead;
        private int _historyCount;

        // Cached style assets
        private readonly Dictionary<string, NotificationStyleSO> _styleCache = new();

        private struct QueuedNotification
        {
            public NotificationData Data;
            public NotificationStyleSO Style;
            public int HandleId;
            public int ArrivalOrder;
        }

        private int _arrivalCounter;
        private bool _queueDirty;

        internal void Initialize(
            NotificationConfigSO config,
            INotificationChannel toastChannel,
            INotificationChannel bannerChannel,
            INotificationChannel centerChannel)
        {
            _config = config;
            _toastChannel = toastChannel;
            _bannerChannel = bannerChannel;
            _centerChannel = centerChannel;

            _history = new NotificationRecord[config.HistoryRingSize > 0 ? config.HistoryRingSize : 50];

            // Audio
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D

            // Wire dismiss callbacks for cleanup
            _toastChannel.OnDismissComplete += OnNotificationDismissed;
            _bannerChannel.OnDismissComplete += OnNotificationDismissed;
            _centerChannel.OnDismissComplete += OnNotificationDismissed;

            Instance = this;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Tick channels
            _toastChannel?.Tick(dt);
            _bannerChannel?.Tick(dt);
            _centerChannel?.Tick(dt);

            // Flush queue into channels with capacity
            FlushQueue();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Show a notification. Returns a handle for dismissal/update.
        /// If DeduplicationKey is set and a notification with that key is already showing, updates it instead.
        /// </summary>
        public NotificationHandle Show(NotificationData data)
        {
            // Dedup check
            if (!string.IsNullOrEmpty(data.DeduplicationKey) && _dedupMap.TryGetValue(data.DeduplicationKey, out int existingId))
            {
                Update(new NotificationHandle(existingId, data.Channel), data);
                return new NotificationHandle(existingId, data.Channel);
            }

            int handleId = _nextHandleId++;
            var style = ResolveStyle(data.StyleId, data.Channel);

            // Register dedup
            if (!string.IsNullOrEmpty(data.DeduplicationKey))
                _dedupMap[data.DeduplicationKey] = handleId;

            // Store active data for dismiss callback
            _activeData[handleId] = data;

            // Record in history
            RecordHistory(data, handleId);

            // Try to show immediately if channel has capacity
            var channel = GetChannel(data.Channel);
            if (channel != null && channel.HasCapacity)
            {
                ShowImmediate(channel, handleId, data, style);
            }
            else
            {
                // Enqueue and mark dirty so FlushQueue re-sorts
                _queue.Add(new QueuedNotification
                {
                    Data = data,
                    Style = style,
                    HandleId = handleId,
                    ArrivalOrder = _arrivalCounter++,
                });
                _queueDirty = true;
            }

            return new NotificationHandle(handleId, data.Channel);
        }

        /// <summary>Dismiss a specific notification.</summary>
        public void Dismiss(NotificationHandle handle)
        {
            if (!handle.IsValid) return;
            var channel = GetChannel(handle.Channel);
            channel?.Dismiss(handle.Id);
        }

        /// <summary>Dismiss all notifications across all channels.</summary>
        public void DismissAll()
        {
            _toastChannel?.Clear();
            _bannerChannel?.Clear();
            _centerChannel?.Clear();
            _queue.Clear();
        }

        /// <summary>Update an existing notification's content in-place.</summary>
        public void Update(NotificationHandle handle, NotificationData data)
        {
            if (!handle.IsValid) return;
            var channel = GetChannel(handle.Channel);
            channel?.Update(handle.Id, data);
            _activeData[handle.Id] = data;
        }

        /// <summary>Check if a notification handle is still active (visible or queued).</summary>
        public bool IsActive(NotificationHandle handle)
        {
            return handle.IsValid && _activeData.ContainsKey(handle.Id);
        }

        /// <summary>Get recent notification history. Returns oldest-first.</summary>
        public NotificationRecord[] GetHistory()
        {
            int count = Mathf.Min(_historyCount, _history.Length);
            var result = new NotificationRecord[count];

            for (int i = 0; i < count; i++)
            {
                int idx = (_historyHead - count + i + _history.Length) % _history.Length;
                result[i] = _history[idx];
            }

            return result;
        }

        /// <summary>Number of items in the history ring buffer.</summary>
        public int HistoryCount => Mathf.Min(_historyCount, _history?.Length ?? 0);

        /// <summary>Whether the config allows draining this queue type.</summary>
        public bool UseUnifiedAchievements => _config != null && _config.UseUnifiedAchievements;
        public bool UseUnifiedLevelUp => _config != null && _config.UseUnifiedLevelUp;
        public bool UseUnifiedQuests => _config != null && _config.UseUnifiedQuests;

        private void ShowImmediate(INotificationChannel channel, int handleId, NotificationData data, NotificationStyleSO style)
        {
            channel.Show(handleId, data, style);

            // Play sound
            AudioClip clip = data.Sound;
            float volume = 0.5f;
            if (clip == null && style != null)
            {
                clip = style.Sound;
                volume = style.SoundVolume;
            }
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, volume);
        }

        private void FlushQueue()
        {
            if (_queue.Count == 0) return;

            // Only re-sort when new items were added
            if (_queueDirty)
            {
                _queue.Sort((a, b) =>
                {
                    int cmp = b.Data.Priority.CompareTo(a.Data.Priority);
                    return cmp != 0 ? cmp : a.ArrivalOrder.CompareTo(b.ArrivalOrder);
                });
                _queueDirty = false;
            }

            // Iterate forward so highest priority (index 0) gets shown first.
            // Collect indices to remove, then remove in reverse to preserve indices.
            int removed = 0;
            for (int i = 0; i < _queue.Count; i++)
            {
                var queued = _queue[i];
                var channel = GetChannel(queued.Data.Channel);
                if (channel != null && channel.HasCapacity)
                {
                    ShowImmediate(channel, queued.HandleId, queued.Data, queued.Style);
                    _queue.RemoveAt(i);
                    i--; // Adjust index after removal
                    removed++;
                }
            }
        }

        private void OnNotificationDismissed(int handleId)
        {
            if (!_activeData.TryGetValue(handleId, out var data)) return;

            data.OnDismiss?.Invoke();
            _activeData.Remove(handleId);

            // Clean dedup
            if (!string.IsNullOrEmpty(data.DeduplicationKey))
                _dedupMap.Remove(data.DeduplicationKey);
        }

        private void RecordHistory(NotificationData data, int handleId)
        {
            if (_history == null) return;

            var record = new NotificationRecord
            {
                Data = data,
                Timestamp = Time.unscaledTime,
                WasSeen = true,
                WasActioned = false,
                Handle = new NotificationHandle(handleId, data.Channel),
            };

            _history[_historyHead] = record;
            _historyHead = (_historyHead + 1) % _history.Length;
            _historyCount++;
        }

        private NotificationStyleSO ResolveStyle(string styleId, NotificationChannel channel)
        {
            // Explicit style ID
            if (!string.IsNullOrEmpty(styleId))
            {
                if (!_styleCache.TryGetValue(styleId, out var style))
                {
                    style = Resources.Load<NotificationStyleSO>($"NotificationStyles/{styleId}");
                    if (style != null) _styleCache[styleId] = style;
                }
                if (style != null) return style;
            }

            // Channel default from config
            if (_config == null) return null;
            return channel switch
            {
                NotificationChannel.Toast => _config.DefaultToastStyle,
                NotificationChannel.Banner => _config.DefaultBannerStyle,
                NotificationChannel.CenterScreen => _config.DefaultCenterStyle,
                _ => null,
            };
        }

        private INotificationChannel GetChannel(NotificationChannel channel)
        {
            return channel switch
            {
                NotificationChannel.Toast => _toastChannel,
                NotificationChannel.Banner => _bannerChannel,
                NotificationChannel.CenterScreen => _centerChannel,
                _ => _toastChannel,
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
