using Unity.Mathematics;
using UnityEngine;

namespace DIG.CameraSystem
{
    /// <summary>
    /// Bridges camera system with player input for movement transformation.
    /// Converts raw WASD input to world-space movement direction based on camera mode.
    ///
    /// Integration with PlayerInputSystem:
    /// - PlayerInputSystem samples raw Horizontal/Vertical input
    /// - CameraInputBridge transforms to world-space direction
    /// - CharacterControllerSystem uses transformed direction for movement
    ///
    /// Usage:
    /// 1. Add CameraInputBridge to your game controller or player
    /// 2. Call TransformMovementInput() with raw WASD input
    /// 3. Use result for character movement
    ///
    /// For automatic integration, the system can read camera rotation from
    /// PlayerCameraSettings component and apply appropriate transformation.
    /// </summary>
    public class CameraInputBridge : MonoBehaviour
    {
        // ============================================================
        // SINGLETON (optional)
        // ============================================================

        private static CameraInputBridge _instance;

        /// <summary>
        /// Singleton instance. May be null if not using singleton pattern.
        /// </summary>
        public static CameraInputBridge Instance => _instance;

        /// <summary>
        /// Check if instance exists.
        /// </summary>
        public static bool HasInstance => _instance != null;

        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Configuration")]
        [Tooltip("Use camera mode from CameraModeProvider. If false, uses manual mode setting.")]
        [SerializeField] private bool _useProviderCamera = true;

        [Tooltip("Camera mode to use if not using CameraModeProvider.")]
        [SerializeField] private CameraMode _manualMode = CameraMode.ThirdPersonFollow;

        [Tooltip("Camera rotation (yaw) for manual mode.")]
        [SerializeField] private float _manualRotation = 0f;

        // ============================================================
        // STATE
        // ============================================================

        private float _lastCameraRotation;

        /// <summary>
        /// Current effective camera mode.
        /// </summary>
        public CameraMode CurrentMode
        {
            get
            {
                if (_useProviderCamera && CameraModeProvider.HasInstance)
                {
                    return CameraModeProvider.Instance.CurrentMode;
                }
                return _manualMode;
            }
        }

        /// <summary>
        /// Current camera rotation for movement transformation.
        /// </summary>
        public float CurrentRotation
        {
            get
            {
                if (_useProviderCamera && CameraModeProvider.HasInstance)
                {
                    return CameraModeProvider.Instance.GetCameraRotation();
                }
                return _manualRotation;
            }
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
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ============================================================
        // MOVEMENT TRANSFORMATION
        // ============================================================

        /// <summary>
        /// Transform raw WASD input to world-space movement direction.
        /// Uses current camera mode and rotation.
        /// </summary>
        /// <param name="rawInput">Raw input (x = horizontal/strafe, y = vertical/forward).</param>
        /// <returns>World-space movement direction (normalized if non-zero).</returns>
        public float3 TransformMovementInput(float2 rawInput)
        {
            _lastCameraRotation = CurrentRotation;
            return CameraInputUtility.TransformMovementInput(rawInput, CurrentMode, _lastCameraRotation);
        }

        /// <summary>
        /// Transform raw WASD input with explicit camera rotation.
        /// Use this when you have camera rotation from another source (e.g., PlayerCameraSettings).
        /// </summary>
        /// <param name="rawInput">Raw input (x = horizontal/strafe, y = vertical/forward).</param>
        /// <param name="cameraYaw">Camera yaw rotation in degrees.</param>
        /// <returns>World-space movement direction (normalized if non-zero).</returns>
        public float3 TransformMovementInput(float2 rawInput, float cameraYaw)
        {
            _lastCameraRotation = cameraYaw;
            return CameraInputUtility.TransformMovementInput(rawInput, CurrentMode, cameraYaw);
        }

        /// <summary>
        /// Transform raw WASD input with explicit mode and rotation.
        /// </summary>
        /// <param name="rawInput">Raw input (x = horizontal/strafe, y = vertical/forward).</param>
        /// <param name="mode">Camera mode to use for transformation.</param>
        /// <param name="cameraYaw">Camera yaw rotation in degrees.</param>
        /// <returns>World-space movement direction (normalized if non-zero).</returns>
        public float3 TransformMovementInput(float2 rawInput, CameraMode mode, float cameraYaw)
        {
            _lastCameraRotation = cameraYaw;
            return CameraInputUtility.TransformMovementInput(rawInput, mode, cameraYaw);
        }

        // ============================================================
        // STATIC CONVENIENCE METHODS
        // ============================================================

        /// <summary>
        /// Transform movement input using singleton instance.
        /// Falls back to third-person transformation if no instance.
        /// </summary>
        public static float3 Transform(float2 rawInput)
        {
            if (HasInstance)
            {
                return Instance.TransformMovementInput(rawInput);
            }

            // Fallback: use camera provider directly
            if (CameraModeProvider.HasInstance)
            {
                var mode = CameraModeProvider.Instance.CurrentMode;
                var rotation = CameraModeProvider.Instance.GetCameraRotation();
                return CameraInputUtility.TransformMovementInput(rawInput, mode, rotation);
            }

            // Final fallback: world-aligned (no transformation)
            return CameraInputUtility.TransformTopDownInput(rawInput);
        }

        /// <summary>
        /// Transform movement input using camera yaw from PlayerCameraSettings or similar.
        /// </summary>
        public static float3 Transform(float2 rawInput, float cameraYaw, bool isCameraYawValid)
        {
            if (!isCameraYawValid)
            {
                return Transform(rawInput);
            }

            if (HasInstance)
            {
                return Instance.TransformMovementInput(rawInput, cameraYaw);
            }

            // Fallback: use provider mode with explicit yaw
            CameraMode mode = CameraMode.ThirdPersonFollow;
            if (CameraModeProvider.HasInstance)
            {
                mode = CameraModeProvider.Instance.CurrentMode;
            }

            return CameraInputUtility.TransformMovementInput(rawInput, mode, cameraYaw);
        }

        // ============================================================
        // MANUAL CONFIGURATION
        // ============================================================

        /// <summary>
        /// Set manual camera mode (when not using CameraModeProvider).
        /// </summary>
        public void SetManualMode(CameraMode mode)
        {
            _manualMode = mode;
        }

        /// <summary>
        /// Set manual camera rotation (when not using CameraModeProvider).
        /// </summary>
        public void SetManualRotation(float yaw)
        {
            _manualRotation = yaw;
        }

        /// <summary>
        /// Enable/disable CameraModeProvider integration.
        /// </summary>
        public void SetUseProviderCamera(bool useProvider)
        {
            _useProviderCamera = useProvider;
        }

        // ============================================================
        // DEBUG
        // ============================================================

        /// <summary>
        /// Get debug info about current state.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"CameraInputBridge: Mode={CurrentMode}, Rotation={CurrentRotation:F1}°, " +
                   $"UseProvider={_useProviderCamera}, HasProvider={CameraModeProvider.HasInstance}";
        }
    }
}
