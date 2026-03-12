using System.Collections.Generic;
using Unity.Entities;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Static queue bridge for PvP kill feed events.
    /// PvPScoringSystem enqueues kill events, PvPUIBridgeSystem dequeues for display.
    /// Same pattern as DamageVisualQueue / CinematicAnimEventQueue.
    /// </summary>
    public static class PvPKillFeedQueue
    {
        private static readonly Queue<PvPKillFeedEntry> _queue = new Queue<PvPKillFeedEntry>(32);

        public static int Count => _queue.Count;

        public static void Enqueue(PvPKillFeedEntry entry)
        {
            _queue.Enqueue(entry);

            // Prevent unbounded growth
            while (_queue.Count > 64)
                _queue.Dequeue();
        }

        public static bool TryDequeue(out PvPKillFeedEntry entry)
        {
            if (_queue.Count > 0)
            {
                entry = _queue.Dequeue();
                return true;
            }
            entry = default;
            return false;
        }

        public static void Clear()
        {
            _queue.Clear();
        }
    }

    /// <summary>
    /// Kill feed entry containing entity references for name resolution.
    /// </summary>
    public struct PvPKillFeedEntry
    {
        public Entity KillerEntity;
        public Entity VictimEntity;
        public byte KillerTeam;
        public byte VictimTeam;
    }
}
