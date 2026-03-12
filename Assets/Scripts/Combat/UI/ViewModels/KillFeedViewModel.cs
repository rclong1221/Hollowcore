using UnityEngine;
using System.Collections.Generic;
using DIG.UI.Core.MVVM;

namespace DIG.Combat.UI.ViewModels
{
    /// <summary>
    /// EPIC 15.9: ViewModel for kill feed display.
    /// Manages a scrolling list of recent kills.
    /// </summary>
    public class KillFeedViewModel : ViewModelBase, IKillFeedProvider
    {
        private readonly List<KillFeedEntry> _entries = new();
        private readonly BindableProperty<int> _entryCount = new(0);
        
        [SerializeField] private int maxVisibleEntries = 5;
        [SerializeField] private float entryLifetime = 5f;
        
        // Track entry timestamps for expiration
        private readonly List<float> _entryTimestamps = new();
        
        public BindableProperty<int> EntryCount => _entryCount;
        public IReadOnlyList<KillFeedEntry> Entries => _entries;
        public int MaxVisible => maxVisibleEntries;
        
        /// <summary>
        /// Event fired when a new entry is added.
        /// </summary>
        public event System.Action<KillFeedEntry> OnEntryAdded;
        
        /// <summary>
        /// Event fired when entries are removed (expired or cleared).
        /// </summary>
        public event System.Action OnEntriesChanged;
        
        public KillFeedViewModel()
        {
            maxVisibleEntries = 5;
            entryLifetime = 5f;
        }
        
        public KillFeedViewModel(int maxEntries, float lifetime)
        {
            maxVisibleEntries = maxEntries;
            entryLifetime = lifetime;
        }
        
        public void AddKill(KillFeedEntry entry)
        {
            entry.Timestamp = Time.time;
            
            _entries.Insert(0, entry); // Newest first
            _entryTimestamps.Insert(0, Time.time);
            
            // Trim excess entries
            while (_entries.Count > maxVisibleEntries)
            {
                _entries.RemoveAt(_entries.Count - 1);
                _entryTimestamps.RemoveAt(_entryTimestamps.Count - 1);
            }
            
            _entryCount.Value = _entries.Count;
            OnEntryAdded?.Invoke(entry);
        }
        
        public void Clear()
        {
            _entries.Clear();
            _entryTimestamps.Clear();
            _entryCount.Value = 0;
            OnEntriesChanged?.Invoke();
        }
        
        /// <summary>
        /// Update entry expiration. Call from View's Update.
        /// </summary>
        public void UpdateExpiration()
        {
            float currentTime = Time.time;
            bool anyExpired = false;
            
            for (int i = _entryTimestamps.Count - 1; i >= 0; i--)
            {
                if (currentTime - _entryTimestamps[i] > entryLifetime)
                {
                    _entries.RemoveAt(i);
                    _entryTimestamps.RemoveAt(i);
                    anyExpired = true;
                }
            }
            
            if (anyExpired)
            {
                _entryCount.Value = _entries.Count;
                OnEntriesChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Get time remaining before entry expires (for fade animation).
        /// </summary>
        public float GetTimeRemaining(int index)
        {
            if (index < 0 || index >= _entryTimestamps.Count)
                return 0f;
            
            float elapsed = Time.time - _entryTimestamps[index];
            return Mathf.Max(0f, entryLifetime - elapsed);
        }
        
        /// <summary>
        /// Get fade alpha for entry (1 = full, 0 = expired).
        /// </summary>
        public float GetFadeAlpha(int index)
        {
            float remaining = GetTimeRemaining(index);
            float fadeStart = entryLifetime * 0.7f; // Start fading at 70% of lifetime
            
            if (remaining > fadeStart)
                return 1f;
            
            return remaining / fadeStart;
        }
    }
}
