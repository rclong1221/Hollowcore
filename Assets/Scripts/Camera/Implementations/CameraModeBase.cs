using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.CameraSystem.Implementations
{
    /// <summary>
    /// Abstract base class for camera mode implementations.
    /// Provides common functionality shared across all camera modes.
    /// </summary>
    public abstract class CameraModeBase : MonoBehaviour, ICameraMode
    {
        // ============================================================
        // SERIALIZED FIELDS
        // ============================================================

        [Header("Camera Reference")]
        [Tooltip("The Unity Camera to control. If null, uses Camera.main.")]
        [SerializeField] protected Camera _camera;

        // ============================================================
        // PROTECTED STATE
        // ============================================================

        protected CameraConfig _config;

        /// <summary>Runtime camera config, if initialized. Used by death camera to match paradigm.</summary>
        public CameraConfig RuntimeConfig => _config;

        protected Entity _targetEntity;
        protected Transform _targetTransform;
        protected float3 _targetPosition;
        protected float _currentZoom;

        // Shake state
        protected float _shakeAmplitude;
        protected float _shakeDuration;
        protected float _shakeTimer;

        // ============================================================
        // ICAMERAMODE INTERFACE - ABSTRACT
        // ============================================================

        public abstract CameraMode Mode { get; }
        public abstract bool SupportsOrbitRotation { get; }
        public abstract bool UsesCursorAiming { get; }

        public abstract void UpdateCamera(float deltaTime);
        public abstract float3 TransformMovementInput(float2 input);
        public abstract float3 TransformAimInput(float2 cursorScreenPos);
        public abstract Plane GetAimPlane();
        public abstract void HandleRotationInput(float2 rotationInput);

        // ============================================================
        // ICAMERAMODE INTERFACE - COMMON IMPLEMENTATION
        // ============================================================

        public virtual void Initialize(CameraConfig config)
        {
            _config = config;

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    _camera = GetComponent<Camera>();
                }
            }

            if (_camera == null)
            {
                Debug.LogError($"[{GetType().Name}] No Camera found!");
                enabled = false;
                return;
            }

            // Set initial zoom from config
            _currentZoom = config.DefaultZoom;

            // Apply FOV
            if (!config.UseOrthographic)
            {
                _camera.fieldOfView = config.FieldOfView;
            }
        }

        public virtual Transform GetCameraTransform()
        {
            return _camera != null ? _camera.transform : null;
        }

        public virtual void SetTarget(Entity entity, Transform visualTransform = null)
        {
            _targetEntity = entity;
            _targetTransform = visualTransform;
        }

        public virtual void SetZoom(float zoomLevel)
        {
            _currentZoom = Mathf.Clamp01(zoomLevel);
        }

        public virtual float GetZoom()
        {
            return _currentZoom;
        }

        public virtual void Shake(float intensity, float duration)
        {
            if (_config == null) return;

            // Apply shake multiplier from config
            _shakeAmplitude = intensity * _config.ShakeMultiplier;
            _shakeDuration = duration;
            _shakeTimer = 0f;
        }

        // ============================================================
        // PROTECTED HELPERS
        // ============================================================

        /// <summary>
        /// Get the current distance based on zoom level and config.
        /// </summary>
        protected float GetCurrentDistance()
        {
            if (_config == null) return 10f;
            return _config.GetDistanceFromZoom(_currentZoom);
        }

        /// <summary>
        /// Update target position from transform if available.
        /// </summary>
        protected void UpdateTargetPosition()
        {
            if (_targetTransform != null)
            {
                _targetPosition = _targetTransform.position;
            }
        }

        /// <summary>
        /// Apply screen shake effect to camera position.
        /// Call this after setting the base camera position.
        /// </summary>
        protected void ApplyShake(float deltaTime)
        {
            if (_shakeAmplitude <= 0f || _camera == null || _config == null)
                return;

            _shakeTimer += deltaTime;

            // Calculate shake using Perlin noise for smooth motion
            float t = _shakeTimer * _config.ShakeFrequency;
            float seed = Time.time * 0.1f;

            float px = Mathf.PerlinNoise(t + seed, seed + 0.1f) * 2f - 1f;
            float py = Mathf.PerlinNoise(t + seed + 10f, seed + 0.2f) * 2f - 1f;
            float pz = Mathf.PerlinNoise(t + seed + 20f, seed + 0.3f) * 2f - 1f;

            Vector3 jitter = new Vector3(
                px * _shakeAmplitude,
                py * _shakeAmplitude * 0.6f,
                pz * _shakeAmplitude * 0.4f
            );

            _camera.transform.position += jitter;

            // Decay amplitude
            _shakeAmplitude = Mathf.Max(0f, _shakeAmplitude - _config.ShakeDecay * deltaTime);
        }

        /// <summary>
        /// Smoothly interpolate camera position.
        /// </summary>
        protected float3 SmoothPosition(float3 current, float3 target, float deltaTime)
        {
            if (_config == null || _config.FollowSmoothing <= 0f)
                return target;

            float t = math.saturate(deltaTime * _config.FollowSmoothing);
            return math.lerp(current, target, t);
        }

        /// <summary>
        /// Smoothly interpolate camera rotation.
        /// </summary>
        protected quaternion SmoothRotation(quaternion current, quaternion target, float deltaTime)
        {
            if (_config == null || _config.FollowSmoothing <= 0f)
                return target;

            float t = math.saturate(deltaTime * _config.FollowSmoothing);
            return math.slerp(current, target, t);
        }

        /// <summary>
        /// Apply camera position with optional collision detection.
        /// </summary>
        protected float3 ApplyCollision(float3 pivot, float3 desiredPosition, LayerMask collisionMask, float radius)
        {
            Vector3 dir = (Vector3)desiredPosition - (Vector3)pivot;
            float dist = dir.magnitude;

            if (dist < 0.001f)
                return desiredPosition;

            Vector3 rayDir = dir / dist;

            if (Physics.SphereCast(pivot, radius, rayDir, out RaycastHit hit, dist, collisionMask))
            {
                // Move camera to hit point with small offset
                return (float3)(hit.point - rayDir * 0.1f);
            }

            return desiredPosition;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        protected virtual void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    _camera = GetComponent<Camera>();
                }
            }
        }

        protected virtual void LateUpdate()
        {
            // Subclasses should call UpdateCamera in their own LateUpdate
            // or this can be called by a manager
        }
    }
}
