// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · StatusEffectBarView
// UI View for displaying active status effects (buffs/debuffs)
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace DIG.Combat.UI.Views
{
    /// <summary>
    /// EPIC 15.9: View component for status effect bar.
    /// Displays active buffs and debuffs with duration timers and stacking indicators.
    /// </summary>
    public class StatusEffectBarView : DIG.UI.Core.MVVM.UIView<ViewModels.StatusEffectBarViewModel>
    {
        // ─────────────────────────────────────────────────────────────────
        // Configuration
        // ─────────────────────────────────────────────────────────────────
        [SerializeField] private bool _separateBuffsDebuffs = true;

        // ─────────────────────────────────────────────────────────────────
        // UXML References
        // ─────────────────────────────────────────────────────────────────
        private VisualElement _buffContainer;
        private VisualElement _debuffContainer;
        private Dictionary<int, VisualElement> _effectElements = new();
        
        private const string ExpiringClass = "status-expiring";
        private const string BuffClass = "status-buff";
        private const string DebuffClass = "status-debuff";
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        protected override void OnEnable()
        {
            base.OnEnable();
            
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;
            
            // Get or create buff container
            _buffContainer = root.Q<VisualElement>("buff-container");
            if (_buffContainer == null)
            {
                _buffContainer = new VisualElement { name = "buff-container" };
                _buffContainer.AddToClassList("status-container");
                _buffContainer.AddToClassList("buff-container");
                root.Add(_buffContainer);
            }
            
            // Get or create debuff container
            _debuffContainer = root.Q<VisualElement>("debuff-container");
            if (_debuffContainer == null)
            {
                _debuffContainer = new VisualElement { name = "debuff-container" };
                _debuffContainer.AddToClassList("status-container");
                _debuffContainer.AddToClassList("debuff-container");
                root.Add(_debuffContainer);
            }
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            _effectElements.Clear();
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Binding
        // ─────────────────────────────────────────────────────────────────
        protected override void OnBind()
        {
            if (ViewModel == null) return;
            
            ViewModel.OnEffectAdded += HandleEffectAdded;
            ViewModel.OnEffectRemoved += HandleEffectRemoved;
            ViewModel.OnEffectsChanged += RefreshAllEffects;
            
            // Initial population
            RefreshAllEffects();
        }
        
        protected override void OnUnbind()
        {
            if (ViewModel == null) return;
            
            ViewModel.OnEffectAdded -= HandleEffectAdded;
            ViewModel.OnEffectRemoved -= HandleEffectRemoved;
            ViewModel.OnEffectsChanged -= RefreshAllEffects;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Element Creation
        // ─────────────────────────────────────────────────────────────────
        private VisualElement CreateEffectElement(ActiveStatusEffect effect)
        {
            var container = new VisualElement();
            container.AddToClassList("status-effect");
            container.AddToClassList(effect.IsDebuff ? DebuffClass : BuffClass);
            
            // Icon
            var icon = new VisualElement { name = "icon" };
            icon.AddToClassList("status-icon");
            icon.AddToClassList($"status-{effect.Type.ToString().ToLowerInvariant()}");
            container.Add(icon);
            
            // Duration overlay
            var durationOverlay = new VisualElement { name = "duration-overlay" };
            durationOverlay.AddToClassList("status-duration-overlay");
            container.Add(durationOverlay);
            
            // Stack count (if applicable)
            if (effect.Stacks > 1)
            {
                var stackLabel = new Label { name = "stack-count", text = effect.Stacks.ToString() };
                stackLabel.AddToClassList("status-stack");
                container.Add(stackLabel);
            }
            
            // Duration timer text
            var timerLabel = new Label { name = "timer" };
            timerLabel.AddToClassList("status-timer");
            container.Add(timerLabel);
            
            // Tooltip on hover
            container.tooltip = effect.Type.ToString();
            
            return container;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────────────────────────
        private void HandleEffectAdded(ActiveStatusEffect effect)
        {
            int effectKey = (int)effect.Type;
            if (_effectElements.ContainsKey(effectKey)) return;
            
            var element = CreateEffectElement(effect);
            _effectElements[effectKey] = element;
            
            // Add to appropriate container
            var container = _separateBuffsDebuffs && effect.IsDebuff ? _debuffContainer : _buffContainer;
            container.Add(element);
            
            // Entrance animation
            element.AddToClassList("status-enter");
            element.schedule.Execute(() => element.RemoveFromClassList("status-enter")).StartingIn(200);
        }
        
        private void HandleEffectRemoved(StatusEffectType type)
        {
            int effectKey = (int)type;
            if (!_effectElements.TryGetValue(effectKey, out var element)) return;
            
            // Exit animation
            element.AddToClassList("status-exit");
            element.schedule.Execute(() =>
            {
                element.RemoveFromHierarchy();
                _effectElements.Remove(effectKey);
            }).StartingIn(200);
        }
        
        private void RefreshAllEffects()
        {
            // Clear existing
            foreach (var kvp in _effectElements)
            {
                kvp.Value.RemoveFromHierarchy();
            }
            _effectElements.Clear();
            
            // Re-add all
            if (ViewModel == null) return;
            
            foreach (var effect in ViewModel.ActiveEffects)
            {
                HandleEffectAdded(effect);
            }
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────
        private bool IsDebuff(StatusEffectType type)
        {
            return type switch
            {
                StatusEffectType.Burn => true,
                StatusEffectType.Freeze => true,
                StatusEffectType.Poison => true,
                StatusEffectType.Stun => true,
                StatusEffectType.Slow => true,
                StatusEffectType.Blind => true,
                StatusEffectType.Bleed => true,
                StatusEffectType.Weakness => true,
                StatusEffectType.Vulnerable => true,
                _ => false
            };
        }
    }
}
