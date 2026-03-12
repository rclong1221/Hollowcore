using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Targeting.Implementations
{
    /// <summary>
    /// Base class for targeting system implementations.
    /// Provides shared logic for reading config, modifiers, and writing to TargetData.
    /// </summary>
    public abstract class TargetingSystemBase : MonoBehaviour, ITargetingSystem
    {
        [Header("Configuration")]
        [SerializeField] protected TargetingConfig _config;
        
        [Header("Entity Reference")]
        [SerializeField] protected Transform _characterTransform;
        
        // Cached state
        protected Entity _ownerEntity;
        protected EntityManager _entityManager;
        protected Entity _currentTarget;
        protected float3 _aimDirection;
        protected float3 _targetPoint;
        protected bool _hasValidTarget;
        protected float _targetDistance;
        
        // Effective values (config + modifiers)
        protected float _effectiveRange;
        protected float _effectiveAimAssist;
        protected bool _ignoreLineOfSight;
        protected TargetPriority _effectivePriority;
        
        public abstract TargetingMode Mode { get; }
        
        public virtual void Initialize(TargetingConfig config)
        {
            _config = config;
        }
        
        public virtual void UpdateTargeting(EntityManager em, Entity ownerEntity)
        {
            _entityManager = em;
            _ownerEntity = ownerEntity;
            
            // Read modifiers and compute effective values
            ComputeEffectiveValues();
            
            // Subclass-specific targeting logic
            PerformTargeting();
            
            // Write results to ECS
            WriteTargetData();
        }
        
        /// <summary>
        /// Compute effective targeting values from config + modifiers.
        /// </summary>
        protected void ComputeEffectiveValues()
        {
            if (_config == null) return;
            
            _effectiveRange = _config.MaxTargetRange;
            _effectiveAimAssist = _config.AimAssistStrength;
            _ignoreLineOfSight = !_config.RequireLineOfSight;
            _effectivePriority = _config.TargetPriority;
            
            // Apply modifiers if entity exists
            if (_ownerEntity != Entity.Null && _entityManager.Exists(_ownerEntity))
            {
                if (_entityManager.HasComponent<TargetingModifiers>(_ownerEntity))
                {
                    var mods = _entityManager.GetComponentData<TargetingModifiers>(_ownerEntity);
                    _effectiveRange += mods.RangeModifier;
                    _effectiveAimAssist += mods.AimAssistModifier;
                    _ignoreLineOfSight = _ignoreLineOfSight || mods.IgnoreLineOfSight;
                    
                    if (mods.PriorityOverride >= 0)
                    {
                        _effectivePriority = (TargetPriority)mods.PriorityOverride;
                    }
                }
            }
            
            // Clamp
            _effectiveRange = math.max(0f, _effectiveRange);
            _effectiveAimAssist = math.clamp(_effectiveAimAssist, 0f, 1f);
        }
        
        /// <summary>
        /// Subclass implements specific targeting logic.
        /// </summary>
        protected abstract void PerformTargeting();
        
        /// <summary>
        /// Write targeting results to ECS TargetData component.
        /// </summary>
        protected void WriteTargetData()
        {
            if (_ownerEntity == Entity.Null || !_entityManager.Exists(_ownerEntity)) return;
            if (!_entityManager.HasComponent<TargetData>(_ownerEntity)) return;
            
            _entityManager.SetComponentData(_ownerEntity, new TargetData
            {
                TargetEntity = _currentTarget,
                TargetPoint = _targetPoint,
                AimDirection = _aimDirection,
                HasValidTarget = _hasValidTarget,
                TargetDistance = _targetDistance,
                Mode = Mode
            });
        }
        
        // ITargetingSystem interface
        public Entity GetPrimaryTarget() => _currentTarget;
        public float3 GetAimDirection() => _aimDirection;
        public float3 GetTargetPoint() => _targetPoint;
        public bool HasValidTarget() => _hasValidTarget;
        
        public virtual void SetTarget(Entity target)
        {
            _currentTarget = target;
        }
        
        public virtual void ClearTarget()
        {
            _currentTarget = Entity.Null;
            _hasValidTarget = false;
        }
        
        public virtual void CycleTarget(int direction)
        {
            // Override in LockOnTargeting
        }
    }
}
