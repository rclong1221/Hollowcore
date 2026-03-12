using UnityEngine;
using Unity.Mathematics;

namespace DIG.CameraSystem
{
    /// <summary>
    /// EPIC 14.9 Phase 5 - Camera Zoom Controller
    /// Handles zoom input (scroll wheel, keys) and smooth zoom transitions.
    ///
    /// Features:
    /// - Scroll wheel zoom
    /// - Keyboard zoom (+ / -)
    /// - Smooth zoom interpolation
    /// - Zoom snap points (optional)
    /// - Integration with CameraModeProvider
    ///
    /// Usage:
    /// - Attach to camera or any GameObject
    /// - Works automatically with active ICameraMode
    /// - Call ZoomIn/ZoomOut/ZoomTo for programmatic control
    /// </summary>
    public class CameraZoomController : MonoBehaviour
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        private static CameraZoomController _instance;

        public static CameraZoomController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CameraZoomController>();
                }
                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        // ============================================================
        // SETTINGS
        // ============================================================

        [Header("Input Settings")]
        [Tooltip("Enable scroll wheel zoom.")]
        [SerializeField] private bool _enableScrollZoom = true;

        [Tooltip("Invert scroll direction.")]
        [SerializeField] private bool _invertScroll = false;

        [Tooltip("Scroll sensitivity multiplier.")]
        [Range(0.1f, 5f)]
        [SerializeField] private float _scrollSensitivity = 1f;

        [Header("Smoothing")]
        [Tooltip("Enable smooth zoom interpolation.")]
        [SerializeField] private bool _enableSmoothing = true;

        [Tooltip("Zoom smoothing speed (higher = faster).")]
        [Range(1f, 30f)]
        [SerializeField] private float _smoothingSpeed = 10f;

        [Header("Snap Points")]
        [Tooltip("Enable zoom snap points.")]
        [SerializeField] private bool _enableSnapPoints = false;

        [Tooltip("Zoom levels to snap to (0-1 values).")]
        [SerializeField] private float[] _snapPoints = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        [Tooltip("Snap threshold - how close to snap point to trigger snap.")]
        [Range(0.01f, 0.1f)]
        [SerializeField] private float _snapThreshold = 0.05f;

        [Header("Limits")]
        [Tooltip("Override minimum zoom (if >= 0, overrides config).")]
        [SerializeField] private float _overrideMinZoom = -1f;

        [Tooltip("Override maximum zoom (if >= 0, overrides config).")]
        [SerializeField] private float _overrideMaxZoom = -1f;

        // ============================================================
        // STATE
        // ============================================================

        private float _targetZoom;
        private float _currentZoom;
        private bool _isZooming;

        // Keyboard input state
        private bool _zoomInPressed;
        private bool _zoomOutPressed;

        // ============================================================
        // PROPERTIES
        // ============================================================

        /// <summary>
        /// Current zoom level (0-1).
        /// </summary>
        public float CurrentZoom => _currentZoom;

        /// <summary>
        /// Target zoom level (0-1).
        /// </summary>
        public float TargetZoom => _targetZoom;

        /// <summary>
        /// Whether zoom is currently animating.
        /// </summary>
        public bool IsZooming => _isZooming;

        /// <summary>
        /// Enable/disable scroll zoom at runtime.
        /// </summary>
        public bool EnableScrollZoom
        {
            get => _enableScrollZoom;
            set => _enableScrollZoom = value;
        }

        /// <summary>
        /// Scroll sensitivity multiplier.
        /// </summary>
        public float ScrollSensitivity
        {
            get => _scrollSensitivity;
            set => _scrollSensitivity = Mathf.Max(0.01f, value);
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            // Initialize zoom from active camera
            if (CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null)
            {
                _currentZoom = CameraModeProvider.Instance.ActiveCamera.GetZoom();
                _targetZoom = _currentZoom;
            }
        }

        private void Update()
        {
            ProcessInput();
            UpdateZoom();
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
        /// Zoom in by a specified amount.
        /// </summary>
        /// <param name="amount">Amount to zoom in (positive = closer).</param>
        public void ZoomIn(float amount = 0.1f)
        {
            SetTargetZoom(_targetZoom - amount);
        }

        /// <summary>
        /// Zoom out by a specified amount.
        /// </summary>
        /// <param name="amount">Amount to zoom out (positive = farther).</param>
        public void ZoomOut(float amount = 0.1f)
        {
            SetTargetZoom(_targetZoom + amount);
        }

        /// <summary>
        /// Set zoom to a specific level.
        /// </summary>
        /// <param name="zoomLevel">Zoom level (0 = closest, 1 = farthest).</param>
        /// <param name="instant">If true, skip smoothing.</param>
        public void ZoomTo(float zoomLevel, bool instant = false)
        {
            SetTargetZoom(zoomLevel);

            if (instant)
            {
                _currentZoom = _targetZoom;
                ApplyZoom(_currentZoom);
                _isZooming = false;
            }
        }

        /// <summary>
        /// Zoom to closest point.
        /// </summary>
        public void ZoomToMin(bool instant = false)
        {
            ZoomTo(GetMinZoom(), instant);
        }

        /// <summary>
        /// Zoom to farthest point.
        /// </summary>
        public void ZoomToMax(bool instant = false)
        {
            ZoomTo(GetMaxZoom(), instant);
        }

        /// <summary>
        /// Reset zoom to default level from config.
        /// </summary>
        /// <param name="instant">If true, skip smoothing.</param>
        public void ResetZoom(bool instant = false)
        {
            if (CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null)
            {
                // Get default from config if available
                float defaultZoom = 0.5f;
                ZoomTo(defaultZoom, instant);
            }
        }

        /// <summary>
        /// Cycle to next snap point.
        /// </summary>
        public void CycleZoomUp()
        {
            if (!_enableSnapPoints || _snapPoints.Length == 0) return;

            // Find next snap point above current
            float nextSnap = GetMaxZoom();
            foreach (float snap in _snapPoints)
            {
                if (snap > _currentZoom + 0.01f && snap < nextSnap)
                {
                    nextSnap = snap;
                }
            }
            ZoomTo(nextSnap);
        }

        /// <summary>
        /// Cycle to previous snap point.
        /// </summary>
        public void CycleZoomDown()
        {
            if (!_enableSnapPoints || _snapPoints.Length == 0) return;

            // Find next snap point below current
            float prevSnap = GetMinZoom();
            foreach (float snap in _snapPoints)
            {
                if (snap < _currentZoom - 0.01f && snap > prevSnap)
                {
                    prevSnap = snap;
                }
            }
            ZoomTo(prevSnap);
        }

        // ============================================================
        // PRIVATE METHODS
        // ============================================================

        private void ProcessInput()
        {
            if (!CameraModeProvider.HasInstance || CameraModeProvider.Instance.ActiveCamera == null)
                return;

            // Scroll wheel input
            // EPIC 15.21: Use PlayerInputState for zoom input
            // The Input System handles both scroll wheel and keyboard keys (via 1D Axis binding)
            if (global::Player.Systems.PlayerInputState.ZoomDelta != 0f)
            {
               float zoomDelta = global::Player.Systems.PlayerInputState.ZoomDelta;
               
               // Invert logic is likely handled in Input System, but keeping preference check
               if (_enableScrollZoom)
               {
                   float speed = GetZoomSpeed();
                   if (_invertScroll) zoomDelta = -zoomDelta;
                   
                   // Note: PlayerInputReader already scales scroll input, but we apply local sensitivity
                   SetTargetZoom(_targetZoom - zoomDelta * speed * _scrollSensitivity * 0.1f);
               }
            }
            // Keyboard zoom is now handled via the same ZoomDelta from Input System

        }

        private void UpdateZoom()
        {
            if (!CameraModeProvider.HasInstance || CameraModeProvider.Instance.ActiveCamera == null)
                return;

            // Check if we need to update
            float diff = Mathf.Abs(_targetZoom - _currentZoom);
            if (diff < 0.001f)
            {
                if (_isZooming)
                {
                    _currentZoom = _targetZoom;
                    ApplyZoom(_currentZoom);
                    _isZooming = false;
                }
                return;
            }

            _isZooming = true;

            // Interpolate
            if (_enableSmoothing)
            {
                _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * _smoothingSpeed);
            }
            else
            {
                _currentZoom = _targetZoom;
            }

            // Apply snap points
            if (_enableSnapPoints && !_zoomInPressed && !_zoomOutPressed)
            {
                float snappedZoom = GetNearestSnapPoint(_currentZoom);
                if (Mathf.Abs(snappedZoom - _currentZoom) < _snapThreshold)
                {
                    _currentZoom = snappedZoom;
                    _targetZoom = snappedZoom;
                }
            }

            ApplyZoom(_currentZoom);
        }

        private void SetTargetZoom(float zoom)
        {
            _targetZoom = Mathf.Clamp(zoom, GetMinZoom(), GetMaxZoom());
        }

        private void ApplyZoom(float zoom)
        {
            if (CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null)
            {
                CameraModeProvider.Instance.ActiveCamera.SetZoom(zoom);
            }
        }

        private float GetZoomSpeed()
        {
            // Could read from CameraConfig if available
            return 1f;
        }

        private float GetMinZoom()
        {
            if (_overrideMinZoom >= 0f)
            {
                return _overrideMinZoom;
            }
            return 0f;
        }

        private float GetMaxZoom()
        {
            if (_overrideMaxZoom >= 0f)
            {
                return _overrideMaxZoom;
            }
            return 1f;
        }

        private float GetNearestSnapPoint(float zoom)
        {
            if (_snapPoints == null || _snapPoints.Length == 0)
                return zoom;

            float nearest = zoom;
            float minDist = float.MaxValue;

            foreach (float snap in _snapPoints)
            {
                float dist = Mathf.Abs(snap - zoom);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = snap;
                }
            }

            return nearest;
        }

        // ============================================================
        // STATIC CONVENIENCE METHODS
        // ============================================================

        /// <summary>
        /// Zoom in on the singleton instance.
        /// </summary>
        public static void DoZoomIn(float amount = 0.1f)
        {
            if (HasInstance)
            {
                Instance.ZoomIn(amount);
            }
            else if (CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null)
            {
                var cam = CameraModeProvider.Instance.ActiveCamera;
                cam.SetZoom(Mathf.Max(0f, cam.GetZoom() - amount));
            }
        }

        /// <summary>
        /// Zoom out on the singleton instance.
        /// </summary>
        public static void DoZoomOut(float amount = 0.1f)
        {
            if (HasInstance)
            {
                Instance.ZoomOut(amount);
            }
            else if (CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null)
            {
                var cam = CameraModeProvider.Instance.ActiveCamera;
                cam.SetZoom(Mathf.Min(1f, cam.GetZoom() + amount));
            }
        }

        /// <summary>
        /// Set zoom level on the singleton instance.
        /// </summary>
        public static void DoZoomTo(float level, bool instant = false)
        {
            if (HasInstance)
            {
                Instance.ZoomTo(level, instant);
            }
            else if (CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null)
            {
                CameraModeProvider.Instance.ActiveCamera.SetZoom(Mathf.Clamp01(level));
            }
        }
    }
}
