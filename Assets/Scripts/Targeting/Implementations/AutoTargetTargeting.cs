using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Targeting.Implementations
{
    /// <summary>
    /// Auto-target targeting for fast-paced ARPG.
    /// Automatically locks to nearest valid enemy in range.
    /// </summary>
    public class AutoTargetTargeting : TargetingSystemBase
    {
        [Header("Auto-Target Settings")]
        [SerializeField] private float _updateInterval = 0.1f;
        [SerializeField] private string _enemyTag = "Enemy";
        
        private float _lastUpdateTime;
        private Collider[] _overlapResults = new Collider[32];
        
        public override TargetingMode Mode => TargetingMode.AutoTarget;
        
        protected override void PerformTargeting()
        {
            if (_characterTransform == null)
            {
                _aimDirection = new float3(0, 0, 1);
                _targetPoint = float3.zero;
                _hasValidTarget = false;
                return;
            }
            
            // Throttle scanning for performance
            if (Time.time - _lastUpdateTime < _updateInterval && _hasValidTarget)
            {
                // Validate current target still in range
                if (_currentTarget != Entity.Null && ValidateTarget())
                {
                    UpdateAimToTarget();
                    return;
                }
            }
            
            _lastUpdateTime = Time.time;
            
            // Find all potential targets
            Vector3 center = _characterTransform.position;
            int count = Physics.OverlapSphereNonAlloc(center, _effectiveRange, _overlapResults, 
                _config != null ? (int)_config.ValidTargetLayers : ~0);
            
            Entity bestTarget = Entity.Null;
            float bestScore = float.MaxValue;
            Vector3 bestPosition = Vector3.zero;
            
            for (int i = 0; i < count; i++)
            {
                var collider = _overlapResults[i];
                if (collider == null) continue;
                
                // Check tag if specified
                if (!string.IsNullOrEmpty(_enemyTag) && !collider.CompareTag(_enemyTag))
                    continue;
                
                // Check for entity link
                var entityLink = collider.GetComponentInParent<EntityLink>();
                if (entityLink == null) continue;
                
                // Check line of sight if required
                if (!_ignoreLineOfSight)
                {
                    Vector3 toTarget = collider.bounds.center - center;
                    if (Physics.Raycast(center, toTarget.normalized, toTarget.magnitude * 0.9f, 
                        ~(int)(_config != null ? _config.ValidTargetLayers : (LayerMask)0)))
                    {
                        continue; // Blocked
                    }
                }
                
                // Calculate score based on priority
                float score = CalculatePriorityScore(collider, entityLink.Entity, center);
                
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = entityLink.Entity;
                    bestPosition = collider.bounds.center;
                }
            }
            
            if (bestTarget != Entity.Null)
            {
                _currentTarget = bestTarget;
                _targetPoint = bestPosition;
                _hasValidTarget = true;
                UpdateAimToTarget();
            }
            else
            {
                _currentTarget = Entity.Null;
                _hasValidTarget = false;
                _aimDirection = _characterTransform.forward;
                _targetPoint = (float3)_characterTransform.position + _aimDirection * _effectiveRange;
                _targetDistance = _effectiveRange;
            }
        }
        
        private float CalculatePriorityScore(Collider collider, Entity entity, Vector3 center)
        {
            float distance = Vector3.Distance(center, collider.bounds.center);
            
            switch (_effectivePriority)
            {
                case TargetPriority.Nearest:
                    return distance;
                    
                case TargetPriority.CursorProximity:
                    // Distance from cursor (if available)
                    Vector3 cursorWorld = GetCursorWorldPosition();
                    return Vector3.Distance(cursorWorld, collider.bounds.center);
                    
                // LowestHealth and HighestThreat would need health/threat components
                // For now, fall back to nearest
                default:
                    return distance;
            }
        }
        
        private Vector3 GetCursorWorldPosition()
        {
            var cam = Camera.main;
            if (cam == null) return _characterTransform.position;
            
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane ground = new Plane(Vector3.up, _characterTransform.position);
            if (ground.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }
            return _characterTransform.position;
        }
        
        private bool ValidateTarget()
        {
            // Check if target still exists and in range
            // This would require EntityManager access to properly validate
            return _hasValidTarget && _targetDistance <= _effectiveRange;
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
        }
    }
}
