using Unity.Collections;

namespace DIG.Tutorial.Bridge
{
    /// <summary>
    /// EPIC 18.4: Visual event data for ECS → UI tutorial bridge.
    /// </summary>
    public struct TutorialVisualEvent
    {
        public FixedString64Bytes SequenceId;
    }

    /// <summary>
    /// EPIC 18.4: Static NativeQueue bridge for ECS → UI tutorial events.
    /// Follows NotificationVisualQueue / AchievementVisualQueue pattern exactly.
    /// </summary>
    public static class TutorialVisualQueue
    {
        private static NativeQueue<TutorialVisualEvent> _queue;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _queue = new NativeQueue<TutorialVisualEvent>(Allocator.Persistent);
            _initialized = true;
        }

        public static void Dispose()
        {
            if (!_initialized) return;
            if (_queue.IsCreated) _queue.Dispose();
            _initialized = false;
        }

        public static void Enqueue(TutorialVisualEvent evt)
        {
            if (!_initialized) Initialize();
            _queue.Enqueue(evt);
        }

        public static bool TryDequeue(out TutorialVisualEvent evt)
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
