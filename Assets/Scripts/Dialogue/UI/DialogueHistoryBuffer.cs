using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 18.5: Fixed-size ring buffer storing recent dialogue lines for player review.
    /// Zero GC after initial allocation.
    /// </summary>
    public class DialogueHistoryBuffer
    {
        public struct HistoryEntry
        {
            public string SpeakerName;
            public string Text;
            public float Timestamp;
            public AudioClip VoiceClip;
        }

        private readonly HistoryEntry[] _entries;
        private int _head;
        private int _count;

        public int Count => _count;
        public int Capacity => _entries.Length;

        public DialogueHistoryBuffer(int capacity = 50)
        {
            _entries = new HistoryEntry[Mathf.Max(capacity, 1)];
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Add a new dialogue line to history.
        /// </summary>
        public void Push(string speakerName, string text, AudioClip voiceClip = null)
        {
            _entries[_head] = new HistoryEntry
            {
                SpeakerName = speakerName,
                Text = text,
                Timestamp = Time.time,
                VoiceClip = voiceClip
            };

            _head = (_head + 1) % _entries.Length;
            if (_count < _entries.Length)
                _count++;
        }

        /// <summary>
        /// Get entry by index (0 = oldest visible entry, Count-1 = most recent).
        /// </summary>
        public HistoryEntry Get(int index)
        {
            if (index < 0 || index >= _count)
                return default;

            // Ring buffer: oldest entry is at (_head - _count + Length) % Length
            int start = (_head - _count + _entries.Length) % _entries.Length;
            int actual = (start + index) % _entries.Length;
            return _entries[actual];
        }

        /// <summary>
        /// Clear all history entries.
        /// </summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
