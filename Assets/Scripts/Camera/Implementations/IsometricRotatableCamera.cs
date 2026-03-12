using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.CameraSystem.Implementations
{
    /// <summary>
    /// Isometric camera with Q/E rotation support.
    /// Extends the fixed isometric camera with the ability to rotate
    /// the view in discrete increments (typically 45° or 90°).
    /// Features:
    /// - All IsometricFixedCamera features
    /// - Q/E rotation in configurable increments
    /// - Smooth rotation animation
    /// - Movement input updates with rotation
    /// </summary>
    public class IsometricRotatableCamera : CameraModeBase
    {
        // ============================================================
        // STATE
        // ============================================================

        private float3 _smoothedFollowPosition;
        private bool _initialized;

        // Rotation state
        private float _currentRotation;
        private float _targetRotation;
        private bool _isRotating;
        private float _rotationProgress;

        // ============================================================
        // ICAMERAMODE PROPERTIES
        // ============================================================

        public override CameraMode Mode => CameraMode.IsometricRotatable;
        public override bool SupportsOrbitRotation => false; // Uses Q/E instead
        public override bool UsesCursorAiming => true;

        /// <summary>
        /// Current camera rotation in degrees.
        /// </summary>
        public float Rotation => _currentRotation;

        /// <summary>
        /// Is the camera currently animating a rotation?
        /// </summary>
        public bool IsRotating => _isRotating;

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

            // Initialize rotation from config
            _currentRotation = config.IsometricRotation;
            _targetRotation = _currentRotation;
            _isRotating = false;
            _rotationProgress = 0f;

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

            // Update rotation animation
            UpdateRotation(deltaTime);

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

            // Calculate camera position based on current (animated) rotation
            float pitchRad = math.radians(_config.IsometricAngle);
            float yawRad = math.radians(_currentRotation);

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
                math.radians(_currentRotation),
                0f
            );

            // Smooth camera movement (but not during rotation - that's handled separately)
            float3 currentPos = _camera.transform.position;
            quaternion currentRot = _camera.transform.rotation;

            float positionSmoothFactor = _isRotating ? deltaTime * 20f : deltaTime * _config.FollowSmoothing;
            float3 smoothedPos = math.lerp(currentPos, cameraPosition, math.saturate(positionSmoothFactor));
            quaternion smoothedRot = math.slerp(currentRot, cameraRotation, math.saturate(positionSmoothFactor));

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
        // ROTATION CONTROL
        // ============================================================

        private void UpdateRotation(float deltaTime)
        {
            if (!_isRotating || _config == null)
                return;

            _rotationProgress += deltaTime / _config.RotationDuration;

            if (_rotationProgress >= 1f)
            {
                // Rotation complete
                _currentRotation = _targetRotation;
                _isRotating = false;
                _rotationProgress = 0f;
            }
            else
            {
                // Smooth interpolation with easing
                float t = EaseInOutCubic(_rotationProgress);
                _currentRotation = CameraInputUtility.LerpRotation(
                    _currentRotation,
                    _targetRotation,
                    t
                );
            }
        }

        /// <summary>
        /// Rotate camera by one increment in the given direction.
        /// </summary>
        /// <param name="direction">-1 for counter-clockwise (Q), 1 for clockwise (E).</param>
        public void RotateStep(int direction)
        {
            if (_config == null || _isRotating)
                return;

            float startRotation = _targetRotation; // Use target in case we're mid-rotation
            _targetRotation = CameraInputUtility.StepRotation(startRotation, _config.RotationIncrement, direction);

            // Only animate if actually changing
            if (math.abs(_targetRotation - _currentRotation) > 0.01f)
            {
                _isRotating = true;
                _rotationProgress = 0f;
            }
        }

        /// <summary>
        /// Rotate counter-clockwise (Q key).
        /// </summary>
        public void RotateLeft()
        {
            RotateStep(-1);
        }

        /// <summary>
        /// Rotate clockwise (E key).
        /// </summary>
        public void RotateRight()
        {
            RotateStep(1);
        }

        /// <summary>
        /// Snap to a specific rotation instantly.
        /// </summary>
        public void SetRotation(float rotation)
        {
            _currentRotation = rotation;
            _targetRotation = rotation;
            _isRotating = false;
            _rotationProgress = 0f;

            // Normalize to 0-360 range
            while (_currentRotation < 0f) _currentRotation += 360f;
            while (_currentRotation >= 360f) _currentRotation -= 360f;
            _targetRotation = _currentRotation;
        }

        private float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - math.pow(-2f * t + 2f, 3f) / 2f;
        }

        // ============================================================
        // INPUT HANDLING
        // ============================================================

        public override void HandleRotationInput(float2 rotationInput)
        {
            // Rotatable isometric uses Q/E keys, not mouse
            // Input is handled via RotateLeft/RotateRight methods
        }

        public override float3 TransformMovementInput(float2 input)
        {
            // Use current (animated) rotation for movement transformation
            return CameraInputUtility.TransformIsometricInput(input, _currentRotation);
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
