// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · BossHealthBarView
// UI View for boss health bar with phase indicators
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace DIG.Combat.UI.Views
{
    /// <summary>
    /// EPIC 15.9: View component for boss health bar display.
    /// Full-width bar with phase indicators, enrage state, and boss name.
    /// </summary>
    public class BossHealthBarView : DIG.UI.Core.MVVM.UIView<ViewModels.BossHealthBarViewModel>
    {
        // ─────────────────────────────────────────────────────────────────
        // UXML References
        // ─────────────────────────────────────────────────────────────────
        private VisualElement _container;
        private Label _bossNameLabel;
        private Label _phaseLabel;
        private VisualElement _healthFill;
        private VisualElement _healthTrail;
        private VisualElement _shieldFill;
        private VisualElement _phaseMarkerContainer;
        private VisualElement _enrageOverlay;
        private Label _percentLabel;
        
        private List<VisualElement> _phaseMarkers = new();
        
        private const string HiddenClass = "boss-bar-hidden";
        private const string EnragedClass = "boss-bar-enraged";
        private const string PhaseTransitionClass = "boss-bar-phase-transition";
        
        // ─────────────────────────────────────────────────────────────────
        // Animation State
        // ─────────────────────────────────────────────────────────────────
        private float _currentDisplayHealth;
        private float _trailHealth;
        private float _targetHealth;
        private const float HealthLerpSpeed = 5f;
        private const float TrailLerpSpeed = 2f;
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        protected override void OnEnable()
        {
            base.OnEnable();
            
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;
            
            _container = root.Q<VisualElement>("boss-health-container");
            if (_container == null)
            {
                _container = CreateDefaultLayout();
                root.Add(_container);
            }
            else
            {
                BindExistingElements();
            }
            
            _container.AddToClassList(HiddenClass);
        }
        
        private VisualElement CreateDefaultLayout()
        {
            var container = new VisualElement { name = "boss-health-container" };
            container.AddToClassList("boss-health-container");
            
            // Boss name
            _bossNameLabel = new Label { name = "boss-name", text = "Boss Name" };
            _bossNameLabel.AddToClassList("boss-name");
            container.Add(_bossNameLabel);
            
            // Phase indicator
            _phaseLabel = new Label { name = "boss-phase", text = "Phase 1" };
            _phaseLabel.AddToClassList("boss-phase");
            container.Add(_phaseLabel);
            
            // Health bar wrapper
            var barWrapper = new VisualElement();
            barWrapper.AddToClassList("boss-bar-wrapper");
            
            // Background
            var background = new VisualElement();
            background.AddToClassList("boss-bar-background");
            barWrapper.Add(background);
            
            // Trail (delayed damage indicator)
            _healthTrail = new VisualElement { name = "health-trail" };
            _healthTrail.AddToClassList("boss-bar-trail");
            barWrapper.Add(_healthTrail);
            
            // Health fill
            _healthFill = new VisualElement { name = "health-fill" };
            _healthFill.AddToClassList("boss-bar-fill");
            barWrapper.Add(_healthFill);
            
            // Shield overlay
            _shieldFill = new VisualElement { name = "shield-fill" };
            _shieldFill.AddToClassList("boss-bar-shield");
            barWrapper.Add(_shieldFill);
            
            // Phase markers
            _phaseMarkerContainer = new VisualElement { name = "phase-markers" };
            _phaseMarkerContainer.AddToClassList("boss-phase-markers");
            barWrapper.Add(_phaseMarkerContainer);
            
            // Enrage overlay
            _enrageOverlay = new VisualElement { name = "enrage-overlay" };
            _enrageOverlay.AddToClassList("boss-bar-enrage-overlay");
            _enrageOverlay.style.display = DisplayStyle.None;
            barWrapper.Add(_enrageOverlay);
            
            container.Add(barWrapper);
            
            // Percentage text
            _percentLabel = new Label { name = "health-percent", text = "100%" };
            _percentLabel.AddToClassList("boss-health-percent");
            container.Add(_percentLabel);
            
            return container;
        }
        
        private void BindExistingElements()
        {
            _bossNameLabel = _container.Q<Label>("boss-name");
            _phaseLabel = _container.Q<Label>("boss-phase");
            _healthFill = _container.Q<VisualElement>("health-fill");
            _healthTrail = _container.Q<VisualElement>("health-trail");
            _shieldFill = _container.Q<VisualElement>("shield-fill");
            _phaseMarkerContainer = _container.Q<VisualElement>("phase-markers");
            _enrageOverlay = _container.Q<VisualElement>("enrage-overlay");
            _percentLabel = _container.Q<Label>("health-percent");
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Binding
        // ─────────────────────────────────────────────────────────────────
        protected override void OnBind()
        {
            if (ViewModel == null) return;
            
            ViewModel.BossName.OnChanged += UpdateBossName;
            ViewModel.HealthPercent.OnChanged += UpdateHealth;
            ViewModel.ShieldPercent.OnChanged += UpdateShield;
            ViewModel.CurrentPhase.OnChanged += UpdatePhase;
            ViewModel.IsEnraged.OnChanged += UpdateEnrageState;
            ViewModel.IsActive.OnChanged += UpdateVisibility;
            ViewModel.OnPhaseTransition += HandlePhaseTransition;
            
            // Initial state
            UpdateBossName(ViewModel.BossName.Value);
            SetupPhaseMarkers(ViewModel.TotalPhases.Value);
            UpdateVisibility(ViewModel.IsActive.Value);
        }
        
        protected override void OnUnbind()
        {
            if (ViewModel == null) return;
            
            ViewModel.BossName.OnChanged -= UpdateBossName;
            ViewModel.HealthPercent.OnChanged -= UpdateHealth;
            ViewModel.ShieldPercent.OnChanged -= UpdateShield;
            ViewModel.CurrentPhase.OnChanged -= UpdatePhase;
            ViewModel.IsEnraged.OnChanged -= UpdateEnrageState;
            ViewModel.IsActive.OnChanged -= UpdateVisibility;
            ViewModel.OnPhaseTransition -= HandlePhaseTransition;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Update Methods
        // ─────────────────────────────────────────────────────────────────
        protected override void Update()
        {
            base.Update();
            // Smooth health animation
            if (Mathf.Abs(_currentDisplayHealth - _targetHealth) > 0.001f)
            {
                _currentDisplayHealth = Mathf.Lerp(_currentDisplayHealth, _targetHealth, Time.deltaTime * HealthLerpSpeed);
                if (_healthFill != null)
                {
                    _healthFill.style.width = new Length(_currentDisplayHealth * 100f, LengthUnit.Percent);
                }
            }
            
            // Trail follows more slowly
            if (Mathf.Abs(_trailHealth - _currentDisplayHealth) > 0.001f)
            {
                _trailHealth = Mathf.Lerp(_trailHealth, _currentDisplayHealth, Time.deltaTime * TrailLerpSpeed);
                if (_healthTrail != null)
                {
                    _healthTrail.style.width = new Length(_trailHealth * 100f, LengthUnit.Percent);
                }
            }
        }
        
        private void UpdateBossName(string name)
        {
            if (_bossNameLabel != null)
            {
                _bossNameLabel.text = name;
            }
        }
        
        private void UpdateHealth(float percent)
        {
            _targetHealth = percent;
            
            // Only update trail if taking damage (trail behind)
            if (percent < _trailHealth)
            {
                // Trail will catch up in Update()
            }
            else
            {
                // Healing - match immediately
                _trailHealth = percent;
            }
            
            if (_percentLabel != null)
            {
                _percentLabel.text = $"{percent * 100f:F0}%";
            }
        }
        
        private void UpdateShield(float percent)
        {
            if (_shieldFill == null) return;
            
            if (percent > 0)
            {
                _shieldFill.style.display = DisplayStyle.Flex;
                _shieldFill.style.width = new Length(percent * 100f, LengthUnit.Percent);
            }
            else
            {
                _shieldFill.style.display = DisplayStyle.None;
            }
        }
        
        private void UpdatePhase(int phase)
        {
            if (_phaseLabel != null)
            {
                _phaseLabel.text = $"Phase {phase}";
            }
            
            // Highlight current phase marker
            for (int i = 0; i < _phaseMarkers.Count; i++)
            {
                if (i < phase)
                {
                    _phaseMarkers[i].AddToClassList("phase-passed");
                }
                else if (i == phase)
                {
                    _phaseMarkers[i].AddToClassList("phase-current");
                }
            }
        }
        
        private void UpdateEnrageState(bool enraged)
        {
            if (_container == null) return;
            
            if (enraged)
            {
                _container.AddToClassList(EnragedClass);
                if (_enrageOverlay != null)
                {
                    _enrageOverlay.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                _container.RemoveFromClassList(EnragedClass);
                if (_enrageOverlay != null)
                {
                    _enrageOverlay.style.display = DisplayStyle.None;
                }
            }
        }
        
        private void UpdateVisibility(bool active)
        {
            if (_container == null) return;
            
            if (active)
            {
                _container.RemoveFromClassList(HiddenClass);
                
                // Reset animation state
                _currentDisplayHealth = ViewModel?.HealthPercent.Value ?? 1f;
                _trailHealth = _currentDisplayHealth;
                _targetHealth = _currentDisplayHealth;
            }
            else
            {
                _container.AddToClassList(HiddenClass);
            }
        }
        
        private void HandlePhaseTransition(int oldPhase, int newPhase)
        {
            if (_container == null) return;
            
            // Play transition animation
            _container.AddToClassList(PhaseTransitionClass);
            _container.schedule.Execute(() => _container.RemoveFromClassList(PhaseTransitionClass)).StartingIn(500);
            
            UpdatePhase(newPhase);
        }
        
        private void SetupPhaseMarkers(int totalPhases)
        {
            if (_phaseMarkerContainer == null) return;
            
            _phaseMarkerContainer.Clear();
            _phaseMarkers.Clear();
            
            if (totalPhases <= 1) return;
            
            // Create markers at phase transition points
            for (int i = 1; i < totalPhases; i++)
            {
                float position = (float)i / totalPhases;
                
                var marker = new VisualElement();
                marker.AddToClassList("phase-marker");
                marker.style.left = new Length(position * 100f, LengthUnit.Percent);
                
                _phaseMarkerContainer.Add(marker);
                _phaseMarkers.Add(marker);
            }
        }
    }
}
