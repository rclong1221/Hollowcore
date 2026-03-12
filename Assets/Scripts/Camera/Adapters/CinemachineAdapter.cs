using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#if CINEMACHINE_ENABLED
using Cinemachine;
#endif

namespace DIG.CameraSystem.Adapters
{
    /// <summary>
    /// EPIC 14.9 Phase 5 - Cinemachine Adapter
    /// Implements ICameraMode by wrapping Cinemachine virtual cameras.
    ///
    /// This adapter pattern allows DIG to use Cinemachine for camera control
    /// while maintaining compatibility with the rest of the camera system.
    ///
    /// Setup:
    /// 1. Add CINEMACHINE_ENABLED to Project Settings > Scripting Define Symbols
    /// 2. Add this component to a GameObject
    /// 3. Assign your Cinemachine virtual cameras
    /// 4. Register with CameraModeProvider
    ///
    /// Features:
    /// - Wraps Cinemachine virtual camera
    /// - Translates ICameraMode calls to Cinemachine API
    /// - Supports camera blending via CinemachineBrain
    /// - Compatible with all DIG camera system features
    /// </summary>
    public class CinemachineAdapter : MonoBehaviour, ICameraMode
    {
        // ============================================================
        // SERIALIZED FIELDS
        // ============================================================

        [Header("Configuration")]
        [Tooltip("Camera configuration asset.")]
        [SerializeField] private CameraConfig _config;

        [Tooltip("Camera mode this adapter represents.")]
        [SerializeField] private CameraMode _mode = CameraMode.ThirdPersonFollow;

#if CINEMACHINE_ENABLED
        [Header("Cinemachine References")]
        [Tooltip("The Cinemachine virtual camera to control.")]
        [SerializeField] private CinemachineVirtualCamera _virtualCamera;

        [Tooltip("Optional: Brain for camera blending (auto-found if null).")]
        [SerializeField] private CinemachineBrain _brain;
#endif

        [Header("Fallback Settings")]
        [Tooltip("The Unity Camera to use (auto-found if null).")]
        [SerializeField] private Camera _unityCamera;

        // ============================================================
        // STATE
        // ============================================================

        private Entity _targetEntity;
        private Transform _targetTransform;
        private float _currentZoom;

        // Shake state (Cinemachine has its own impulse system, but we provide fallback)
        private float _shakeAmplitude;
        private float _shakeTimer;

        // Input transformation cache
        private float _cachedCameraYaw;

        // ============================================================
        // ICAMERAMODE IMPLEMENTATION
        // ============================================================

        public CameraMode Mode => _mode;

