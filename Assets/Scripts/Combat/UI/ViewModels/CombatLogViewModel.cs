using UnityEngine;
using System.Collections.Generic;
using DIG.UI.Core.MVVM;

namespace DIG.Combat.UI.ViewModels
{
    /// <summary>
    /// EPIC 15.9: ViewModel for combat log display.
    /// Implements ICombatLogProvider for combat UI integration.
    /// </summary>
    public class CombatLogViewModel : ViewModelBase, ICombatLogProvider
    {
        private readonly List<CombatLogEntry> _entries = new();
        private readonly BindableProperty<int> _entryCount = new(0);
        private readonly BindableProperty<bool> _isVisible = new(false);
        private readonly BindableProperty<bool> _isMinimized = new(true);
        
        private int _maxEntries = 100;
        
        // Filters
        private bool _showDamageDealt = true;
        private bool _showDamageTaken = true;
        private bool _showHeals = true;
        private bool _showKills = true;
        
        public BindableProperty<int> EntryCount => _entryCount;
        public BindableProperty<bool> IsVisible => _isVisible;
        public BindableProperty<bool> IsMinimized => _isMinimized;
        public IReadOnlyList<CombatLogEntry> Entries => _entries;
        
        /// <summary>
        /// Fired when a new entry is added.
        /// </summary>
        public event System.Action<CombatLogEntry> OnEntryAdded;
        
        /// <summary>
        /// Fired when log is cleared.
        /// </summary>
        public event System.Action OnLogCleared;
        
        public void LogCombatEvent(CombatLogEntry entry)
        {
            entry.Timestamp = Time.time;
            
            _entries.Add(entry);
            
            // Trim old entries
            while (_entries.Count > _maxEntries)
            {
                _entries.RemoveAt(0);
            }
            
            _entryCount.Value = _entries.Count;
            OnEntryAdded?.Invoke(entry);
        }
        
        public void ClearLog()
        {
            _entries.Clear();
            _entryCount.Value = 0;
            OnLogCleared?.Invoke();
        }
        
        /// <summary>
        /// Toggle visibility.
        /// </summary>
        public void ToggleVisibility()
        {
            _isVisible.Value = !_isVisible.Value;
        }
        
        /// <summary>
        /// Toggle minimized state.
        /// </summary>
        public void ToggleMinimized()
        {
            _isMinimized.Value = !_isMinimized.Value;
        }
        
        /// <summary>
        /// Set filter options.
        /// </summary>
        public void SetFilters(bool damageDealt, bool damageTaken, bool heals, bool kills)
        {
            _showDamageDealt = damageDealt;
            _showDamageTaken = damageTaken;
            _showHeals = heals;
            _showKills = kills;
        }
        
        /// <summary>
        /// Get filtered entries.
        /// </summary>
        public IEnumerable<CombatLogEntry> GetFilteredEntries()
        {
            foreach (var entry in _entries)
            {
                bool isPlayerDamage = entry.AttackerName == "Player";
                bool isPlayerTakingDamage = entry.TargetName == "Player";
                bool isHeal = entry.Damage < 0; // Negative damage = heal
                bool isKill = entry.TargetKilled;
                
                if (isKill && _showKills) yield return entry;
                else if (isHeal && _showHeals) yield return entry;
                else if (isPlayerDamage && _showDamageDealt) yield return entry;
                else if (isPlayerTakingDamage && _showDamageTaken) yield return entry;
            }
        }
        
        /// <summary>
        /// Format entry for display.
        /// </summary>
        public string FormatEntry(CombatLogEntry entry)
        {
            string timestamp = $"[{entry.Timestamp:F1}s]";
            string action;
            
            if (entry.TargetKilled)
            {
                action = $"{entry.AttackerName} killed {entry.TargetName}";
            }
            else if (entry.Damage < 0)
            {
                action = $"{entry.AttackerName} healed {entry.TargetName} for {Mathf.Abs(entry.Damage):F0}";
            }
            else
            {
                string hitTypeStr = entry.HitType == Targeting.Theming.HitType.Critical ? " (CRIT)" : "";
                action = $"{entry.AttackerName} hit {entry.TargetName} for {entry.Damage:F0}{hitTypeStr}";
            }
            
            return $"{timestamp} {action}";
        }
        
        /// <summary>
        /// Get color for entry.
        /// </summary>
        public Color GetEntryColor(CombatLogEntry entry, string playerName = "Player")
        {
            if (entry.TargetKilled)
                return new Color(1f, 0.8f, 0.2f); // Gold
            
            if (entry.Damage < 0) // Heal
                return Color.green;
            
            if (entry.TargetName == playerName)
                return new Color(1f, 0.4f, 0.4f); // Red - incoming damage
            
            if (entry.HitType == Targeting.Theming.HitType.Critical)
                return new Color(1f, 0.7f, 0.3f); // Orange - crit
            
            return Color.white; // Default outgoing damage
        }
    }
}
