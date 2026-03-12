using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using DIG.Targeting.Theming;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// Bridge that connects TargetData (ECS) to indicator visuals.
    /// Reads targeting state, builds theme context, and forwards to registered indicators.
    /// This is the central coordinator for all targeting UI.
    /// </summary>
    public class TargetIndicatorBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DefaultThemeResolver _themeResolver;
        
        [Header("Registered Indicators")]
        [SerializeField] private List<MonoBehaviour> _indicatorComponents = new List<MonoBehaviour>();
        
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private bool _initialized;
        
        private List<ITargetIndicator> _indicators = new List<ITargetIndicator>();
        private List<IThemedTargetIndicator> _themedIndicators = new List<IThemedTargetIndicator>();
        
        /// <summary>
        /// Initialize bridge with player entity.
        /// </summary>
        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            _initialized = true;
            
            // Initialize theme resolver
            if (_themeResolver != null)
            {
                _themeResolver.Initialize(em, playerEntity);
            }
            
            // Cache indicator interfaces from serialized components
            _indicators.Clear();
            _themedIndicators.Clear();
            
            foreach (var component in _indicatorComponents)
            {
                if (component is IThemedTargetIndicator themed)
                {
                    _themedIndicators.Add(themed);
                    themed.Initialize(em, playerEntity);
                }
                else if (component is ITargetIndicator indicator)
                {
                    _indicators.Add(indicator);
                    indicator.Initialize(em, playerEntity);
                }
            }
        }
        
        /// <summary>
        /// Register an indicator at runtime.
        /// </summary>
        public void RegisterIndicator(ITargetIndicator indicator)
        {
            if (indicator is IThemedTargetIndicator themed)
            {
                if (!_themedIndicators.Contains(themed))
                {
                    _themedIndicators.Add(themed);
                    if (_initialized)
                        themed.Initialize(_entityManager, _playerEntity);
                }
            }
            else
            {
                if (!_indicators.Contains(indicator))
                {
                    _indicators.Add(indicator);
                    if (_initialized)
                        indicator.Initialize(_entityManager, _playerEntity);
                }
            }
        }
        
        /// <summary>
        /// Unregister an indicator.
        /// </summary>
        public void UnregisterIndicator(ITargetIndicator indicator)
        {
            if (indicator is IThemedTargetIndicator themed)
                _themedIndicators.Remove(themed);
            else
                _indicators.Remove(indicator);
        }
        
        private void LateUpdate()
        {
            if (!_initialized) return;
            
            if (_playerEntity == Entity.Null || !_entityManager.Exists(_playerEntity))
            {
                HideAll();
                return;
            }
            
            if (!_entityManager.HasComponent<TargetData>(_playerEntity))
            {
                HideAll();
                return;
            }
            
            var targetData = _entityManager.GetComponentData<TargetData>(_playerEntity);
            
            if (!targetData.HasValidTarget)
            {
                HideAll();
                return;
            }
            
            // Build theme context
            IndicatorThemeContext context = IndicatorThemeContext.Default;
            if (_themeResolver != null)
            {
                context = _themeResolver.BuildContext(targetData);
            }
            else
            {
                // Minimal context without resolver
                context.HasValidTarget = targetData.HasValidTarget;
                context.IsLocked = targetData.Mode == TargetingMode.LockOn && 
                                   targetData.TargetEntity != Entity.Null;
                context.TargetPosition = targetData.TargetPoint;
                context.Mode = targetData.Mode;
            }
            
            // Update themed indicators with full context
            foreach (var themed in _themedIndicators)
            {
                themed.SetVisible(true);
                themed.UpdateIndicatorThemed(context);
            }
            
            // Update basic indicators with simple data
            foreach (var indicator in _indicators)
            {
                indicator.SetVisible(true);
                indicator.UpdateIndicator(
                    targetData.TargetPoint,
                    targetData.HasValidTarget,
                    context.IsLocked
                );
            }
        }
        
        private void HideAll()
        {
            foreach (var themed in _themedIndicators)
                themed.SetVisible(false);
            foreach (var indicator in _indicators)
                indicator.SetVisible(false);
        }
    }
}
