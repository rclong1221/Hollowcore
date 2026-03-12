using Unity.Collections;
using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: XP gain event data for UI display.
    /// </summary>
    public struct XPGainEvent
    {
        public int Amount;
        public XPSourceType Source;
    }

    /// <summary>
    /// EPIC 16.14: Level-up visual event data for UI display.
    /// </summary>
    public struct LevelUpVisualEvent
    {
        public int NewLevel;
        public int PreviousLevel;
        public int StatPointsAwarded;
    }

    /// <summary>
    /// EPIC 16.14: Static NativeQueue bridge for ECS → UI visual events.
    /// Follows DamageVisualQueue pattern. Thread-safe for single producer/consumer.
    /// </summary>
    public static class LevelUpVisualQueue
    {
        private static NativeQueue<XPGainEvent> _xpQueue;
        private static NativeQueue<LevelUpVisualEvent> _levelUpQueue;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _xpQueue = new NativeQueue<XPGainEvent>(Allocator.Persistent);
            _levelUpQueue = new NativeQueue<LevelUpVisualEvent>(Allocator.Persistent);
            _initialized = true;
        }

        public static void Dispose()
        {
            if (!_initialized) return;
            if (_xpQueue.IsCreated) _xpQueue.Dispose();
            if (_levelUpQueue.IsCreated) _levelUpQueue.Dispose();
            _initialized = false;
        }

        public static void EnqueueXPGain(int amount, XPSourceType source)
        {
            if (!_initialized) Initialize();
            _xpQueue.Enqueue(new XPGainEvent { Amount = amount, Source = source });
        }

        public static void EnqueueLevelUp(int newLevel, int previousLevel, int statPoints)
        {
            if (!_initialized) Initialize();
            _levelUpQueue.Enqueue(new LevelUpVisualEvent
            {
                NewLevel = newLevel,
                PreviousLevel = previousLevel,
                StatPointsAwarded = statPoints
            });
        }

        public static bool TryDequeueXP(out XPGainEvent evt)
        {
            if (!_initialized) { evt = default; return false; }
            return _xpQueue.TryDequeue(out evt);
        }

        public static bool TryDequeueLevelUp(out LevelUpVisualEvent evt)
        {
            if (!_initialized) { evt = default; return false; }
            return _levelUpQueue.TryDequeue(out evt);
        }

        public static int XPCount => _initialized && _xpQueue.IsCreated ? _xpQueue.Count : 0;
        public static int LevelUpCount => _initialized && _levelUpQueue.IsCreated ? _levelUpQueue.Count : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_initialized)
            {
                if (_xpQueue.IsCreated) _xpQueue.Dispose();
                if (_levelUpQueue.IsCreated) _levelUpQueue.Dispose();
                _initialized = false;
            }
        }
    }
}
