using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Targeting.Implementations
{
    /// <summary>
    /// Camera raycast targeting for TPS/FPS games.
    /// Fires toward screen center (crosshair).
    /// </summary>
    public class CameraRaycastTargeting : TargetingSystemBase
    {
        [Header("Camera Settings")]
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _hitLayers = ~0;
        
        public override TargetingMode Mode => TargetingMode.CameraRaycast;
        
        private void Awake()
        {
            if (_camera == null)
                _camera = Camera.main;
        }
        
        protected override void PerformTargeting()
        {
            if (_camera == null)
            {
                _aimDirection = new float3(0, 0, 1);
                _targetPoint = float3.zero;
                _hasValidTarget = false;
                return;
            }
            
            // Raycast from camera center
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            if (Physics.Raycast(ray, out RaycastHit hit, _effectiveRange, _config != null ? _config.ValidTargetLayers : _hitLayers))
            {
                _targetPoint = hit.point;
                _targetDistance = hit.distance;
                _aimDirection = math.normalize(_targetPoint - (float3)_camera.transform.position);
                
                // Check if target is valid (has entity reference)
                var entityLink = hit.collider.GetComponentInParent<EntityLink>();
                if (entityLink != null)
                {
                    _currentTarget = entityLink.Entity;
                    _hasValidTarget = true;
                }
                else
                {
                    _currentTarget = Entity.Null;
                    _hasValidTarget = false;
                }
            }
            else
            {
                // No hit - aim forward at max range
                _aimDirection = ray.direction;
                _targetPoint = (float3)ray.origin + (float3)ray.direction * _effectiveRange;
                _targetDistance = _effectiveRange;
                _currentTarget = Entity.Null;
                _hasValidTarget = false;
            }
        }
    }
    
    /// <summary>
    /// Component to link GameObjects to ECS entities.
    /// Attach to entity-linked prefabs for targeting detection.
    /// </summary>
    public class EntityLink : MonoBehaviour
    {
        public Entity Entity;
    }
}
