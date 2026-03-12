using UnityEngine;

namespace DIG.CameraSystem
{
    /// <summary>
    /// Data-driven configuration for camera behavior.
    /// Assign to player prefab to configure camera mode and parameters.
    /// Supports ThirdPerson (DIG), Isometric (ARPG), and TopDown modes.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "DIG/Camera/Camera Config")]
    public class CameraConfig : ScriptableObject
    {
        // ============================================================
        // MODE SELECTION
        // ============================================================

        [Header("Mode Selection")]
        [Tooltip("Which camera mode to use.")]
        public CameraMode CameraMode = CameraMode.ThirdPersonFollow;

        // ============================================================
        // COMMON SETTINGS
        // ============================================================

        [Header("Common Settings")]
        [Tooltip("How quickly camera follows the target (higher = snappier).")]
        [Range(0f, 50f)]
        public float FollowSmoothing = 10f;

        [Tooltip("Minimum zoom/distance from character.")]
        public float ZoomMin = 2f;

        [Tooltip("Maximum zoom/distance from character.")]
        public float ZoomMax = 20f;

        [Tooltip("Starting zoom level (0 = min, 1 = max).")]
        [Range(0f, 1f)]
        public float DefaultZoom = 0.3f;

        [Tooltip("Scroll wheel sensitivity for zoom.")]
        [Range(0.1f, 5f)]
        public float ZoomSpeed = 1f;

        [Tooltip("Field of view in degrees (perspective cameras only).")]
        [Range(30f, 120f)]
        public float FieldOfView = 60f;

        // ============================================================
        // THIRD-PERSON SETTINGS
        // ============================================================

        [Header("Third-Person Settings")]
        [Tooltip("Offset from character pivot to camera orbit center.")]
        public Vector3 FollowOffset = new Vector3(0f, 1.6f, 0f);

        [Tooltip("Offset for where camera looks at (relative to character).")]
        public Vector3 LookAtOffset = new Vector3(0f, 1.6f, 0f);

        [Tooltip("Mouse look sensitivity for orbit control.")]
        [Range(0.01f, 1f)]
        public float OrbitSensitivity = 0.15f;

        [Tooltip("Maximum upward pitch angle (degrees).")]
        [Range(0f, 89f)]
        public float MaxPitchUp = 80f;

        [Tooltip("Maximum downward pitch angle (degrees).")]
        [Range(0f, 89f)]
        public float MaxPitchDown = 60f;

        [Tooltip("Default yaw angle (degrees, 0 = behind character).")]
        public float DefaultYaw = 0f;

        [Tooltip("Default pitch angle (degrees, 0 = horizontal).")]
        [Range(-89f, 89f)]
        public float DefaultPitch = 25f;

        [Tooltip("Layer mask for camera collision detection.")]
        public LayerMask CollisionLayers = ~0;

        [Tooltip("Radius for collision spherecast.")]
        [Range(0.1f, 1f)]
        public float CollisionRadius = 0.25f;

        [Tooltip("Enable camera collision to avoid clipping through walls.")]
        public bool EnableCollision = true;

        [Tooltip("Offset for first-person mode (when distance = 0).")]
        public Vector3 FPSOffset = new Vector3(0f, 1.7f, 0f);

        // ============================================================
        // ISOMETRIC SETTINGS
        // ============================================================

        [Header("Isometric Settings")]
        [Tooltip("Camera pitch angle for isometric view (degrees from horizontal).")]
        [Range(30f, 75f)]
        public float IsometricAngle = 45f;

        [Tooltip("Camera yaw/rotation for isometric view (45 = diamond, 0 = straight).")]
        [Range(0f, 360f)]
        public float IsometricRotation = 45f;

        [Tooltip("Base height above character for isometric camera.")]
        public float IsometricHeight = 15f;

        [Tooltip("Use orthographic projection for true isometric look.")]
        public bool UseOrthographic = false;

        [Tooltip("Orthographic camera size (if using orthographic).")]
        [Range(5f, 50f)]
        public float OrthoSize = 10f;

        [Tooltip("Deadzone radius before camera starts following (world units).")]
        [Range(0f, 5f)]
        public float FollowDeadzone = 0.5f;

        [Tooltip("Method for projecting cursor to world space.")]
        public CursorProjectionMethod CursorProjection = CursorProjectionMethod.GroundPlane;

        [Tooltip("Fixed Y height for cursor projection (if using FixedHeight method).")]
        public float CursorProjectionHeight = 0f;

        [Tooltip("Layer mask for terrain raycasting (if using TerrainHit method).")]
        public LayerMask TerrainLayers = ~0;

        // ============================================================
        // TOP-DOWN SETTINGS
        // ============================================================

        [Header("Top-Down Settings")]
        [Tooltip("Camera pitch angle for top-down view (90 = straight down).")]
        [Range(60f, 90f)]
        public float TopDownAngle = 85f;

        [Tooltip("Height above character for top-down camera.")]
        public float TopDownHeight = 20f;

        // ============================================================
        // EDGE PAN (MOBA) — EPIC 15.20 Phase 4a
        // ============================================================

        [Header("Edge Pan (MOBA)")]
        [Tooltip("Speed of edge-pan camera movement in units/second.")]
        [Range(1f, 50f)]
        public float EdgePanSpeed = 15f;

        [Tooltip("Screen margin in pixels that triggers edge-pan.")]
        [Range(5f, 100f)]
        public float EdgePanMargin = 30f;

        [Tooltip("Maximum distance camera can pan from the player.")]
        [Range(5f, 50f)]
        public float EdgePanMaxOffset = 25f;

        // ============================================================
        // ROTATABLE ISOMETRIC SETTINGS
        // ============================================================

