using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Targeting.Implementations
{
    /// <summary>
    /// Click-select targeting for Diablo-style games.
    /// Click on enemy to select, then use abilities on selected target.
    /// </summary>
    public class ClickSelectTargeting : TargetingSystemBase
    {
        [Header("Click Settings")]
        [SerializeField] private Camera _camera;
        [SerializeField] private int _mouseButton = 0; // Left click
        [SerializeField] private LayerMask _groundLayers = 1;
        
        private Vector3 _selectedPosition;
        
        public override TargetingMode Mode => TargetingMode.ClickSelect;
        
        public bool HasSelectedTarget => _currentTarget != Entity.Null;
        
        private void Awake()
        {
            if (_camera == null)
                _camera = Camera.main;
        }
        
        private void Update()
        {
            if (Input.GetMouseButtonDown(_mouseButton))
            {
                TrySelectTarget();
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
            
            if (_currentTarget != Entity.Null)
            {
                // Update aim to selected target
                // Note: Would need to track target position updates
                UpdateAimToTarget();
            }
            else
            {
                // No target - use ground position from last click or aim forward
                if (_selectedPosition != Vector3.zero)
                {
                    UpdateAimToPosition(_selectedPosition);
                }
                else
                {
                    _aimDirection = _characterTransform.forward;
                    _targetPoint = (float3)_characterTransform.position + _aimDirection * _effectiveRange;
                    _targetDistance = _effectiveRange;
                }
            }
        }
        
        private void TrySelectTarget()
        {
            if (_camera == null) return;
            
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            
            // First try to hit an entity
            if (Physics.Raycast(ray, out RaycastHit hit, _effectiveRange * 2f, 
                _config != null ? _config.ValidTargetLayers : ~0))
            {
                var entityLink = hit.collider.GetComponentInParent<EntityLink>();
                if (entityLink != null)
                {
                    _currentTarget = entityLink.Entity;
                    _targetPoint = hit.collider.bounds.center;
                    _selectedPosition = hit.collider.bounds.center;
                    _hasValidTarget = true;
                    return;
                }
            }
            
            // No entity hit - try ground for position targeting
            if (Physics.Raycast(ray, out RaycastHit groundHit, _effectiveRange * 2f, _groundLayers))
            {
                _selectedPosition = groundHit.point;
                _targetPoint = groundHit.point;
                _currentTarget = Entity.Null;
                _hasValidTarget = false;
            }
        }
        
        public override void ClearTarget()
        {
            base.ClearTarget();
            _selectedPosition = Vector3.zero;
        }
        
        private void UpdateAimToTarget()
        {
            // In a full implementation, we'd query the target entity's position
            // For now, use the stored target point
            float3 charPos = _characterTransform.position;
            float3 toTarget = _targetPoint - charPos;
            _targetDistance = math.length(toTarget);
            
            if (_targetDistance > 0.01f)
            {
                _aimDirection = math.normalize(toTarget);
            }
            _hasValidTarget = true;
        }
        
        private void UpdateAimToPosition(Vector3 position)
        {
            float3 charPos = _characterTransform.position;
            float3 toTarget = (float3)position - charPos;
            _targetDistance = math.length(toTarget);
            
            if (_targetDistance > 0.01f)
            {
                _aimDirection = math.normalize(toTarget);
            }
            _targetPoint = position;
        }
    }
}
