using System.Collections.Generic;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: UI event types for quest notifications.
    /// </summary>
    public enum QuestUIEventType : byte
    {
        QuestAccepted = 0,
        QuestCompleted = 1,
        QuestFailed = 2,
        QuestTurnedIn = 3,
        ObjectiveUpdated = 4
    }

    /// <summary>
    /// EPIC 16.12: Lightweight UI event data pushed to the static queue.
    /// </summary>
    public struct QuestUIEvent
    {
        public QuestUIEventType Type;
        public int QuestId;
        public int ObjectiveId;
        public int CurrentCount;
        public int RequiredCount;
    }

    /// <summary>
    /// EPIC 16.12: Static queue bridging ECS quest systems to managed UI.
    /// Follows DamageVisualQueue pattern.
    /// </summary>
    public static class QuestEventQueue
    {
        private static readonly Queue<QuestUIEvent> _queue = new(16);

        public static void Enqueue(QuestUIEvent evt) => _queue.Enqueue(evt);
        public static bool TryDequeue(out QuestUIEvent evt) => _queue.TryDequeue(out evt);
        public static int Count => _queue.Count;
        public static void Clear() => _queue.Clear();
    }
}
