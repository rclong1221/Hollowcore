using UnityEngine;
using DIG.UI.Core.MVVM;

namespace DIG.Combat.UI.ViewModels
{
    /// <summary>
    /// EPIC 15.9: ViewModel for boss health bar display.
    /// Supports phase tracking and status effects.
    /// </summary>
    public class BossHealthBarViewModel : ViewModelBase
    {
        private readonly BindableProperty<string> _bossName = new("");
        private readonly BindableProperty<float> _currentHealth = new(0f);
        private readonly BindableProperty<float> _maxHealth = new(100f);
        private readonly BindableProperty<float> _healthPercent = new(1f);
        private readonly BindableProperty<int> _currentPhase = new(1);
        private readonly BindableProperty<int> _totalPhases = new(1);
        private readonly BindableProperty<bool> _isActive = new(false);
        private readonly BindableProperty<bool> _isEnraged = new(false);
        private readonly BindableProperty<float> _shieldPercent = new(0f);
        
        // Phase thresholds (e.g., 0.75, 0.5, 0.25 for 4 phases)
        private float[] _phaseThresholds = new float[0];
        
        public BindableProperty<string> BossName => _bossName;
        public BindableProperty<float> CurrentHealth => _currentHealth;
        public BindableProperty<float> MaxHealth => _maxHealth;
        public BindableProperty<float> HealthPercent => _healthPercent;
        public BindableProperty<int> CurrentPhase => _currentPhase;
        public BindableProperty<int> TotalPhases => _totalPhases;
        public BindableProperty<bool> IsActive => _isActive;
        public BindableProperty<bool> IsEnraged => _isEnraged;
        public BindableProperty<float> ShieldPercent => _shieldPercent;
        
        /// <summary>
        /// Event fired when boss enters a new phase.
        /// </summary>
        public event System.Action<int, int> OnPhaseTransition;
        
        /// <summary>
        /// Event fired when boss fight starts.
        /// </summary>
        public event System.Action OnBossFightStarted;
        
        /// <summary>
        /// Event fired when boss is defeated.
        /// </summary>
        public event System.Action OnBossDefeated;
        
        /// <summary>
        /// Initialize boss fight display.
        /// </summary>
        public void StartBossFight(string bossName, float maxHealth, int totalPhases = 1)
        {
            _bossName.Value = bossName;
            _maxHealth.Value = maxHealth;
            _currentHealth.Value = maxHealth;
            _healthPercent.Value = 1f;
            _totalPhases.Value = totalPhases;
            _currentPhase.Value = 1;
            _isEnraged.Value = false;
            _isActive.Value = true;
            
            // Calculate phase thresholds
            if (totalPhases > 1)
            {
                _phaseThresholds = new float[totalPhases - 1];
                for (int i = 0; i < totalPhases - 1; i++)
                {
                    _phaseThresholds[i] = 1f - ((i + 1) / (float)totalPhases);
                }
            }
            else
            {
                _phaseThresholds = new float[0];
            }
            
            OnBossFightStarted?.Invoke();
        }
        
        /// <summary>
        /// Update boss health.
        /// </summary>
        public void UpdateHealth(float current, float max)
        {
            float previousPercent = _healthPercent.Value;
            
            _currentHealth.Value = current;
            _maxHealth.Value = max;
            _healthPercent.Value = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            
            // Check for phase transition
            CheckPhaseTransition(previousPercent, _healthPercent.Value);
            
            // Check for defeat
            if (current <= 0 && _isActive.Value)
            {
                OnBossDefeated?.Invoke();
            }
        }
        
        /// <summary>
        /// Set enrage state.
        /// </summary>
        public void SetEnraged(bool enraged)
        {
            _isEnraged.Value = enraged;
        }
        
        /// <summary>
        /// Hide boss health bar (boss defeated or despawned).
        /// </summary>
        public void EndBossFight()
        {
            _isActive.Value = false;
        }
        
        /// <summary>
        /// Force set current phase.
        /// </summary>
        public void SetPhase(int phase)
        {
            if (phase != _currentPhase.Value)
            {
                int oldPhase = _currentPhase.Value;
                _currentPhase.Value = phase;
                OnPhaseTransition?.Invoke(oldPhase, phase);
            }
        }
        
        private void CheckPhaseTransition(float previousPercent, float currentPercent)
        {
            int previousPhase = CalculatePhase(previousPercent);
            int currentPhase = CalculatePhase(currentPercent);
            
            if (currentPhase != previousPhase && currentPhase > previousPhase)
            {
                _currentPhase.Value = currentPhase;
                OnPhaseTransition?.Invoke(previousPhase, currentPhase);
            }
        }
        
        private int CalculatePhase(float healthPercent)
        {
            if (_phaseThresholds.Length == 0)
                return 1;
            
            for (int i = 0; i < _phaseThresholds.Length; i++)
            {
                if (healthPercent > _phaseThresholds[i])
                    return i + 1;
            }
            
            return _totalPhases.Value;
        }
        
        /// <summary>
        /// Get phase thresholds for UI phase markers.
        /// </summary>
        public float[] GetPhaseThresholds() => _phaseThresholds;
    }
}
