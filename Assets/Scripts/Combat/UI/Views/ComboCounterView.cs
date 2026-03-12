// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · ComboCounterView
// UI View for displaying combo counter with multiplier
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Combat.UI.Views
{
    /// <summary>
    /// EPIC 15.9: View component for combo counter display.
    /// Shows current combo count, multiplier, and animates on milestone achievements.
    /// </summary>
    public class ComboCounterView : DIG.UI.Core.MVVM.UIView<ViewModels.ComboCounterViewModel>
    {
        // ─────────────────────────────────────────────────────────────────
        // UXML References
        // ─────────────────────────────────────────────────────────────────
        private VisualElement _container;
        private Label _comboLabel;
        private Label _multiplierLabel;
        private VisualElement _timerBar;
        private Label _milestoneLabel;
        
        private const string HiddenClass = "combo-hidden";
        private const string ActiveClass = "combo-active";
        private const string MilestoneClass = "combo-milestone";
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        protected override void OnEnable()
        {
            base.OnEnable();
            
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;
            
            _container = root.Q<VisualElement>("combo-container");
            if (_container == null)
            {
                _container = CreateDefaultLayout();
                root.Add(_container);
            }
            else
            {
                _comboLabel = _container.Q<Label>("combo-count");
                _multiplierLabel = _container.Q<Label>("combo-multiplier");
                _timerBar = _container.Q<VisualElement>("combo-timer");
                _milestoneLabel = _container.Q<Label>("combo-milestone");
            }
            
            _container.AddToClassList(HiddenClass);
        }
        
        private VisualElement CreateDefaultLayout()
        {
            var container = new VisualElement { name = "combo-container" };
            container.AddToClassList("combo-container");
            
            _comboLabel = new Label { name = "combo-count", text = "0" };
            _comboLabel.AddToClassList("combo-count");
            container.Add(_comboLabel);
            
            _multiplierLabel = new Label { name = "combo-multiplier", text = "x1.0" };
            _multiplierLabel.AddToClassList("combo-multiplier");
            container.Add(_multiplierLabel);
            
            _timerBar = new VisualElement { name = "combo-timer" };
            _timerBar.AddToClassList("combo-timer");
            container.Add(_timerBar);
            
            _milestoneLabel = new Label { name = "combo-milestone" };
            _milestoneLabel.AddToClassList("combo-milestone-text");
            _milestoneLabel.style.display = DisplayStyle.None;
            container.Add(_milestoneLabel);
            
            return container;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Binding
        // ─────────────────────────────────────────────────────────────────
        protected override void OnBind()
        {
            if (ViewModel == null) return;
            
            ViewModel.CurrentCombo.OnChanged += UpdateComboDisplay;
            ViewModel.ComboMultiplier.OnChanged += UpdateMultiplierDisplay;
            ViewModel.ComboTimer.OnChanged += UpdateTimerDisplay;
            ViewModel.IsVisible.OnChanged += UpdateVisibility;
            ViewModel.OnComboMilestone += HandleMilestone;
        }
        
        protected override void OnUnbind()
        {
            if (ViewModel == null) return;
            
            ViewModel.CurrentCombo.OnChanged -= UpdateComboDisplay;
            ViewModel.ComboMultiplier.OnChanged -= UpdateMultiplierDisplay;
            ViewModel.ComboTimer.OnChanged -= UpdateTimerDisplay;
            ViewModel.IsVisible.OnChanged -= UpdateVisibility;
            ViewModel.OnComboMilestone -= HandleMilestone;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Update Methods
        // ─────────────────────────────────────────────────────────────────
        private void UpdateComboDisplay(int count)
        {
            if (_comboLabel == null) return;
            
            _comboLabel.text = count.ToString();
            
            // Pulse animation on increase
            _comboLabel.AddToClassList("combo-pulse");
            _comboLabel.schedule.Execute(() => _comboLabel.RemoveFromClassList("combo-pulse")).StartingIn(150);
        }
        
        private void UpdateMultiplierDisplay(float multiplier)
        {
            if (_multiplierLabel == null) return;
            _multiplierLabel.text = $"x{multiplier:F1}";
        }
        
        private void UpdateTimerDisplay(float timer)
        {
            if (_timerBar == null) return;
            
            // Timer is normalized 0-1 (1 = full, 0 = expired)
            float percent = Mathf.Clamp01(timer / 3f); // Assuming 3 second combo window
            _timerBar.style.width = new Length(percent * 100f, LengthUnit.Percent);
            
            // Change color based on time remaining
            if (percent < 0.25f)
            {
                _timerBar.AddToClassList("combo-timer-critical");
            }
            else
            {
                _timerBar.RemoveFromClassList("combo-timer-critical");
            }
        }
        
        private void UpdateVisibility(bool active)
        {
            if (_container == null) return;
            
            if (active)
            {
                _container.RemoveFromClassList(HiddenClass);
                _container.AddToClassList(ActiveClass);
            }
            else
            {
                _container.AddToClassList(HiddenClass);
                _container.RemoveFromClassList(ActiveClass);
            }
        }
        
        private void HandleMilestone(int milestone)
        {
            if (_milestoneLabel == null || _container == null) return;
            
            // Show milestone text
            string milestoneText = milestone switch
            {
                >= 100 => "LEGENDARY!",
                >= 50 => "INSANE!",
                >= 25 => "AWESOME!",
                >= 10 => "GREAT!",
                _ => "NICE!"
            };
            
            _milestoneLabel.text = milestoneText;
            _milestoneLabel.style.display = DisplayStyle.Flex;
            
            // Animate container
            _container.AddToClassList(MilestoneClass);
            
            // Hide after delay
            _milestoneLabel.schedule.Execute(() =>
            {
                _milestoneLabel.style.display = DisplayStyle.None;
                _container.RemoveFromClassList(MilestoneClass);
            }).StartingIn(1000);
        }
    }
}
