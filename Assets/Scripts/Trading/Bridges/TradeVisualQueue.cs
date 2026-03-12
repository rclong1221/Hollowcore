using Unity.Collections;
using UnityEngine;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Static NativeQueue bridge for ECS -> UI trade events.
    /// Follows PartyVisualQueue / DamageVisualQueue pattern.
    /// </summary>
    public static class TradeVisualQueue
    {
        public struct TradeVisualEvent
        {
            public TradeVisualEventType Type;
            public byte Payload;
        }

        private static NativeQueue<TradeVisualEvent> _queue;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _queue = new NativeQueue<TradeVisualEvent>(Allocator.Persistent);
            _initialized = true;
        }

        public static void Dispose()
        {
            if (!_initialized) return;
            if (_queue.IsCreated) _queue.Dispose();
            _initialized = false;
        }

        public static void Enqueue(TradeVisualEvent evt)
        {
            if (!_initialized) Initialize();
            _queue.Enqueue(evt);
        }

        public static bool TryDequeue(out TradeVisualEvent evt)
        {
            if (!_initialized) { evt = default; return false; }
            return _queue.TryDequeue(out evt);
        }

        public static int Count => _initialized && _queue.IsCreated ? _queue.Count : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
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
