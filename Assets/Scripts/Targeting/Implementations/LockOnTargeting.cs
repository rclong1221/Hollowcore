using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Targeting.Implementations
{
    /// <summary>
    /// Lock-on targeting for Souls-like games.
    /// EPIC 15.16: Tab key lock-on DISABLED. Use TargetLockSettingsManager to control lock programmatically.
    /// Cycling keys (Q/E) still work if locked on via other means.
    /// </summary>
    public class LockOnTargeting : TargetingSystemBase
    {
        [Header("Lock-On Settings")]
        [SerializeField] private KeyCode _cycleNextKey = KeyCode.E;
        [SerializeField] private KeyCode _cyclePrevKey = KeyCode.Q;
        [SerializeField] private float _lockOnAngle = 45f;
        [SerializeField] private string _enemyTag = "Enemy";
        
        private bool _isLockedOn;
        private List<Entity> _validTargets = new List<Entity>();
        private List<Vector3> _targetPositions = new List<Vector3>();
        private int _currentTargetIndex;
        private Collider[] _overlapResults = new Collider[32];
        
        public override TargetingMode Mode => TargetingMode.LockOn;
        
        public bool IsLockedOn => _isLockedOn;
        
        private void Update()
        {
            // EPIC 15.16: Check if target locking is allowed
            bool allowTargetLock = TargetLockSettingsManager.Instance.AllowTargetLock;
            
            // If locked on but settings disallow it, break lock immediately
            if (_isLockedOn && !allowTargetLock)
            {
                BreakLockOn();
                return;
            }
            
            // EPIC 15.16: NO Tab key lock-on. Lock is controlled via debug tester / UI only.
            
            // Handle cycling (only if locked on and allowed)
            if (_isLockedOn && allowTargetLock)
            {
                if (Input.GetKeyDown(_cycleNextKey))
                {
                    CycleTarget(1);
                }
                else if (Input.GetKeyDown(_cyclePrevKey))
                {
                    CycleTarget(-1);
                }
            }
        }
        
        protected override void PerformTargeting()
        {
            if (_characterTransform == null)
            {
                _aimDirection = new float3(0, 0, 1);
                _targetPoint = float3.zero;
                _hasValidTarget = false;
                return;
            }
            
            if (_isLockedOn && _currentTarget != Entity.Null)
            {
                // Validate lock is still valid
                if (ValidateLockOn())
                {
                    UpdateAimToTarget();
                }
                else
                {
                    // Try next target or break lock
                    if (_validTargets.Count > 1)
                    {
                        CycleTarget(1);
                    }
                    else
                    {
                        BreakLockOn();
                    }
                }
            }
            else
            {
                // Not locked on - aim forward
                _aimDirection = _characterTransform.forward;
                _targetPoint = (float3)_characterTransform.position + _aimDirection * _effectiveRange;
                _targetDistance = _effectiveRange;
                _hasValidTarget = false;
            }
        }
        
        private void TryLockOn()
        {
            RefreshValidTargets();
            
            if (_validTargets.Count == 0)
            {
                _isLockedOn = false;
                return;
            }
            
            // Find target closest to center of view
            Vector3 forward = _characterTransform.forward;
            Vector3 center = _characterTransform.position;
            
            int bestIndex = 0;
            float bestDot = -1f;
            
            for (int i = 0; i < _validTargets.Count; i++)
            {
                Vector3 toTarget = (_targetPositions[i] - center).normalized;
                float dot = Vector3.Dot(forward, toTarget);
                
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }
            
            _currentTargetIndex = bestIndex;
            _currentTarget = _validTargets[bestIndex];
            _targetPoint = _targetPositions[bestIndex];
            _isLockedOn = true;
            _hasValidTarget = true;
        }
        
        private void BreakLockOn()
        {
            _isLockedOn = false;
            _currentTarget = Entity.Null;
            _hasValidTarget = false;
            _validTargets.Clear();
            _targetPositions.Clear();
        }
        
        public override void CycleTarget(int direction)
        {
            if (!_isLockedOn) return;
            
            RefreshValidTargets();
            
            if (_validTargets.Count <= 1) return;
            
            _currentTargetIndex = (_currentTargetIndex + direction + _validTargets.Count) % _validTargets.Count;
            _currentTarget = _validTargets[_currentTargetIndex];
            _targetPoint = _targetPositions[_currentTargetIndex];
        }
        
        private void RefreshValidTargets()
        {
            _validTargets.Clear();
            _targetPositions.Clear();
            
            Vector3 center = _characterTransform.position;
            Vector3 forward = _characterTransform.forward;
            float maxDist = _config != null ? _config.LockOnMaxDistance : _effectiveRange;
            float maxAngle = _config != null ? _config.LockOnMaxAngle : _lockOnAngle;
            
            int count = Physics.OverlapSphereNonAlloc(center, maxDist, _overlapResults,
                _config != null ? (int)_config.ValidTargetLayers : ~0);
            
            for (int i = 0; i < count; i++)
            {
                var collider = _overlapResults[i];
                if (collider == null) continue;
                
                if (!string.IsNullOrEmpty(_enemyTag) && !collider.CompareTag(_enemyTag))
                    continue;
                
                var entityLink = collider.GetComponentInParent<EntityLink>();
                if (entityLink == null) continue;
                
                Vector3 toTarget = collider.bounds.center - center;
                float angle = Vector3.Angle(forward, toTarget);
                
                if (angle > maxAngle) continue;
                
                // Check line of sight
                if (!_ignoreLineOfSight)
                {
                    if (Physics.Raycast(center, toTarget.normalized, toTarget.magnitude * 0.9f,
                        ~(int)(_config != null ? _config.ValidTargetLayers : (LayerMask)0)))
                    {
                        continue;
                    }
                }
                
                _validTargets.Add(entityLink.Entity);
                _targetPositions.Add(collider.bounds.center);
            }
        }
        
        private bool ValidateLockOn()
        {
            if (_currentTarget == Entity.Null) return false;
            if (_targetDistance > (_config != null ? _config.LockOnMaxDistance : _effectiveRange)) return false;
            
            // Would need to check entity still exists via EntityManager
            return true;
        }
        
        private void UpdateAimToTarget()
        {
            float3 charPos = _characterTransform.position;
            float3 toTarget = _targetPoint - charPos;
            _targetDistance = math.length(toTarget);
            
            if (_targetDistance > 0.01f)
            {
                _aimDirection = math.normalize(toTarget);
            }
            _hasValidTarget = true;
        }
    }
}
