using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.CameraSystem
{
    /// <summary>
    /// Interface for camera mode implementations.
    /// MonoBehaviour-based for polymorphism and easy mode switching.
    /// Acts as an adapter between DIG's game logic and camera implementations.
    ///
    /// Implementations:
    /// - ThirdPersonFollowCamera: DIG-style behind-character orbit camera
    /// - IsometricFixedCamera: ARPG-style fixed angle camera
    /// - TopDownFixedCamera: Straight-down camera variant
    /// - IsometricRotatableCamera: Isometric with Q/E rotation
    ///
    /// Third-party integrations (Cinemachine, ProCamera2D, etc.) can implement
    /// this interface to delegate camera control while maintaining compatibility
    /// with DIG's input and targeting systems.
    /// </summary>
    public interface ICameraMode
    {
        /// <summary>
        /// Current camera mode.
        /// </summary>
        CameraMode Mode { get; }

        /// <summary>
        /// Initialize the camera with configuration.
        /// Called when camera mode is activated or config changes.
        /// </summary>
        /// <param name="config">Camera configuration settings.</param>
        void Initialize(CameraConfig config);

        /// <summary>
        /// Per-frame update for camera position/rotation.
        /// Called from LateUpdate for smooth following.
        /// </summary>
        /// <param name="deltaTime">Time since last frame.</param>
        void UpdateCamera(float deltaTime);

        /// <summary>
        /// Get the current camera transform (position and rotation).
        /// </summary>
        /// <returns>Transform of the active camera.</returns>
        Transform GetCameraTransform();

        /// <summary>
        /// Get the aim plane for cursor projection.
        /// For third-person: plane perpendicular to camera forward.
        /// For isometric/top-down: ground plane (Y=0 or character height).
        /// </summary>
        /// <returns>Plane used for cursor-to-world projection.</returns>
        Plane GetAimPlane();

        /// <summary>
        /// Transform raw WASD input to world-space movement direction.
        /// For third-person: camera-relative movement.
        /// For isometric: transformed based on camera rotation (e.g., 45° diamond).
        /// </summary>
        /// <param name="input">Raw input (x = horizontal, y = vertical) in range [-1, 1].</param>
        /// <returns>World-space movement direction (normalized if non-zero).</returns>
        float3 TransformMovementInput(float2 input);

        /// <summary>
        /// Transform cursor screen position to world-space aim point.
        /// For third-person: raycast from camera center or cursor.
        /// For isometric: project cursor to ground plane.
        /// </summary>
        /// <param name="cursorScreenPos">Cursor position in screen coordinates.</param>
        /// <returns>World-space aim point.</returns>
        float3 TransformAimInput(float2 cursorScreenPos);

        /// <summary>
        /// Set the entity/transform the camera should follow.
        /// </summary>
        /// <param name="entity">Entity to follow.</param>
        /// <param name="visualTransform">Optional transform for visual representation (ragdoll, etc.).</param>
        void SetTarget(Entity entity, Transform visualTransform = null);

        /// <summary>
        /// Set the camera zoom level.
        /// For third-person: distance from character.
        /// For isometric: height/orthographic size.
        /// </summary>
        /// <param name="zoomLevel">Zoom level (0 = min zoom/closest, 1 = max zoom/farthest).</param>
        void SetZoom(float zoomLevel);

        /// <summary>
        /// Get the current zoom level.
        /// </summary>
        /// <returns>Current zoom level (0-1 range).</returns>
        float GetZoom();

        /// <summary>
        /// Trigger screen shake effect.
        /// </summary>
        /// <param name="intensity">Shake amplitude in world units.</param>
        /// <param name="duration">How long the shake lasts in seconds.</param>
        void Shake(float intensity, float duration);

        /// <summary>
        /// Handle orbit/rotation input (for third-person mode).
        /// Isometric cameras may ignore this or use it for Q/E rotation.
        /// </summary>
        /// <param name="rotationInput">Rotation delta (x = yaw, y = pitch).</param>
        void HandleRotationInput(float2 rotationInput);

        /// <summary>
        /// Check if this camera mode supports free orbit rotation.
        /// </summary>
        /// <returns>True if mouse orbit is supported.</returns>
        bool SupportsOrbitRotation { get; }

        /// <summary>
        /// Check if this camera mode uses cursor for aiming.
        /// </summary>
        /// <returns>True if cursor position determines aim direction.</returns>
        bool UsesCursorAiming { get; }
    }
}
