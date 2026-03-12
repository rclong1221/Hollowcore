using UnityEngine;
using Unity.Mathematics;

namespace DIG.CameraSystem
{
    /// <summary>
    /// EPIC 14.9 Phase 5 - Camera Transition Manager
    /// Handles smooth transitions between camera modes and configurations.
    ///
    /// Usage:
    /// - Call TransitionToMode() to smoothly switch camera modes
    /// - Call TransitionToConfig() to smoothly change camera settings
    /// - Works with CameraModeProvider to manage active camera
    /// </summary>
    public class CameraTransitionManager : MonoBehaviour
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        private static CameraTransitionManager _instance;

        public static CameraTransitionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CameraTransitionManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("CameraTransitionManager");
                        _instance = go.AddComponent<CameraTransitionManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        // ============================================================
        // SETTINGS
        // ============================================================

        [Header("Transition Settings")]
        [Tooltip("Default duration for camera transitions in seconds.")]
        [Range(0.1f, 3f)]
        [SerializeField] private float _defaultTransitionDuration = 0.5f;

        [Tooltip("Easing curve for transitions.")]
        [SerializeField] private AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("If true, transitions affect position. If false, only rotation/zoom.")]
        [SerializeField] private bool _transitionPosition = true;

        [Tooltip("If true, transitions affect rotation.")]
        [SerializeField] private bool _transitionRotation = true;

        [Tooltip("If true, transitions affect FOV/zoom.")]
        [SerializeField] private bool _transitionFOV = true;

        // ============================================================
        // STATE
        // ============================================================

        private bool _isTransitioning;
        private float _transitionProgress;
        private float _transitionDuration;

        // Transition start/end state
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _startFOV;
        private float _startZoom;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _targetFOV;
        private float _targetZoom;

        // Transition camera references
        private ICameraMode _fromCamera;
        private ICameraMode _toCamera;
        private Camera _unityCamera;

        // Callbacks
        private System.Action _onTransitionComplete;

        // ============================================================
        // PROPERTIES
        // ============================================================

        /// <summary>
        /// Whether a transition is currently in progress.
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// Current transition progress (0-1).
        /// </summary>
        public float TransitionProgress => _transitionProgress;

        /// <summary>
        /// Default transition duration.
        /// </summary>
        public float DefaultTransitionDuration
        {
            get => _defaultTransitionDuration;
            set => _defaultTransitionDuration = Mathf.Max(0.01f, value);
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Ensure default curve exists
            if (_transitionCurve == null || _transitionCurve.keys.Length == 0)
            {
                _transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }

        private void LateUpdate()
        {
            if (_isTransitioning)
            {
                UpdateTransition();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Transition to a new camera mode.
        /// </summary>
        /// <param name="newCamera">The new camera mode to transition to.</param>
        /// <param name="duration">Transition duration (uses default if <= 0).</param>
        /// <param name="onComplete">Optional callback when transition completes.</param>
        public void TransitionToCamera(ICameraMode newCamera, float duration = -1f, System.Action onComplete = null, bool force = false)
        {
            if (newCamera == null)
            {
                Debug.LogWarning("[CameraTransitionManager] Cannot transition to null camera");
                return;
            }

            // Get current camera
            _fromCamera = CameraModeProvider.HasInstance ? CameraModeProvider.Instance.ActiveCamera : null;
            _toCamera = newCamera;

            // Skip transition if same camera or no from camera
            if (_fromCamera == null || _fromCamera == _toCamera)
            {
                if (CameraModeProvider.HasInstance)
                {
                    CameraModeProvider.Instance.SetActiveCamera(newCamera, force);
                }
                onComplete?.Invoke();
                return;
            }

            // Get Unity camera
            var cameraTransform = _fromCamera.GetCameraTransform();
            _unityCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;

            if (_unityCamera == null)
            {
                Debug.LogWarning("[CameraTransitionManager] No Unity Camera found for transition");
                if (CameraModeProvider.HasInstance)
                {
                    CameraModeProvider.Instance.SetActiveCamera(newCamera, force);
                }
                onComplete?.Invoke();
                return;
            }

            // Store start state
            _startPosition = _unityCamera.transform.position;
            _startRotation = _unityCamera.transform.rotation;
            _startFOV = _unityCamera.fieldOfView;
            _startZoom = _fromCamera.GetZoom();

            // Store target state (will be calculated each frame as new camera updates)
            _targetZoom = _toCamera.GetZoom();

            // Setup transition
            _transitionDuration = duration > 0f ? duration : _defaultTransitionDuration;
            _transitionProgress = 0f;
            _isTransitioning = true;
            _onTransitionComplete = onComplete;

            // Register new camera with provider (but we'll blend position/rotation manually)
            if (CameraModeProvider.HasInstance)
            {
                CameraModeProvider.Instance.SetActiveCamera(newCamera, force);
            }
        }

        /// <summary>
        /// Transition to a new camera configuration on the current camera.
        /// </summary>
        /// <param name="newConfig">The new configuration to apply.</param>
        /// <param name="duration">Transition duration (uses default if <= 0).</param>
        /// <param name="onComplete">Optional callback when transition completes.</param>
        public void TransitionToConfig(CameraConfig newConfig, float duration = -1f, System.Action onComplete = null)
        {
            if (newConfig == null)
            {
                Debug.LogWarning("[CameraTransitionManager] Cannot transition to null config");
                return;
            }

            if (!CameraModeProvider.HasInstance || CameraModeProvider.Instance.ActiveCamera == null)
            {
                Debug.LogWarning("[CameraTransitionManager] No active camera for config transition");
                return;
            }

            var activeCamera = CameraModeProvider.Instance.ActiveCamera;

            // Store current state
            var cameraTransform = activeCamera.GetCameraTransform();
            _unityCamera = cameraTransform?.GetComponent<Camera>();

            if (_unityCamera == null)
            {
                activeCamera.Initialize(newConfig);
                onComplete?.Invoke();
                return;
            }

            _startPosition = _unityCamera.transform.position;
            _startRotation = _unityCamera.transform.rotation;
            _startFOV = _unityCamera.fieldOfView;
            _startZoom = activeCamera.GetZoom();

            // Target is new config's values
            _targetFOV = newConfig.FieldOfView;
            _targetZoom = newConfig.DefaultZoom;

            // Apply the new config
            activeCamera.Initialize(newConfig);

            // Setup transition
            _transitionDuration = duration > 0f ? duration : _defaultTransitionDuration;
            _transitionProgress = 0f;
            _isTransitioning = true;
            _fromCamera = null; // Config transition, not mode transition
            _toCamera = activeCamera;
            _onTransitionComplete = onComplete;
        }

        /// <summary>
        /// Transition zoom level smoothly.
        /// </summary>
        /// <param name="targetZoom">Target zoom level (0-1).</param>
        /// <param name="duration">Duration of zoom transition.</param>
        public void TransitionZoom(float targetZoom, float duration = -1f)
        {
            if (!CameraModeProvider.HasInstance || CameraModeProvider.Instance.ActiveCamera == null)
                return;

            var camera = CameraModeProvider.Instance.ActiveCamera;
            _startZoom = camera.GetZoom();
            _targetZoom = Mathf.Clamp01(targetZoom);

            if (_unityCamera == null)
            {
                var cameraTransform = camera.GetCameraTransform();
                _unityCamera = cameraTransform?.GetComponent<Camera>();
            }

            if (_unityCamera != null)
            {
                _startPosition = _unityCamera.transform.position;
                _startRotation = _unityCamera.transform.rotation;
                _startFOV = _unityCamera.fieldOfView;
            }

            _transitionDuration = duration > 0f ? duration : _defaultTransitionDuration * 0.5f;
            _transitionProgress = 0f;
            _isTransitioning = true;
            _fromCamera = null;
            _toCamera = camera;
        }

        /// <summary>
        /// Cancel the current transition and snap to end state.
        /// </summary>
        public void CancelTransition()
        {
            if (!_isTransitioning) return;

            _isTransitioning = false;
            _transitionProgress = 1f;

            // Apply final state
            if (_toCamera != null && _unityCamera != null)
            {
                _toCamera.SetZoom(_targetZoom);
                _toCamera.UpdateCamera(0f);
            }

            var callback = _onTransitionComplete;
            _onTransitionComplete = null;
            callback?.Invoke();
        }

        /// <summary>
        /// Skip transition animation and immediately apply end state.
        /// </summary>
        public void SkipTransition()
        {
            CancelTransition();
        }

        // ============================================================
        // PRIVATE METHODS
        // ============================================================

        private void UpdateTransition()
        {
            if (_unityCamera == null || _toCamera == null)
            {
                CancelTransition();
                return;
            }

            // Update progress
            _transitionProgress += Time.deltaTime / _transitionDuration;

            if (_transitionProgress >= 1f)
            {
                // Transition complete
                _transitionProgress = 1f;
                _isTransitioning = false;

                // Final state
                _toCamera.SetZoom(_targetZoom);
                _toCamera.UpdateCamera(Time.deltaTime);

                var callback = _onTransitionComplete;
                _onTransitionComplete = null;
                callback?.Invoke();
                return;
            }

            // Get eased progress
            float t = _transitionCurve.Evaluate(_transitionProgress);

            // Update destination camera to get target state
            _toCamera.UpdateCamera(Time.deltaTime);
            var targetTransform = _toCamera.GetCameraTransform();

            if (targetTransform != null)
            {
                _targetPosition = targetTransform.position;
                _targetRotation = targetTransform.rotation;

                var targetCamera = targetTransform.GetComponent<Camera>();
                if (targetCamera != null)
                {
                    _targetFOV = targetCamera.fieldOfView;
                }
            }

            // Interpolate values
            if (_transitionPosition)
            {
                _unityCamera.transform.position = Vector3.Lerp(_startPosition, _targetPosition, t);
            }

            if (_transitionRotation)
            {
                _unityCamera.transform.rotation = Quaternion.Slerp(_startRotation, _targetRotation, t);
            }

            if (_transitionFOV && !_unityCamera.orthographic)
            {
                _unityCamera.fieldOfView = Mathf.Lerp(_startFOV, _targetFOV, t);
            }

            // Interpolate zoom
            float currentZoom = Mathf.Lerp(_startZoom, _targetZoom, t);
            _toCamera.SetZoom(currentZoom);
        }

        // ============================================================
        // UTILITY METHODS
        // ============================================================

        /// <summary>
        /// Set the transition easing curve.
        /// </summary>
        public void SetTransitionCurve(AnimationCurve curve)
        {
            _transitionCurve = curve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        /// <summary>
        /// Create a smooth step curve for transitions.
        /// </summary>
        public static AnimationCurve CreateSmoothStepCurve()
        {
            return AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        /// <summary>
        /// Create a linear curve for transitions.
        /// </summary>
        public static AnimationCurve CreateLinearCurve()
        {
            return AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        /// <summary>
        /// Create an ease-out curve (fast start, slow end).
        /// </summary>
        public static AnimationCurve CreateEaseOutCurve()
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f, 0f, 2f, 2f));
            curve.AddKey(new Keyframe(1f, 1f, 0f, 0f));
            return curve;
        }

        /// <summary>
        /// Create an ease-in curve (slow start, fast end).
        /// </summary>
        public static AnimationCurve CreateEaseInCurve()
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f, 0f, 0f, 0f));
            curve.AddKey(new Keyframe(1f, 1f, 2f, 2f));
            return curve;
        }
    }
}
