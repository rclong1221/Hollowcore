using UnityEngine;
using DIG.UI.Core.MVVM;

namespace DIG.Combat.UI.ViewModels
{
    /// <summary>
    /// EPIC 15.9: ViewModel for combo counter display.
    /// Tracks hit combo with multiplier and decay timer.
    /// </summary>
    public class ComboCounterViewModel : ViewModelBase
    {
        private readonly BindableProperty<int> _currentCombo = new(0);
        private readonly BindableProperty<int> _maxCombo = new(0);
        private readonly BindableProperty<float> _comboTimer = new(0f);
        private readonly BindableProperty<float> _comboTimerMax = new(0f);
        private readonly BindableProperty<float> _comboMultiplier = new(1f);
        private readonly BindableProperty<bool> _isVisible = new(false);
        private readonly BindableProperty<bool> _isNewHighScore = new(false);
        
        public BindableProperty<int> CurrentCombo => _currentCombo;
        public BindableProperty<int> MaxCombo => _maxCombo;
        public BindableProperty<float> ComboTimer => _comboTimer;
        public BindableProperty<float> ComboTimerMax => _comboTimerMax;
        public BindableProperty<float> ComboMultiplier => _comboMultiplier;
        public BindableProperty<bool> IsVisible => _isVisible;
        public BindableProperty<bool> IsNewHighScore => _isNewHighScore;
        
        /// <summary>
        /// Fired when combo reaches a milestone (5, 10, 25, 50, 100).
        /// </summary>
        public event System.Action<int> OnComboMilestone;
        
        /// <summary>
        /// Fired when combo ends (timer expired or break).
        /// </summary>
        public event System.Action<int> OnComboEnded;
        
        /// <summary>
        /// Fired when a hit is registered.
        /// </summary>
        public event System.Action OnHitRegistered;
        
        private float _comboDecayTime = 3f; // Time before combo resets
        private bool _comboActive;
        
        /// <summary>
        /// Register a combo hit.
        /// </summary>
        public void RegisterHit(float comboWindow = 3f)
        {
            _comboDecayTime = comboWindow;
            _currentCombo.Value++;
            _comboTimer.Value = comboWindow;
            _comboTimerMax.Value = comboWindow;
            _comboActive = true;
            _isVisible.Value = true;
            
            // Update max combo
            if (_currentCombo.Value > _maxCombo.Value)
            {
                _maxCombo.Value = _currentCombo.Value;
                _isNewHighScore.Value = true;
            }
            
            // Update multiplier (example: 1.0 + 0.1 per 5 hits, max 2.0)
            _comboMultiplier.Value = Mathf.Min(2f, 1f + (_currentCombo.Value / 5) * 0.1f);
            
            // Check milestones
            CheckMilestone(_currentCombo.Value);
            
            OnHitRegistered?.Invoke();
        }
        
        /// <summary>
        /// Update combo timer. Call from View's Update.
        /// </summary>
        public void UpdateTimer(float deltaTime)
        {
            if (!_comboActive) return;
            
            _comboTimer.Value -= deltaTime;
            
            if (_comboTimer.Value <= 0)
            {
                EndCombo();
            }
        }
        
        /// <summary>
        /// Force end the combo (took damage, etc).
        /// </summary>
        public void BreakCombo()
        {
            if (_comboActive)
            {
                EndCombo();
            }
        }
        
        /// <summary>
        /// Reset combo for new session.
        /// </summary>
        public void Reset()
        {
            _currentCombo.Value = 0;
            _maxCombo.Value = 0;
            _comboTimer.Value = 0;
            _comboMultiplier.Value = 1f;
            _isVisible.Value = false;
            _isNewHighScore.Value = false;
            _comboActive = false;
        }
        
        /// <summary>
        /// Set combo values directly (for ECS sync).
        /// </summary>
        public void SetComboState(int count, float timer, float maxTimer)
        {
            bool wasInactive = !_comboActive;
            
            _currentCombo.Value = count;
            _comboTimer.Value = timer;
            _comboTimerMax.Value = maxTimer;
            _comboActive = count > 0 && timer > 0;
            _isVisible.Value = count > 0;
            
            if (_currentCombo.Value > _maxCombo.Value)
            {
                _maxCombo.Value = _currentCombo.Value;
            }
            
            _comboMultiplier.Value = Mathf.Min(2f, 1f + (count / 5) * 0.1f);
            
            // Check for combo end
            if (wasInactive && !_comboActive && count == 0)
            {
                // Combo just ended
            }
        }
        
        private void EndCombo()
        {
            int finalCombo = _currentCombo.Value;
            
            _comboActive = false;
            _currentCombo.Value = 0;
            _comboTimer.Value = 0;
            _comboMultiplier.Value = 1f;
            _isNewHighScore.Value = false;
            
            // Delay hiding for ending animation
            // View should handle hiding after animation
            
            OnComboEnded?.Invoke(finalCombo);
        }
        
        private void CheckMilestone(int combo)
        {
            // Milestones at 5, 10, 25, 50, 100, 200, 500, 1000...
            int[] milestones = { 5, 10, 25, 50, 100, 200, 500, 1000 };
            
            foreach (int milestone in milestones)
            {
                if (combo == milestone)
                {
                    OnComboMilestone?.Invoke(milestone);
                    break;
                }
            }
        }
        
        /// <summary>
        /// Get timer as normalized 0-1 value.
        /// </summary>
        public float GetTimerNormalized()
        {
            if (_comboTimerMax.Value <= 0) return 0f;
            return Mathf.Clamp01(_comboTimer.Value / _comboTimerMax.Value);
        }
    }
}