        public bool SupportsOrbitRotation
        {
            get
            {
                switch (_mode)
                {
                    case CameraMode.ThirdPersonFollow:
                        return true;
                    case CameraMode.IsometricRotatable:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool UsesCursorAiming
        {
            get
            {
                switch (_mode)
                {
                    case CameraMode.IsometricFixed:
                    case CameraMode.TopDownFixed:
                    case CameraMode.IsometricRotatable:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public void Initialize(CameraConfig config)
        {
            _config = config;

            if (_unityCamera == null)
            {
                _unityCamera = Camera.main;
            }

#if CINEMACHINE_ENABLED
            if (_virtualCamera == null)
            {
                _virtualCamera = FindFirstObjectByType<CinemachineVirtualCamera>();
            }

            if (_brain == null && _unityCamera != null)
            {
                _brain = _unityCamera.GetComponent<CinemachineBrain>();
            }

            // Apply config to virtual camera
            if (_virtualCamera != null && config != null)
            {
                // Set FOV
                if (!config.UseOrthographic)
                {
                    _virtualCamera.m_Lens.FieldOfView = config.FieldOfView;
                }
                else
                {
                    _virtualCamera.m_Lens.OrthographicSize = config.OrthoSize;
                }
            }
#endif

            _currentZoom = config?.DefaultZoom ?? 0.5f;
        }

        public void UpdateCamera(float deltaTime)
        {
            // Cinemachine handles most of the camera update via the Brain
            // We just need to update our state and apply any shake

            UpdateCameraYaw();

#if CINEMACHINE_ENABLED
            // Update virtual camera distance based on zoom
            if (_virtualCamera != null && _config != null)
            {
                var body = _virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
                if (body != null)
                {
                    float distance = _config.GetDistanceFromZoom(_currentZoom);
                    var offset = body.m_FollowOffset;
                    offset.z = -distance;
                    body.m_FollowOffset = offset;
                }

                var framingBody = _virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
                if (framingBody != null)
                {
                    float distance = _config.GetDistanceFromZoom(_currentZoom);
                    framingBody.m_CameraDistance = distance;
                }
            }
#endif

            // Apply shake fallback (if not using Cinemachine Impulse)
            ApplyShakeFallback(deltaTime);
        }

        public Transform GetCameraTransform()
        {
            if (_unityCamera != null)
            {
                return _unityCamera.transform;
            }

#if CINEMACHINE_ENABLED
            if (_brain != null)
            {
                return _brain.transform;
            }
#endif

            return null;
        }

        public void SetTarget(Entity entity, Transform visualTransform = null)
        {
            _targetEntity = entity;
            _targetTransform = visualTransform;

#if CINEMACHINE_ENABLED
            if (_virtualCamera != null && visualTransform != null)
            {
                _virtualCamera.Follow = visualTransform;
                _virtualCamera.LookAt = visualTransform;
            }
#endif
        }

        public void SetZoom(float zoomLevel)
        {
            _currentZoom = Mathf.Clamp01(zoomLevel);
        }

        public float GetZoom()
        {
            return _currentZoom;
        }

        public void Shake(float intensity, float duration)
        {
#if CINEMACHINE_ENABLED
            // Try to use Cinemachine Impulse if available
            var impulse = _virtualCamera?.GetComponent<CinemachineImpulseSource>();
            if (impulse != null)
            {
                impulse.GenerateImpulse(intensity);
                return;
            }
#endif

            // Fallback to manual shake
            if (_config != null)
            {
                _shakeAmplitude = intensity * (_config.ShakeMultiplier);
                _shakeTimer = duration;
            }
        }

        public float3 TransformMovementInput(float2 input)
        {
            if (math.lengthsq(input) < 0.0001f)
                return float3.zero;

            // Use camera yaw for transformation
            float yaw = _cachedCameraYaw;

            switch (_mode)
            {
                case CameraMode.ThirdPersonFollow:
                    return CameraInputUtility.TransformThirdPersonInput(input, yaw);

                case CameraMode.IsometricFixed:
                    float isoRot = _config?.IsometricRotation ?? 45f;
                    return CameraInputUtility.TransformIsometricInput(input, isoRot);

                case CameraMode.TopDownFixed:
                    return CameraInputUtility.TransformTopDownInput(input);

                case CameraMode.IsometricRotatable:
                    return CameraInputUtility.TransformIsometricInput(input, yaw);

                default:
                    return new float3(input.x, 0f, input.y);
            }
        }

        public float3 TransformAimInput(float2 cursorScreenPos)
        {
            if (_unityCamera == null)
                return float3.zero;

            switch (_mode)
            {
                case CameraMode.ThirdPersonFollow:
                    // Third-person: aim at screen center
                    return _unityCamera.transform.forward;

                case CameraMode.IsometricFixed:
                case CameraMode.TopDownFixed:
                case CameraMode.IsometricRotatable:
                    // Isometric/top-down: project cursor to ground
                    var method = _config?.CursorProjection ?? CursorProjectionMethod.GroundPlane;
                    float height = _targetTransform != null ? _targetTransform.position.y : 0f;
                    return CameraInputUtility.ProjectCursor(cursorScreenPos, _unityCamera, method, _config, height);

                default:
                    return float3.zero;
            }
        }

        public Plane GetAimPlane()
        {
            float height = _targetTransform != null ? _targetTransform.position.y : 0f;
            if (_config != null)
            {
                height = _config.CursorProjectionHeight;
            }
            return new Plane(Vector3.up, new Vector3(0f, height, 0f));
        }

        public void HandleRotationInput(float2 rotationInput)
        {
#if CINEMACHINE_ENABLED
            // For third-person, we might control a FreeLook or POV component
            if (_mode == CameraMode.ThirdPersonFollow && _virtualCamera != null)
            {
                var pov = _virtualCamera.GetCinemachineComponent<CinemachinePOV>();
                if (pov != null)
                {
                    // POV handles input automatically via input axes
                    // We could override here if needed
                }
            }
#endif
        }

        // ============================================================
        // PRIVATE METHODS
        // ============================================================

        private void UpdateCameraYaw()
        {
            var camTransform = GetCameraTransform();
            if (camTransform != null)
            {
                _cachedCameraYaw = camTransform.eulerAngles.y;
            }
        }

        private void ApplyShakeFallback(float deltaTime)
        {
            if (_shakeAmplitude <= 0f || _unityCamera == null)
                return;

            // Decay
            _shakeTimer -= deltaTime;
            if (_shakeTimer <= 0f)
            {
                _shakeAmplitude = 0f;
                return;
            }

            // Apply shake offset
            float freq = _config?.ShakeFrequency ?? 15f;
            float t = Time.time * freq;

            float px = Mathf.PerlinNoise(t, 0f) * 2f - 1f;
            float py = Mathf.PerlinNoise(t + 10f, 0f) * 2f - 1f;

            Vector3 offset = new Vector3(px, py, 0f) * _shakeAmplitude;
            _unityCamera.transform.position += offset;

            // Decay amplitude
            float decay = _config?.ShakeDecay ?? 8f;
            _shakeAmplitude = Mathf.Max(0f, _shakeAmplitude - decay * deltaTime);
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            if (_unityCamera == null)
            {
                _unityCamera = Camera.main;
            }

#if CINEMACHINE_ENABLED
            if (_virtualCamera == null)
            {
                _virtualCamera = GetComponent<CinemachineVirtualCamera>();
            }
#endif
        }

        private void LateUpdate()
        {
            // Can be called manually or by CameraSystemAuthoring
        }

        // ============================================================
        // PUBLIC API - CINEMACHINE SPECIFIC
        // ============================================================

#if CINEMACHINE_ENABLED
        /// <summary>
        /// Get the wrapped virtual camera.
        /// </summary>
        public CinemachineVirtualCamera VirtualCamera => _virtualCamera;

        /// <summary>
        /// Set the virtual camera to wrap.
        /// </summary>
        public void SetVirtualCamera(CinemachineVirtualCamera vcam)
        {
            _virtualCamera = vcam;
            if (vcam != null && _targetTransform != null)
            {
                vcam.Follow = _targetTransform;
                vcam.LookAt = _targetTransform;
            }
        }

        /// <summary>
        /// Switch to a different virtual camera with blending.
        /// </summary>
        public void SwitchToCamera(CinemachineVirtualCamera newCamera, float blendTime = 0.5f)
        {
            if (_brain == null) return;

            // Lower priority of current, raise priority of new
            if (_virtualCamera != null)
            {
                _virtualCamera.Priority = 10;
            }

            if (newCamera != null)
            {
                newCamera.Priority = 20;
                _virtualCamera = newCamera;

                if (_targetTransform != null)
                {
                    newCamera.Follow = _targetTransform;
                    newCamera.LookAt = _targetTransform;
                }
            }

            // Brain will handle the blend automatically
        }

        /// <summary>
        /// Trigger a Cinemachine impulse.
        /// </summary>
        public void TriggerImpulse(float force)
        {
            var impulse = _virtualCamera?.GetComponent<CinemachineImpulseSource>();
            if (impulse != null)
            {
                impulse.GenerateImpulse(force);
            }
        }
#endif

        /// <summary>
        /// Set the camera mode type.
        /// </summary>
        public void SetMode(CameraMode mode)
        {
            _mode = mode;
        }
    }
}
