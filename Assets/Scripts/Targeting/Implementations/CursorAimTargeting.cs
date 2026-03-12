using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.CameraSystem;

namespace DIG.Targeting.Implementations
{
    /// <summary>
    /// Cursor aim targeting for ARPG/Isometric games.
    /// Fires toward mouse cursor position in world space.
    ///
    /// Integrates with EPIC 14.9 camera system:
    /// - Uses ICameraMode.TransformAimInput() for cursor projection when available
    /// - Falls back to legacy ground plane raycast if no camera system
    /// </summary>
    public class CursorAimTargeting : CameraAwareTargetingBase
    {
        [Header("Cursor Settings (Legacy Fallback)")]
        [SerializeField] private LayerMask _groundLayers = 1; // Default layer
        [SerializeField] private float _groundPlaneHeight = 0f;

        public override TargetingMode Mode => TargetingMode.CursorAim;

        protected override void PerformTargeting()
        {
            if (_characterTransform == null)
            {
                _aimDirection = new float3(0, 0, 1);
                _targetPoint = float3.zero;
                _hasValidTarget = false;
                return;
            }

            // Get cursor world position using camera system (EPIC 14.9) or legacy fallback
            Vector3 mousePos = Input.mousePosition;
            float2 cursorScreenPos = new float2(mousePos.x, mousePos.y);

            if (_useCameraSystem && HasActiveCamera)
            {
                // Use camera system for cursor projection
                _targetPoint = ProjectCursorToWorld(cursorScreenPos);
            }
            else
            {
                // Legacy: direct raycast to ground
                _targetPoint = ProjectCursorLegacy(cursorScreenPos, _groundLayers, _groundPlaneHeight);
            }

            // Calculate aim direction from character to target point
            float3 charPos = _characterTransform.position;
            float3 toTarget = _targetPoint - charPos;
            toTarget.y = 0f; // Flatten for horizontal aim
            _targetDistance = math.length(toTarget);

            if (_targetDistance > 0.01f)
            {
                _aimDirection = math.normalize(toTarget);
            }
            else
            {
                _aimDirection = new float3(0, 0, 1);
            }

            // Check for entity at cursor position (for skill targeting)
            var camera = GetCamera();
            if (camera != null)
            {
                Ray ray = camera.ScreenPointToRay(mousePos);
                if (Physics.Raycast(ray, out RaycastHit entityHit, _effectiveRange, _config != null ? _config.ValidTargetLayers : ~0))
                {
                    var entityLink = entityHit.collider.GetComponentInParent<EntityLink>();
                    if (entityLink != null)
                    {
                        _currentTarget = entityLink.Entity;
                        _hasValidTarget = true;
                        return;
                    }
                }
            }

            _currentTarget = Entity.Null;
            _hasValidTarget = false;
        }

        /// <summary>
        /// Legacy cursor projection with ground layer support.
        /// </summary>
        private float3 ProjectCursorLegacy(float2 cursorScreenPos, LayerMask groundLayers, float groundHeight)
        {
            var camera = GetCamera();
            if (camera == null)
                return float3.zero;

            Ray ray = camera.ScreenPointToRay(new Vector3(cursorScreenPos.x, cursorScreenPos.y, 0f));

            // Try to hit ground first
            if (Physics.Raycast(ray, out RaycastHit groundHit, _effectiveRange * 2f, groundLayers))
            {
                return (float3)groundHit.point;
            }

            // Fallback: project to ground plane
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundHeight, 0));
            if (groundPlane.Raycast(ray, out float enter))
            {
                return (float3)ray.GetPoint(enter);
            }

            return (float3)(ray.origin + ray.direction * _effectiveRange);
        }
    }
}
