using UnityEngine;
using DIG.DeathCamera;

namespace DIG.CameraSystem
{
    /// <summary>
    /// Provides centralized access to the active camera mode.
    /// Singleton pattern for easy access from targeting, input, and other systems.
    ///
    /// Usage:
    /// - Set the active camera mode via SetActiveCamera()
    /// - Access from anywhere via CameraModeProvider.Instance.ActiveCamera
    /// - Systems can query camera for input transformation and cursor projection
    /// </summary>
    public class CameraModeProvider : MonoBehaviour
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        private static CameraModeProvider _instance;

        /// <summary>
        /// Singleton instance. Creates one if it doesn't exist.
        /// </summary>
        public static CameraModeProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CameraModeProvider>();
                    if (_instance == null)
                    {
                        var go = new GameObject("CameraModeProvider");
                        _instance = go.AddComponent<CameraModeProvider>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Check if an instance exists without creating one.
        /// </summary>
        public static bool HasInstance => _instance != null;

        // ============================================================
        // STATE
        // ============================================================

        [Header("Active Camera")]
        [Tooltip("The currently active camera mode implementation.")]
        [SerializeField] private MonoBehaviour _activeCameraComponent;

        private ICameraMode _activeCamera;

        /// <summary>
        /// The currently active camera mode.
        /// May be null if no camera is set.
        /// </summary>
        public ICameraMode ActiveCamera => _activeCamera;

        /// <summary>
        /// Current camera mode enum (or ThirdPersonFollow if no camera set).
        /// </summary>
        public CameraMode CurrentMode => _activeCamera?.Mode ?? CameraMode.ThirdPersonFollow;

        /// <summary>
        /// Whether an active camera is available.
        /// </summary>
        public bool HasActiveCamera => _activeCamera != null;

        /// <summary>
        /// Whether the active camera uses cursor for aiming (isometric/top-down).
        /// </summary>
        public bool UsesCursorAiming => _activeCamera?.UsesCursorAiming ?? false;

        /// <summary>
        /// Whether the active camera supports orbit rotation (third-person).
        /// </summary>
        public bool SupportsOrbitRotation => _activeCamera?.SupportsOrbitRotation ?? true;

        // ============================================================
        // EVENTS
        // ============================================================

        /// <summary>
        /// Fired when the active camera mode changes.
        /// Passes (previousMode, newMode) for paradigm compatibility checks.
        /// </summary>
        public event System.Action<CameraMode, CameraMode> OnCameraChanged;

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

            // Try to get camera from serialized component
            if (_activeCameraComponent != null)
            {
                _activeCamera = _activeCameraComponent as ICameraMode;
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
        /// Set the active camera mode.
        /// When CameraAuthorityGate is overridden, only callers that pass force=true
        /// can change the active camera (death cam, cutscene, etc.).
        /// </summary>
        /// <param name="camera">Camera mode implementation.</param>
        /// <param name="force">If true, bypass the authority gate check.</param>
        public void SetActiveCamera(ICameraMode camera, bool force = false)
        {
            if (!force && CameraAuthorityGate.IsOverridden)
            {
                Debug.LogWarning($"[DCam] CameraModeProvider.SetActiveCamera BLOCKED — '{CameraAuthorityGate.CurrentOwner}' holds authority. Caller tried: {camera?.GetType().Name ?? "null"}");
                return;
            }

            var previousMode = CurrentMode;
            _activeCamera = camera;
            _activeCameraComponent = camera as MonoBehaviour;
            var newMode = CurrentMode;

            if (previousMode != newMode)
            {
                OnCameraChanged?.Invoke(previousMode, newMode);
            }
        }

        /// <summary>
        /// Set the active camera from a MonoBehaviour that implements ICameraMode.
        /// When CameraAuthorityGate is overridden, only callers that pass force=true
        /// can change the active camera.
        /// </summary>
        /// <param name="cameraComponent">MonoBehaviour implementing ICameraMode.</param>
        /// <param name="force">If true, bypass the authority gate check.</param>
        public void SetActiveCamera(MonoBehaviour cameraComponent, bool force = false)
        {
            if (!force && CameraAuthorityGate.IsOverridden)
            {
                Debug.LogWarning($"[DCam] CameraModeProvider.SetActiveCamera BLOCKED — '{CameraAuthorityGate.CurrentOwner}' holds authority. Caller tried: {cameraComponent?.GetType().Name ?? "null"}");
                return;
            }

            if (cameraComponent is ICameraMode cameraMode)
            {
                var previousMode = CurrentMode;
                _activeCamera = cameraMode;
                _activeCameraComponent = cameraComponent;
                var newMode = CurrentMode;

                if (previousMode != newMode)
                {
                    OnCameraChanged?.Invoke(previousMode, newMode);
                }
            }
            else
            {
                Debug.LogWarning($"[CameraModeProvider] {cameraComponent.GetType().Name} does not implement ICameraMode");
            }
        }

        /// <summary>
        /// Clear the active camera.
        /// </summary>
        public void ClearActiveCamera()
        {
            var previousMode = CurrentMode;
            _activeCamera = null;
            _activeCameraComponent = null;
            var newMode = CurrentMode; // Will be ThirdPersonFollow (default)
            
            if (previousMode != newMode)
            {
                OnCameraChanged?.Invoke(previousMode, newMode);
            }
        }

        /// <summary>
        /// Try to auto-detect and set the active camera from the scene.
        /// Searches for any MonoBehaviour implementing ICameraMode.
        /// </summary>
        /// <returns>True if a camera was found and set.</returns>
        public bool AutoDetectCamera()
        {
            // Try to find any ICameraMode in the scene
            var cameras = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var component in cameras)
            {
                if (component is ICameraMode cameraMode)
                {
                    SetActiveCamera(cameraMode);
                    Debug.Log($"[CameraModeProvider] Auto-detected camera: {component.GetType().Name}");
                    return true;
                }
            }

            Debug.LogWarning("[CameraModeProvider] No ICameraMode implementation found in scene");
            return false;
        }

        // ============================================================
        // CONVENIENCE METHODS
        // ============================================================

        /// <summary>
        /// Get the camera's Transform.
        /// </summary>
        public Transform GetCameraTransform()
        {
            return _activeCamera?.GetCameraTransform();
        }

        /// <summary>
        /// Get the Unity Camera component.
        /// </summary>
        public Camera GetCamera()
        {
            var transform = _activeCamera?.GetCameraTransform();
            return transform != null ? transform.GetComponent<Camera>() : Camera.main;
        }

        /// <summary>
        /// Get the current camera rotation (yaw) for input transformation.
        /// For third-person, this is the orbit yaw.
        /// For isometric, this is the fixed isometric rotation.
        /// </summary>
        public float GetCameraRotation()
        {
            if (_activeCamera == null)
                return 0f;

            var transform = _activeCamera.GetCameraTransform();
            if (transform == null)
                return 0f;

            // Extract yaw from camera rotation
            return transform.eulerAngles.y;
        }
    }
}
