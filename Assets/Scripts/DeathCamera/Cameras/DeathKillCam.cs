using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.CameraSystem;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Kill cam camera mode. Paradigm-aware: adapts to the gameplay camera mode.
    ///
    /// TPS mode: orbits a fixed world position (the kill location) with configurable radius,
    /// height, and speed. Implements ICameraMode for CameraModeProvider integration.
    ///
    /// Isometric/TopDown mode: fixed-angle view from above with zoom-in effect (no orbit spin).
    ///
    /// Includes a built-in ease-in transition from the previous camera position so
    /// the cut to kill cam feels cinematic even without CameraTransitionManager.
    /// </summary>
    public class DeathKillCam : MonoBehaviour, ICameraMode
    {
        // Paradigm-aware mode (set by SetGameplayMode)
        private CameraMode _reportedMode = CameraMode.ThirdPersonFollow;
        public CameraMode Mode => _reportedMode;

        private float3 _targetPosition;
        private float _orbitRadius = 5f;
        private float _orbitHeight = 3f;
        private float _orbitSpeed = 30f;
        private float _orbitAngle;
        private float _fov = 60f;

        // Built-in ease-in transition
        private bool _transitioning;
        private float _transitionDuration;
        private float _transitionElapsed;
        private Vector3 _transitionFromPos;
        private Quaternion _transitionFromRot;

        // Paradigm state
        private bool _isFixedAngle; // true for Iso/TopDown
        private float _isoAngle;
        private float _isoRotation;

        public bool SupportsOrbitRotation => !_isFixedAngle;
        public bool UsesCursorAiming => _isFixedAngle;

        /// <summary>
        /// Set the paradigm for this kill cam based on the gameplay camera mode.
        /// Call before SetKillPosition.
        /// </summary>
        public void SetGameplayMode(CameraMode mode, CameraConfig gameplayConfig, DeathCameraConfigSO deathConfig)
        {
            bool isIso = mode == CameraMode.IsometricFixed || mode == CameraMode.IsometricRotatable;
            bool isTop = mode == CameraMode.TopDownFixed;
            _isFixedAngle = isIso || isTop;

            if (isIso)
            {
                _reportedMode = mode;
                _isoAngle = gameplayConfig != null ? gameplayConfig.IsometricAngle : deathConfig.IsometricAngle;
                _isoRotation = gameplayConfig != null ? gameplayConfig.IsometricRotation : deathConfig.IsometricRotation;
            }
            else if (isTop)
            {
                _reportedMode = CameraMode.TopDownFixed;
                _isoAngle = gameplayConfig != null ? gameplayConfig.TopDownAngle : deathConfig.TopDownAngle;
                _isoRotation = 0f;
            }
            else
            {
                _reportedMode = CameraMode.ThirdPersonFollow;
            }
        }

        public void SetKillPosition(float3 position, float radius, float height, float speed, float fov)
        {
            _targetPosition = position;
            _orbitRadius = radius;
            _orbitHeight = height;
            _orbitSpeed = speed;
            _fov = fov;
            _orbitAngle = 0f;

            // Immediately compute initial position so the very first frame reads a valid transform
            ApplyOrbit();
        }

        /// <summary>
        /// Start a built-in ease-in transition from the given camera pose.
        /// Call AFTER SetKillPosition so the orbit target is already valid.
        /// Immediately positions the camera at the "from" pose so the very first
        /// frame reads the gameplay camera position (not the orbit snap).
        /// </summary>
        public void SetTransitionFrom(Vector3 fromPos, Quaternion fromRot, float duration)
        {
            if (duration <= 0f) return;
            _transitioning = true;
            _transitionDuration = duration;
            _transitionElapsed = 0f;
            _transitionFromPos = fromPos;
            _transitionFromRot = fromRot;

            // Override transform to "from" pose so first frame isn't at the orbit position
            transform.SetPositionAndRotation(fromPos, fromRot);
        }

        public void Initialize(CameraConfig config) { }

        public void UpdateCamera(float deltaTime)
        {
            // Only advance orbit angle for TPS mode (isometric stays fixed)
            if (!_isFixedAngle)
                _orbitAngle += _orbitSpeed * deltaTime;

            ApplyOrbit();

            // If transitioning, blend from the stored "from" pose toward the orbit pose
            if (_transitioning)
            {
                _transitionElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
                // Smoothstep easing
                float t = progress * progress * (3f - 2f * progress);

                transform.position = Vector3.Lerp(_transitionFromPos, transform.position, t);
                transform.rotation = Quaternion.Slerp(_transitionFromRot, transform.rotation, t);

                if (progress >= 1f)
                    _transitioning = false;
            }
        }

        private void ApplyOrbit()
        {
            if (_isFixedAngle)
            {
                // Fixed-angle: position above target at isometric/top-down angle (no orbit spin)
                float pitchRad = math.radians(_isoAngle);
                float yawRad = math.radians(_isoRotation);
                float horizontalDist = _orbitHeight / math.tan(pitchRad);

                float3 offset = new float3(
                    -math.sin(yawRad) * horizontalDist,
                    _orbitHeight,
                    -math.cos(yawRad) * horizontalDist
                );

                transform.position = (Vector3)(_targetPosition + offset);
                transform.rotation = Quaternion.Euler(_isoAngle, _isoRotation, 0f);
            }
            else
            {
                // TPS: orbit around kill position
                float3 offset = new float3(
                    math.sin(math.radians(_orbitAngle)) * _orbitRadius,
                    _orbitHeight,
                    math.cos(math.radians(_orbitAngle)) * _orbitRadius
                );

                transform.position = (Vector3)(_targetPosition + offset);
                transform.LookAt((Vector3)_targetPosition);
            }
        }

        public Transform GetCameraTransform() => transform;

        public Plane GetAimPlane()
        {
            return new Plane(Vector3.up, (Vector3)_targetPosition);
        }

        public float3 TransformMovementInput(float2 input) => float3.zero;

        public float3 TransformAimInput(float2 cursorScreenPos) => _targetPosition;

        public void SetTarget(Entity entity, Transform visualTransform = null) { }

        public void SetOrbitRadius(float radius) => _orbitRadius = radius;
        public void SetOrbitHeight(float height) => _orbitHeight = height;

        public void SetZoom(float zoomLevel) { }
        public float GetZoom() => 0.5f;

        public void Shake(float intensity, float duration) { }

        public void HandleRotationInput(float2 rotationInput)
        {
            if (_isFixedAngle) return; // No rotation for fixed-angle paradigms
            _orbitAngle += rotationInput.x * 2f;
        }
    }
}
