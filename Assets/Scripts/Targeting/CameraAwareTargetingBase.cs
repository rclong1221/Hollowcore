using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.CameraSystem;

namespace DIG.Targeting
{
    /// <summary>
    /// Extended targeting base class that integrates with the camera system (EPIC 14.9).
    /// Provides camera-aware cursor projection and aim direction calculation.
    ///
    /// Use this base class for targeting implementations that need camera context:
    /// - Cursor projection based on camera mode
    /// - Aim direction from camera or character-to-cursor
    /// - Automatic camera detection via CameraModeProvider
    ///
    /// For targeting implementations that don't need camera integration,
    /// use TargetingSystemBase directly.
    /// </summary>
    public abstract class CameraAwareTargetingBase : Implementations.TargetingSystemBase
    {
        // ============================================================
        // CAMERA INTEGRATION
        // ============================================================

        [Header("Camera Integration (EPIC 14.9)")]
        [Tooltip("Use the camera system for cursor projection. If false, uses legacy direct raycast.")]
        [SerializeField] protected bool _useCameraSystem = true;

        [Tooltip("Fallback camera if CameraModeProvider has no active camera.")]
        [SerializeField] protected Camera _fallbackCamera;

        /// <summary>
        /// The active camera mode, if available.
        /// </summary>
        protected ICameraMode ActiveCamera => CameraModeProvider.HasInstance
            ? CameraModeProvider.Instance.ActiveCamera
            : null;

        /// <summary>
        /// Whether we have an active camera mode to use.
        /// </summary>
        protected bool HasActiveCamera => ActiveCamera != null;

        /// <summary>
        /// Get the camera to use for raycasting/projection.
        /// Returns camera from ICameraMode if available, otherwise falls back to Camera.main.
        /// </summary>
        protected Camera GetCamera()
        {
            if (_useCameraSystem && HasActiveCamera)
            {
                var transform = ActiveCamera.GetCameraTransform();
                if (transform != null)
                {
                    var cam = transform.GetComponent<Camera>();
                    if (cam != null)
                        return cam;
                }
            }

            return _fallbackCamera != null ? _fallbackCamera : Camera.main;
        }

        // ============================================================
        // CURSOR PROJECTION
        // ============================================================

        /// <summary>
        /// Project cursor screen position to world space using the camera system.
        /// For isometric/top-down cameras, projects to ground plane.
        /// For third-person, uses camera raycast.
        /// </summary>
        /// <param name="cursorScreenPos">Cursor position in screen coordinates.</param>
        /// <returns>World-space position.</returns>
        protected float3 ProjectCursorToWorld(float2 cursorScreenPos)
        {
            if (_useCameraSystem && HasActiveCamera)
            {
                return ActiveCamera.TransformAimInput(cursorScreenPos);
            }

            // Legacy fallback: direct raycast to ground plane
            return ProjectCursorLegacy(cursorScreenPos);
        }

        /// <summary>
        /// Project cursor using legacy method (direct raycast).
        /// </summary>
        protected float3 ProjectCursorLegacy(float2 cursorScreenPos)
        {
            var camera = GetCamera();
            if (camera == null)
                return float3.zero;

            Ray ray = camera.ScreenPointToRay(new Vector3(cursorScreenPos.x, cursorScreenPos.y, 0f));
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
            {
                return (float3)ray.GetPoint(distance);
            }

            return (float3)(ray.origin + ray.direction * 100f);
        }

        /// <summary>
        /// Get cursor world position using current mouse position.
        /// Convenience method that reads Input.mousePosition.
        /// </summary>
        protected float3 GetCursorWorldPosition()
        {
            Vector3 mousePos = Input.mousePosition;
            return ProjectCursorToWorld(new float2(mousePos.x, mousePos.y));
        }

        // ============================================================
        // AIM DIRECTION
        // ============================================================

        /// <summary>
        /// Calculate aim direction based on camera mode.
        /// For third-person: camera forward direction.
        /// For isometric/top-down: character to cursor direction.
        /// </summary>
        /// <param name="characterPosition">World position of the character.</param>
        /// <returns>Normalized aim direction.</returns>
        protected float3 CalculateCameraAwareAimDirection(float3 characterPosition)
        {
            if (_useCameraSystem && HasActiveCamera)
            {
                if (ActiveCamera.UsesCursorAiming)
                {
                    // Isometric/top-down: aim from character to cursor
                    float3 cursorWorld = GetCursorWorldPosition();
                    return CameraInputUtility.GetIsometricAimDirection(characterPosition, cursorWorld);
                }
                else
                {
                    // Third-person: aim from camera center
                    var camera = GetCamera();
                    if (camera != null)
                    {
                        return CameraInputUtility.GetThirdPersonAimDirection(camera);
                    }
                }
            }

            // Fallback: forward direction
            if (_characterTransform != null)
            {
                return (float3)_characterTransform.forward;
            }

            return new float3(0f, 0f, 1f);
        }

        /// <summary>
        /// Get the aim plane based on camera mode.
        /// For third-person: plane perpendicular to camera forward.
        /// For isometric/top-down: ground plane.
        /// </summary>
        protected Plane GetAimPlane()
        {
            if (_useCameraSystem && HasActiveCamera)
            {
                return ActiveCamera.GetAimPlane();
            }

            // Default ground plane
            return new Plane(Vector3.up, Vector3.zero);
        }

        // ============================================================
        // MOVEMENT INPUT (for reference)
        // ============================================================

        /// <summary>
        /// Transform movement input based on camera mode.
        /// This is primarily for reference - actual movement transformation
        /// should be done in the movement system using CameraInputBridge.
        /// </summary>
        /// <param name="rawInput">Raw WASD input.</param>
        /// <returns>World-space movement direction.</returns>
        protected float3 TransformMovementInput(float2 rawInput)
        {
            if (_useCameraSystem && HasActiveCamera)
            {
                return ActiveCamera.TransformMovementInput(rawInput);
            }

            // Fallback: world-aligned (no transformation)
            return CameraInputUtility.TransformTopDownInput(rawInput);
        }

        // ============================================================
        // INITIALIZATION
        // ============================================================

        protected virtual void Start()
        {
            // Try to get fallback camera if not set
            if (_fallbackCamera == null)
            {
                _fallbackCamera = Camera.main;
            }

            // Ensure CameraModeProvider exists
            if (_useCameraSystem && !CameraModeProvider.HasInstance)
            {
                // Auto-detect camera when provider is first accessed
                CameraModeProvider.Instance.AutoDetectCamera();
            }
        }
    }
}
