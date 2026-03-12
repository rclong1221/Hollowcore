using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.CameraSystem.Implementations
{
    /// <summary>
    /// Third-person follow camera implementation for DIG.
    /// WoW-style orbit camera that follows behind the character.
    /// Features:
    /// - Mouse orbit control (yaw/pitch)
    /// - Collision avoidance
    /// - Zoom via scroll wheel
    /// - FPS mode when distance = 0
    /// - Camera-relative movement input
    /// </summary>
    public class ThirdPersonFollowCamera : CameraModeBase
    {
        // ============================================================
        // STATE
        // ============================================================

        private float _yaw;
        private float _pitch;
        private float _currentDistance;

        // ============================================================
        // ICAMERAMODE PROPERTIES
        // ============================================================

        public override CameraMode Mode => CameraMode.ThirdPersonFollow;
        public override bool SupportsOrbitRotation => true;
        public override bool UsesCursorAiming => false;

        /// <summary>
        /// Current camera yaw angle in degrees.
        /// </summary>
        public float Yaw => _yaw;

        /// <summary>
        /// Current camera pitch angle in degrees.
        /// </summary>
        public float Pitch => _pitch;

        // ============================================================
        // INITIALIZATION
        // ============================================================

        public override void Initialize(CameraConfig config)
        {
            base.Initialize(config);

            // Set initial angles from config
            _yaw = config.DefaultYaw;
            _pitch = config.DefaultPitch;
            _currentDistance = GetCurrentDistance();
        }

        // ============================================================
        // CAMERA UPDATE
        // ============================================================

        public override void UpdateCamera(float deltaTime)
        {
            if (_camera == null || _config == null)
                return;

            UpdateTargetPosition();

            // Calculate desired distance from zoom
            float targetDistance = GetCurrentDistance();
            _currentDistance = Mathf.Lerp(_currentDistance, targetDistance, deltaTime * 10f);

            // Get pivot position (character position + offset)
            float3 pivotPosition = _targetPosition + (float3)_config.FollowOffset;

            // Calculate camera position
            float3 cameraPosition;
            quaternion cameraRotation;

            if (_currentDistance < 0.01f)
            {
                // FPS mode - camera at character head
                cameraPosition = _targetPosition + (float3)_config.FPSOffset;
                cameraRotation = quaternion.Euler(math.radians(_pitch), math.radians(_yaw), 0f);
            }
            else
            {
                // Third-person mode - orbit around pivot
                quaternion orbitRotation = quaternion.Euler(math.radians(_pitch), math.radians(_yaw), 0f);
                float3 orbitDirection = math.mul(orbitRotation, new float3(0f, 0f, -1f));
                cameraPosition = pivotPosition + orbitDirection * _currentDistance;

                // Look at pivot
                float3 lookDirection = math.normalize(pivotPosition - cameraPosition);
                if (math.lengthsq(lookDirection) > 0.0001f)
                {
                    float3 up = new float3(0f, 1f, 0f);
                    float3 right = math.normalize(math.cross(up, lookDirection));
                    up = math.cross(lookDirection, right);
                    cameraRotation = quaternion.LookRotation(lookDirection, up);
                }
                else
                {
                    cameraRotation = orbitRotation;
                }

                // Apply collision avoidance
                if (_config.EnableCollision)
                {
                    cameraPosition = ApplyCollision(pivotPosition, cameraPosition, _config.CollisionLayers, _config.CollisionRadius);
                }
            }

            // Smooth camera movement
            float3 currentPos = _camera.transform.position;
            quaternion currentRot = _camera.transform.rotation;

            float3 smoothedPos = SmoothPosition(currentPos, cameraPosition, deltaTime);
            quaternion smoothedRot = SmoothRotation(currentRot, cameraRotation, deltaTime);

            _camera.transform.SetPositionAndRotation(smoothedPos, smoothedRot);

            // Apply shake after positioning
            ApplyShake(deltaTime);
        }

        // ============================================================
        // INPUT HANDLING
        // ============================================================

        public override void HandleRotationInput(float2 rotationInput)
        {
            if (_config == null)
                return;

            // Apply sensitivity
            float yawDelta = rotationInput.x * _config.OrbitSensitivity;
            float pitchDelta = rotationInput.y * _config.OrbitSensitivity;

            // Update yaw (unlimited rotation)
            _yaw += yawDelta;
            while (_yaw > 360f) _yaw -= 360f;
            while (_yaw < 0f) _yaw += 360f;

            // Update pitch (clamped)
            _pitch -= pitchDelta; // Invert Y for natural feel
            _pitch = Mathf.Clamp(_pitch, -_config.MaxPitchDown, _config.MaxPitchUp);
        }

        public override float3 TransformMovementInput(float2 input)
        {
            return CameraInputUtility.TransformThirdPersonInput(input, _yaw);
        }

        public override float3 TransformAimInput(float2 cursorScreenPos)
        {
            if (_camera == null)
                return new float3(0f, 0f, 1f);

            // For third-person, aim is screen center (camera forward)
            // But we still support cursor aiming if requested
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // Raycast to find aim point
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return (float3)hit.point;
            }

            // Fallback: point far ahead
            return (float3)(ray.origin + ray.direction * 100f);
        }

        public override Plane GetAimPlane()
        {
            if (_camera == null)
                return new Plane(Vector3.up, Vector3.zero);

            // For third-person, aim plane is perpendicular to camera forward
            // But typically we use raycasting instead
            return new Plane(_camera.transform.forward, _targetPosition + (float3)_config.LookAtOffset);
        }

        // ============================================================
        // ZOOM CONTROL
        // ============================================================

        public override void SetZoom(float zoomLevel)
        {
            base.SetZoom(zoomLevel);
            // Distance will be updated in UpdateCamera
        }

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
        /// Set camera angles directly.
        /// </summary>
        public void SetAngles(float yaw, float pitch)
        {
            _yaw = yaw;
            if (_config != null)
            {
                _pitch = Mathf.Clamp(pitch, -_config.MaxPitchDown, _config.MaxPitchUp);
            }
            else
            {
                _pitch = pitch;
            }
        }

        /// <summary>
        /// Snap camera to look at a world position.
        /// </summary>
        public void LookAt(float3 worldPosition)
        {
            float3 direction = worldPosition - _targetPosition;
            direction.y = 0f; // Flatten for yaw calculation

            if (math.lengthsq(direction) > 0.0001f)
            {
                direction = math.normalize(direction);
                _yaw = math.degrees(math.atan2(direction.x, direction.z));
            }
        }
    }
}
