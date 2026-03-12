using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Core.Input;
using Player.Systems;

namespace DIG.CameraSystem.Implementations
{
    /// <summary>
    /// Top-down fixed camera implementation.
    /// Camera looks straight down (or near-vertical) at the character.
    /// Features:
    /// - Near-vertical viewing angle (85-90°)
    /// - Simple world-aligned movement
    /// - Cursor-to-world projection
    /// - Zoom control
    /// Good for twin-stick shooters, roguelikes, and strategy games.
    /// </summary>
    public class TopDownFixedCamera : CameraModeBase
    {
        // ============================================================
        // STATE
        // ============================================================

        private float3 _smoothedFollowPosition;
        private bool _initialized;

        // Edge-pan state (EPIC 15.20 Phase 4a)
        private float3 _panOffset;
        private bool _cameraLocked = true;

        // ============================================================
        // ICAMERAMODE PROPERTIES
        // ============================================================

        public override CameraMode Mode => CameraMode.TopDownFixed;
        public override bool SupportsOrbitRotation => false;
        public override bool UsesCursorAiming => true;

        // ============================================================
        // INITIALIZATION
        // ============================================================

        public override void Initialize(CameraConfig config)
        {
            base.Initialize(config);

            // Top-down typically uses orthographic but can be perspective
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

            // Smooth follow (top-down typically has no deadzone, direct follow)
            _smoothedFollowPosition = SmoothPosition(_smoothedFollowPosition, _targetPosition, deltaTime);

            // EPIC 15.20 Phase 4a: Camera lock toggle (consumed immediately)
            if (PlayerInputState.CameraLockToggle)
            {
                PlayerInputState.CameraLockToggle = false;
                ToggleCameraLock();
            }

            // EPIC 15.20 Phase 4a: Edge-pan when camera is unlocked
            if (!_cameraLocked && CameraOrbitController.Instance != null &&
                CameraOrbitController.Instance.IsEdgePanEnabled)
            {
                float2 screenPos = PlayerInputState.CursorScreenPosition;
                float margin = _config.EdgePanMargin;
                float speed = _config.EdgePanSpeed * deltaTime;
                float3 panDelta = float3.zero;

                if (screenPos.x < margin) panDelta.x = -speed;
                else if (screenPos.x > Screen.width - margin) panDelta.x = speed;
                if (screenPos.y < margin) panDelta.z = -speed;
                else if (screenPos.y > Screen.height - margin) panDelta.z = speed;

                _panOffset += panDelta;

                // Clamp offset magnitude
                float maxOff = _config.EdgePanMaxOffset;
                if (math.length(_panOffset) > maxOff)
                    _panOffset = math.normalize(_panOffset) * maxOff;
            }

            // Apply pan offset to follow position
            float3 effectiveFollowPos = _smoothedFollowPosition + _panOffset;

            // Apply zoom to height
            float baseHeight = _config.TopDownHeight;
            float zoomMultiplier = math.lerp(0.5f, 1.5f, _currentZoom);
            float height = baseHeight * zoomMultiplier;

            // Camera position: directly above character (with pan offset)
            float3 cameraPosition = effectiveFollowPos + new float3(0f, height, 0f);

            // Calculate rotation based on angle (90° = straight down)
            // Slight angle offset toward +Z for visual depth
            float pitchRad = math.radians(_config.TopDownAngle);

            // For 90° (straight down), rotation is -90° pitch
            // For 85°, there's a slight tilt
            float3 lookOffset = new float3(0f, 0f, 0f);
            if (_config.TopDownAngle < 90f)
            {
                // Add slight forward offset for angled view
                float tiltAmount = (90f - _config.TopDownAngle) / 90f;
                float offsetDistance = height * tiltAmount * 0.1f;
                lookOffset = new float3(0f, 0f, offsetDistance);
                cameraPosition -= new float3(0f, 0f, offsetDistance * 2f); // Pull camera back
            }

            // Look at character position (with pan offset)
            float3 lookAtPoint = effectiveFollowPos + lookOffset;
            float3 lookDirection = math.normalize(lookAtPoint - cameraPosition);

            quaternion cameraRotation;
            if (math.lengthsq(lookDirection) > 0.0001f)
            {
                // Use a consistent up vector for top-down
                float3 up = new float3(0f, 0f, 1f); // Z is "up" in screen space for top-down
                if (_config.TopDownAngle >= 89f)
                {
                    // Nearly straight down - use world forward as up
                    up = new float3(0f, 0f, 1f);
                }
                cameraRotation = quaternion.LookRotation(lookDirection, up);
            }
            else
            {
                cameraRotation = quaternion.Euler(math.radians(-90f), 0f, 0f);
            }

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
            // Top-down camera doesn't respond to rotation input
        }

        public override float3 TransformMovementInput(float2 input)
        {
            // Top-down uses world-aligned movement (no transformation)
            return CameraInputUtility.TransformTopDownInput(input);
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
            // For top-down, aim plane is the ground plane at character height
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
            return CameraInputUtility.CalculateAimDirection(_targetPosition, cursorWorld, flattenY: true);
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

        // ============================================================
        // CAMERA LOCK (EPIC 15.20 Phase 4a)
        // ============================================================

        /// <summary>Whether the camera is locked to the player (no edge-pan offset).</summary>
        public bool IsCameraLocked => _cameraLocked;

        /// <summary>Toggle camera lock on/off. When locked, pan offset resets to zero.</summary>
        public void ToggleCameraLock()
        {
            _cameraLocked = !_cameraLocked;
            if (_cameraLocked) _panOffset = float3.zero;
        }

        /// <summary>Set camera lock state explicitly.</summary>
        public void SetCameraLocked(bool locked)
        {
            _cameraLocked = locked;
            if (locked) _panOffset = float3.zero;
        }
    }
}
