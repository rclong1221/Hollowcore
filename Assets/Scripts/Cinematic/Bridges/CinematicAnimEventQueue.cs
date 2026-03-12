using Unity.Collections;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Static NativeQueue bridge for Timeline signal events.
    /// CinematicPlaybackSystem enqueues from SignalReceiver callbacks.
    /// NPC animation systems and VFX systems dequeue.
    /// Same pattern as DamageVisualQueue, LevelUpVisualQueue.
    /// </summary>
    public static class CinematicAnimEventQueue
    {
        private static NativeQueue<CinematicAnimEvent> _queue;

        public static bool IsCreated => _queue.IsCreated;
        public static int Count => _queue.IsCreated ? _queue.Count : 0;

        public static void Initialize()
        {
            if (_queue.IsCreated) return;
            _queue = new NativeQueue<CinematicAnimEvent>(Allocator.Persistent);
        }

        public static void Dispose()
        {
            if (_queue.IsCreated)
                _queue.Dispose();
        }

        public static void Enqueue(CinematicAnimEvent evt)
        {
            if (_queue.IsCreated)
                _queue.Enqueue(evt);
        }

        public static bool TryDequeue(out CinematicAnimEvent evt)
        {
            if (_queue.IsCreated)
                return _queue.TryDequeue(out evt);
            evt = default;
            return false;
        }

        public static void Clear()
        {
            if (_queue.IsCreated)
                _queue.Clear();
        }
    }
}
