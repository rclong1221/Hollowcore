using UnityEngine;
using System.Collections.Generic;
using DIG.UI.Core.MVVM;

namespace DIG.Combat.UI.ViewModels
{
    /// <summary>
    /// EPIC 15.9: ViewModel for status effect icons display.
    /// Tracks active buffs and debuffs on player.
    /// </summary>
    public class StatusEffectBarViewModel : ViewModelBase
    {
        private readonly List<ActiveStatusEffect> _activeEffects = new();
        private readonly BindableProperty<int> _buffCount = new(0);
        private readonly BindableProperty<int> _debuffCount = new(0);
        private readonly BindableProperty<int> _totalCount = new(0);
        
        public BindableProperty<int> BuffCount => _buffCount;
        public BindableProperty<int> DebuffCount => _debuffCount;
        public BindableProperty<int> TotalCount => _totalCount;
        public IReadOnlyList<ActiveStatusEffect> ActiveEffects => _activeEffects;
        
        /// <summary>
        /// Fired when a status effect is added.
        /// </summary>
        public event System.Action<ActiveStatusEffect> OnEffectAdded;
        
        /// <summary>
        /// Fired when a status effect is removed.
        /// </summary>
        public event System.Action<StatusEffectType> OnEffectRemoved;
        
        /// <summary>
        /// Fired when effects list changes.
        /// </summary>
        public event System.Action OnEffectsChanged;
        
        /// <summary>
        /// Add or update a status effect.
        /// </summary>
        public void AddOrUpdateEffect(StatusEffectType type, float duration, int stacks = 1)
        {
            bool isDebuff = IsDebuff(type);
            
            // Check if already exists
            int existingIndex = _activeEffects.FindIndex(e => e.Type == type);
            
            if (existingIndex >= 0)
            {
                // Update existing
                var effect = _activeEffects[existingIndex];
                effect.RemainingDuration = duration;
                effect.TotalDuration = duration;
                effect.Stacks = stacks;
                _activeEffects[existingIndex] = effect;
            }
            else
            {
                // Add new
                var effect = new ActiveStatusEffect
                {
                    Type = type,
                    RemainingDuration = duration,
                    TotalDuration = duration,
                    Stacks = stacks,
                    IsDebuff = isDebuff
                };
                _activeEffects.Add(effect);
                OnEffectAdded?.Invoke(effect);
            }
            
            UpdateCounts();
            OnEffectsChanged?.Invoke();
        }
        
        /// <summary>
        /// Remove a status effect.
        /// </summary>
        public void RemoveEffect(StatusEffectType type)
        {
            int index = _activeEffects.FindIndex(e => e.Type == type);
            if (index >= 0)
            {
                _activeEffects.RemoveAt(index);
                UpdateCounts();
                OnEffectRemoved?.Invoke(type);
                OnEffectsChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Update effect durations. Call from View's Update.
        /// </summary>
        public void UpdateDurations(float deltaTime)
        {
            bool anyExpired = false;
            
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                effect.RemainingDuration -= deltaTime;
                
                if (effect.RemainingDuration <= 0)
                {
                    OnEffectRemoved?.Invoke(effect.Type);
                    _activeEffects.RemoveAt(i);
                    anyExpired = true;
                }
                else
                {
                    _activeEffects[i] = effect;
                }
            }
            
            if (anyExpired)
            {
                UpdateCounts();
                OnEffectsChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Set all effects at once (for ECS sync).
        /// </summary>
        public void SetEffects(List<ActiveStatusEffect> effects)
        {
            _activeEffects.Clear();
            _activeEffects.AddRange(effects);
            UpdateCounts();
            OnEffectsChanged?.Invoke();
        }
        
        /// <summary>
        /// Clear all effects.
        /// </summary>
        public void ClearAll()
        {
            _activeEffects.Clear();
            _buffCount.Value = 0;
            _debuffCount.Value = 0;
            _totalCount.Value = 0;
            OnEffectsChanged?.Invoke();
        }
        
        /// <summary>
        /// Check if player has a specific effect.
        /// </summary>
        public bool HasEffect(StatusEffectType type)
        {
            return _activeEffects.Exists(e => e.Type == type);
        }
        
        /// <summary>
        /// Get remaining duration for an effect.
        /// </summary>
        public float GetRemainingDuration(StatusEffectType type)
        {
            var effect = _activeEffects.Find(e => e.Type == type);
            return effect.Type != StatusEffectType.None ? effect.RemainingDuration : 0f;
        }
        
        /// <summary>
        /// Get normalized duration (0-1) for an effect.
        /// </summary>
        public float GetDurationNormalized(StatusEffectType type)
        {
            var effect = _activeEffects.Find(e => e.Type == type);
            if (effect.Type == StatusEffectType.None || effect.TotalDuration <= 0)
                return 0f;
            return Mathf.Clamp01(effect.RemainingDuration / effect.TotalDuration);
        }
        
        private void UpdateCounts()
        {
            int buffs = 0;
            int debuffs = 0;
            
            foreach (var effect in _activeEffects)
            {
                if (effect.IsDebuff)
                    debuffs++;
                else
                    buffs++;
            }
            
            _buffCount.Value = buffs;
            _debuffCount.Value = debuffs;
            _totalCount.Value = _activeEffects.Count;
        }
        
        private bool IsDebuff(StatusEffectType type)
        {
            return type switch
            {
                // DOT
                StatusEffectType.Burn => true,
                StatusEffectType.Bleed => true,
                StatusEffectType.Poison => true,
                StatusEffectType.Frostbite => true,
                
                // CC
                StatusEffectType.Stun => true,
                StatusEffectType.Freeze => true,
                StatusEffectType.Slow => true,
                StatusEffectType.Root => true,
                StatusEffectType.Silence => true,
                StatusEffectType.Blind => true,
                
                // Debuffs
                StatusEffectType.Weakness => true,
                StatusEffectType.Vulnerable => true,
                StatusEffectType.Exposed => true,
                StatusEffectType.Marked => true,
                StatusEffectType.Fear => true,
                
                // Buffs
                _ => false
            };
        }
    }
}
