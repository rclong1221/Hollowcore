using Unity.Collections;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Visual event data for achievement unlock toast.
    /// </summary>
    public struct AchievementUnlockVisualEvent
    {
        public ushort AchievementId;
        public AchievementTier Tier;
        public FixedString64Bytes AchievementName;
        public FixedString128Bytes Description;
        public AchievementRewardType RewardType;
        public int RewardAmount;
    }

    /// <summary>
    /// EPIC 17.7: Static NativeQueue bridge for ECS → UI achievement unlock notifications.
    /// Follows DamageVisualQueue / LevelUpVisualQueue pattern.
    /// </summary>
    public static class AchievementVisualQueue
    {
        private static NativeQueue<AchievementUnlockVisualEvent> _queue;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _queue = new NativeQueue<AchievementUnlockVisualEvent>(Allocator.Persistent);
            _initialized = true;
        }

        public static void Dispose()
        {
            if (!_initialized) return;
            if (_queue.IsCreated) _queue.Dispose();
            _initialized = false;
        }

        public static void Enqueue(AchievementUnlockVisualEvent evt)
        {
            if (!_initialized) Initialize();
            _queue.Enqueue(evt);
        }

        public static bool TryDequeue(out AchievementUnlockVisualEvent evt)
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
