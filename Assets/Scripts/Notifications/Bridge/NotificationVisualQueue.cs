using Unity.Collections;

namespace DIG.Notifications.Bridge
{
    /// <summary>
    /// EPIC 18.3: Visual event data for ECS → UI notification bridge.
    /// </summary>
    public struct NotificationVisualEvent
    {
        public NotificationChannel Channel;
        public NotificationPriority Priority;
        public FixedString64Bytes Title;
        public FixedString128Bytes Body;
        public FixedString32Bytes StyleId;
        public FixedString64Bytes DeduplicationKey;
        public float Duration;
    }

    /// <summary>
    /// EPIC 18.3: Static NativeQueue bridge for ECS → UI notification events.
    /// Follows AchievementVisualQueue / LevelUpVisualQueue / DamageVisualQueue pattern.
    /// </summary>
    public static class NotificationVisualQueue
    {
        private static NativeQueue<NotificationVisualEvent> _queue;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _queue = new NativeQueue<NotificationVisualEvent>(Allocator.Persistent);
            _initialized = true;
        }

        public static void Dispose()
        {
            if (!_initialized) return;
            if (_queue.IsCreated) _queue.Dispose();
            _initialized = false;
        }

        public static void Enqueue(NotificationVisualEvent evt)
        {
            if (!_initialized) Initialize();
            _queue.Enqueue(evt);
        }

        public static bool TryDequeue(out NotificationVisualEvent evt)
        {
            if (!_initialized) { evt = default; return false; }
            return _queue.TryDequeue(out evt);
        }

        public static int Count => _initialized && _queue.IsCreated ? _queue.Count : 0;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_initialized)
            {
                if (_queue.IsCreated) _queue.Dispose();
                _initialized = false;
            }
        }
    }
}
