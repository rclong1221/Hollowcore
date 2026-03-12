using Unity.Collections;
using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Static NativeQueue bridge for ECS -> UI party events.
    /// Follows DamageVisualQueue / LevelUpVisualQueue pattern.
    /// </summary>
    public static class PartyVisualQueue
    {
        public struct PartyVisualEvent
        {
            public PartyNotifyType Type;
            public Unity.Entities.Entity SourcePlayer;
            public byte Payload;
        }

        private static NativeQueue<PartyVisualEvent> _queue;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _queue = new NativeQueue<PartyVisualEvent>(Allocator.Persistent);
            _initialized = true;
        }

        public static void Dispose()
        {
            if (!_initialized) return;
            if (_queue.IsCreated) _queue.Dispose();
            _initialized = false;
        }

        public static void Enqueue(PartyVisualEvent evt)
        {
            if (!_initialized) Initialize();
            _queue.Enqueue(evt);
        }

        public static bool TryDequeue(out PartyVisualEvent evt)
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
