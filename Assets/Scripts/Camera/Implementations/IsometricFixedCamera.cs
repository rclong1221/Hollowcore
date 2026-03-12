using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.CameraSystem.Implementations
{
    /// <summary>
    /// Isometric fixed-angle camera implementation for ARPG games.
    /// Diablo/Hades style camera that follows the character at a fixed angle.
    /// Features:
    /// - Fixed pitch and yaw angles
    /// - Cursor-to-world projection for aiming
    /// - Zoom via scroll wheel
    /// - Optional orthographic projection
    /// - Follow deadzone
    /// </summary>
    public class IsometricFixedCamera : CameraModeBase
    {
        // ============================================================
        // STATE
        // ============================================================

        private float3 _smoothedFollowPosition;
        private bool _initialized;

        // ============================================================
        // ICAMERAMODE PROPERTIES
        // ============================================================

        public override CameraMode Mode => CameraMode.IsometricFixed;
        public override bool SupportsOrbitRotation => false;
        public override bool UsesCursorAiming => true;

        /// <summary>
        /// Current camera rotation in degrees.
        /// </summary>
        public float Rotation => _config?.IsometricRotation ?? 45f;

        // ============================================================
        // INITIALIZATION
        // ============================================================

        public override void Initialize(CameraConfig config)
        {
            base.Initialize(config);

            // Setup orthographic if configured
            if (_camera != null)
            {
                _camera.orthographic = config.UseOrthographic;
                if (config.UseOrthographic)
                {
                    _camera.orthographicSize = config.OrthoSize;
                }
            }

            _initialized = false;
        }

        // ============================================================
        // CAMERA UPDATE
        // ============================================================

        public override void UpdateCamera(float deltaTime)
        {
            if (_camera == null || _config == null)
                return;

            UpdateTargetPosition();

            // Initialize smoothed position on first update
            if (!_initialized)
            {
                _smoothedFollowPosition = _targetPosition;
                _initialized = true;
            }

            // Apply deadzone - only follow if character moved far enough
            float3 toTarget = _targetPosition - _smoothedFollowPosition;
            float distanceFromCenter = math.length(new float2(toTarget.x, toTarget.z));

            if (distanceFromCenter > _config.FollowDeadzone)
            {
                // Move toward target, maintaining deadzone
                float3 direction = math.normalize(toTarget);
                float moveAmount = distanceFromCenter - _config.FollowDeadzone;
                _smoothedFollowPosition += direction * moveAmount * math.saturate(deltaTime * _config.FollowSmoothing);
            }

            // Calculate camera position based on angle and height
            float pitchRad = math.radians(_config.IsometricAngle);
            float yawRad = math.radians(_config.IsometricRotation);

            // Calculate offset from character based on angle
            float horizontalDistance = _config.IsometricHeight / math.tan(pitchRad);
            float3 offset = new float3(
                -math.sin(yawRad) * horizontalDistance,
                _config.IsometricHeight,
                -math.cos(yawRad) * horizontalDistance
            );

            // Apply zoom to height/distance
            float zoomMultiplier = math.lerp(0.5f, 1.5f, _currentZoom);
            offset *= zoomMultiplier;

            float3 cameraPosition = _smoothedFollowPosition + offset;

            // Calculate rotation to look at follow position
            quaternion cameraRotation = quaternion.Euler(
                math.radians(_config.IsometricAngle),
                math.radians(_config.IsometricRotation),
                0f
            );

            // Smooth camera movement
            float3 currentPos = _camera.transform.position;
            quaternion currentRot = _camera.transform.rotation;

            float3 smoothedPos = SmoothPosition(currentPos, cameraPosition, deltaTime);
            quaternion smoothedRot = SmoothRotation(currentRot, cameraRotation, deltaTime);

            _camera.transform.SetPositionAndRotation(smoothedPos, smoothedRot);

            // Update orthographic size based on zoom
            if (_camera.orthographic)
            {
                float targetOrthoSize = _config.OrthoSize * zoomMultiplier;
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, targetOrthoSize, deltaTime * 10f);
            }

            // Apply shake after positioning
            ApplyShake(deltaTime);
        }

        // ============================================================
        // INPUT HANDLING
        // ============================================================

        public override void HandleRotationInput(float2 rotationInput)
        {
            // Fixed isometric camera doesn't respond to rotation input
            // Use IsometricRotatableCamera if rotation is needed
        }

        public override float3 TransformMovementInput(float2 input)
        {
            if (_config == null)
                return CameraInputUtility.TransformIsometricInput(input, 45f);

            return CameraInputUtility.TransformIsometricInput(input, _config.IsometricRotation);
        }

        public override float3 TransformAimInput(float2 cursorScreenPos)
        {
            if (_camera == null || _config == null)
                return _targetPosition + new float3(0f, 0f, 1f);

            // Project cursor to world based on configured projection method
            return CameraInputUtility.ProjectCursor(
                cursorScreenPos,
                _camera,
                _config.CursorProjection,
                _config,
                _targetPosition.y
            );
        }

        public override Plane GetAimPlane()
        {
            // For isometric, aim plane is the ground plane at character height
            float height = _config?.CursorProjectionHeight ?? 0f;
            if (_config?.CursorProjection == CursorProjectionMethod.FixedHeight)
            {
                height = _targetPosition.y;
            }

            return new Plane(Vector3.up, new Vector3(0f, height, 0f));
        }

        // ============================================================
        // ZOOM CONTROL
        // ============================================================

        /// <summary>
        /// Adjust zoom by a delta amount (e.g., from scroll wheel).
        /// Positive = zoom out, negative = zoom in.
        /// </summary>
        public void AdjustZoom(float delta)
        {
            if (_config == null) return;

            float zoomDelta = delta * _config.ZoomSpeed * 0.1f;
            SetZoom(_currentZoom + zoomDelta);
        }

        // ============================================================
        // UTILITY
        // ============================================================

        /// <summary>
        /// Get the world position under the cursor.
        /// Convenience method for gameplay code.
        /// </summary>
        public float3 GetCursorWorldPosition()
        {
            if (_camera == null)
                return _targetPosition;

            Vector3 mousePos = Input.mousePosition;
            return TransformAimInput(new float2(mousePos.x, mousePos.y));
        }

        /// <summary>
        /// Get the aim direction from character to cursor.
        /// </summary>
        public float3 GetAimDirection()
        {
            float3 cursorWorld = GetCursorWorldPosition();
            return CameraInputUtility.GetIsometricAimDirection(_targetPosition, cursorWorld);
        }

        /// <summary>
        /// Snap camera to target position immediately (no smoothing).
        /// Useful when teleporting or respawning.
        /// </summary>
        public void SnapToTarget()
        {
            UpdateTargetPosition();
            _smoothedFollowPosition = _targetPosition;
            _initialized = true;
        }
    }
}