        [Header("Rotatable Isometric Settings")]
        [Tooltip("Degrees to rotate per Q/E press.")]
        [Range(15f, 90f)]
        public float RotationIncrement = 45f;

        [Tooltip("Duration of rotation animation (seconds).")]
        [Range(0.1f, 1f)]
        public float RotationDuration = 0.25f;

        // ============================================================
        // SCREEN SHAKE SETTINGS
        // ============================================================

        [Header("Screen Shake")]
        [Tooltip("Global multiplier for screen shake effects.")]
        [Range(0f, 2f)]
        public float ShakeMultiplier = 1f;

        [Tooltip("Shake decay rate (amplitude reduction per second).")]
        [Range(0.1f, 20f)]
        public float ShakeDecay = 8f;

        [Tooltip("Shake oscillation frequency (oscillations per second).")]
        [Range(1f, 50f)]
        public float ShakeFrequency = 15f;

        // ============================================================
        // STATIC PRESETS
        // ============================================================

        /// <summary>
        /// Default config for DIG (third-person shooter with orbit camera).
        /// WoW-style defaults: 8m distance, 25° pitch, mouse orbit control.
        /// </summary>
        public static CameraConfig CreateDIGPreset()
        {
            var config = CreateInstance<CameraConfig>();
            config.name = "CameraConfig_DIG";

            // Mode
            config.CameraMode = CameraMode.ThirdPersonFollow;

            // Common
            config.FollowSmoothing = 10f;
            config.ZoomMin = 0f; // Allow FPS mode
            config.ZoomMax = 20f;
            config.DefaultZoom = 0.4f; // ~8m distance
            config.ZoomSpeed = 1f;
            config.FieldOfView = 60f;

            // Third-person specific
            config.FollowOffset = new Vector3(0f, 1.6f, 0f);
            config.LookAtOffset = new Vector3(0f, 1.6f, 0f);
            config.OrbitSensitivity = 0.15f;
            config.MaxPitchUp = 80f;
            config.MaxPitchDown = 60f;
            config.DefaultYaw = 0f;
            config.DefaultPitch = 25f;
            config.EnableCollision = true;
            config.CollisionRadius = 0.25f;
            config.FPSOffset = new Vector3(0f, 1.7f, 0f);

            return config;
        }

        /// <summary>
        /// Default config for ARPG (isometric camera like Diablo/Hades).
        /// 45° angle, 45° rotation (diamond view), cursor aiming.
        /// </summary>
        public static CameraConfig CreateARPGPreset()
        {
            var config = CreateInstance<CameraConfig>();
            config.name = "CameraConfig_ARPG";

            // Mode
            config.CameraMode = CameraMode.IsometricFixed;

            // Common
            config.FollowSmoothing = 8f;
            config.ZoomMin = 10f;
            config.ZoomMax = 25f;
            config.DefaultZoom = 0.3f;
            config.ZoomSpeed = 1.5f;
            config.FieldOfView = 60f;

            // Isometric specific
            config.IsometricAngle = 50f;
            config.IsometricRotation = 45f;
            config.IsometricHeight = 15f;
            config.UseOrthographic = false;
            config.OrthoSize = 12f;
            config.FollowDeadzone = 0.5f;
            config.CursorProjection = CursorProjectionMethod.GroundPlane;
            config.CursorProjectionHeight = 0f;

            return config;
        }

        /// <summary>
        /// Config for top-down view (straight down or near-vertical).
        /// Good for twin-stick shooters or certain roguelikes.
        /// </summary>
        public static CameraConfig CreateTopDownPreset()
        {
            var config = CreateInstance<CameraConfig>();
            config.name = "CameraConfig_TopDown";

            // Mode
            config.CameraMode = CameraMode.TopDownFixed;

            // Common
            config.FollowSmoothing = 10f;
            config.ZoomMin = 8f;
            config.ZoomMax = 30f;
            config.DefaultZoom = 0.4f;
            config.ZoomSpeed = 1f;
            config.FieldOfView = 60f;

            // Top-down specific
            config.TopDownAngle = 85f;
            config.TopDownHeight = 20f;
            config.CursorProjection = CursorProjectionMethod.GroundPlane;

            return config;
        }

        /// <summary>
        /// Config for rotatable isometric view.
        /// Allows Q/E to rotate camera in increments.
        /// </summary>
        public static CameraConfig CreateRotatableIsometricPreset()
        {
            var config = CreateInstance<CameraConfig>();
            config.name = "CameraConfig_RotatableIso";

            // Mode
            config.CameraMode = CameraMode.IsometricRotatable;

            // Common
            config.FollowSmoothing = 8f;
            config.ZoomMin = 10f;
            config.ZoomMax = 25f;
            config.DefaultZoom = 0.3f;
            config.ZoomSpeed = 1.5f;
            config.FieldOfView = 60f;

            // Isometric specific
            config.IsometricAngle = 50f;
            config.IsometricRotation = 45f;
            config.IsometricHeight = 15f;
            config.UseOrthographic = false;

            // Rotation specific
            config.RotationIncrement = 45f;
            config.RotationDuration = 0.25f;

            return config;
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        /// <summary>
        /// Get the actual camera distance from a zoom level (0-1).
        /// </summary>
        public float GetDistanceFromZoom(float zoomLevel)
        {
            return Mathf.Lerp(ZoomMin, ZoomMax, zoomLevel);
        }

        /// <summary>
        /// Get the zoom level (0-1) from an actual distance.
        /// </summary>
        public float GetZoomFromDistance(float distance)
        {
            if (ZoomMax <= ZoomMin) return 0f;
            return Mathf.Clamp01((distance - ZoomMin) / (ZoomMax - ZoomMin));
        }
    }
}
