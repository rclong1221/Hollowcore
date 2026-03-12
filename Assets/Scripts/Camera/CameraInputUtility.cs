using Unity.Mathematics;
using UnityEngine;

namespace DIG.CameraSystem
{
    /// <summary>
    /// Utility class for camera-related input transformations.
    /// Provides methods to transform WASD input and cursor position
    /// based on camera mode and rotation.
    /// </summary>
    public static class CameraInputUtility
    {
        // ============================================================
        // MOVEMENT INPUT TRANSFORMATION
        // ============================================================

        /// <summary>
        /// Transform raw WASD input to world-space direction for third-person camera.
        /// Movement is relative to camera's horizontal forward direction.
        /// </summary>
        /// <param name="input">Raw input (x = horizontal/strafe, y = vertical/forward).</param>
        /// <param name="cameraYaw">Camera yaw angle in degrees.</param>
        /// <returns>World-space movement direction (normalized if non-zero).</returns>
        public static float3 TransformThirdPersonInput(float2 input, float cameraYaw)
        {
            if (math.lengthsq(input) < 0.0001f)
                return float3.zero;

            // Convert yaw to radians
            float yawRad = math.radians(cameraYaw);

            // Camera forward/right on XZ plane
            float3 forward = new float3(math.sin(yawRad), 0f, math.cos(yawRad));
            float3 right = new float3(math.cos(yawRad), 0f, -math.sin(yawRad));

            // Combine input with camera directions
            float3 worldDir = (forward * input.y) + (right * input.x);

            // Normalize if non-zero
            float len = math.length(worldDir);
            if (len > 0.0001f)
                worldDir /= len;

            return worldDir;
        }

        /// <summary>
        /// Transform raw WASD input to world-space direction for isometric camera.
        /// For 45° rotated isometric (diamond view):
        ///   W (up)    → (+X, 0, +Z) - up-right on screen
        ///   S (down)  → (-X, 0, -Z) - down-left on screen
        ///   A (left)  → (-X, 0, +Z) - up-left on screen
        ///   D (right) → (+X, 0, -Z) - down-right on screen
        /// </summary>
        /// <param name="input">Raw input (x = horizontal, y = vertical).</param>
        /// <param name="isometricRotation">Camera yaw rotation in degrees (45 = diamond).</param>
        /// <returns>World-space movement direction (normalized if non-zero).</returns>
        public static float3 TransformIsometricInput(float2 input, float isometricRotation)
        {
            if (math.lengthsq(input) < 0.0001f)
                return float3.zero;

            // Convert rotation to radians
            float rotRad = math.radians(isometricRotation);

            // Base isometric directions (for 0° rotation):
            // Screen up = world +Z, screen right = world +X
            // For 45° rotation, we rotate these directions

            float cosRot = math.cos(rotRad);
            float sinRot = math.sin(rotRad);

            // Screen up direction in world space
            float3 screenUp = new float3(sinRot, 0f, cosRot);
            // Screen right direction in world space
            float3 screenRight = new float3(cosRot, 0f, -sinRot);

            // Combine input with screen directions
            float3 worldDir = (screenUp * input.y) + (screenRight * input.x);

            // Normalize if non-zero
            float len = math.length(worldDir);
            if (len > 0.0001f)
                worldDir /= len;

            return worldDir;
        }

        /// <summary>
        /// Transform raw WASD input to world-space direction for top-down camera.
        /// Standard world-aligned movement (no transformation needed).
        /// </summary>
        /// <param name="input">Raw input (x = horizontal, y = vertical).</param>
        /// <returns>World-space movement direction (normalized if non-zero).</returns>
        public static float3 TransformTopDownInput(float2 input)
        {
            if (math.lengthsq(input) < 0.0001f)
                return float3.zero;

            // Direct mapping: input.y = Z, input.x = X
            float3 worldDir = new float3(input.x, 0f, input.y);

            // Normalize if non-zero
            float len = math.length(worldDir);
            if (len > 0.0001f)
                worldDir /= len;

            return worldDir;
        }

        /// <summary>
        /// Transform movement input based on camera mode.
        /// Convenience method that dispatches to the appropriate transformation.
        /// </summary>
        /// <param name="input">Raw input (x = horizontal, y = vertical).</param>
        /// <param name="mode">Current camera mode.</param>
        /// <param name="cameraRotation">Camera yaw rotation in degrees.</param>
        /// <returns>World-space movement direction (normalized if non-zero).</returns>
        public static float3 TransformMovementInput(float2 input, CameraMode mode, float cameraRotation)
        {
            switch (mode)
            {
                case CameraMode.ThirdPersonFollow:
                    return TransformThirdPersonInput(input, cameraRotation);

                case CameraMode.IsometricFixed:
                case CameraMode.IsometricRotatable:
                    return TransformIsometricInput(input, cameraRotation);

                case CameraMode.TopDownFixed:
                    return TransformTopDownInput(input);

                default:
                    return TransformThirdPersonInput(input, cameraRotation);
            }
        }

        // ============================================================
        // CURSOR PROJECTION
        // ============================================================

        /// <summary>
        /// Project cursor screen position to world space using ground plane.
        /// </summary>
        /// <param name="cursorScreenPos">Cursor position in screen coordinates.</param>
        /// <param name="camera">Camera to use for projection.</param>
        /// <param name="groundHeight">Y height of the ground plane.</param>
        /// <returns>World-space position on ground plane.</returns>
        public static float3 ProjectCursorToGroundPlane(float2 cursorScreenPos, UnityEngine.Camera camera, float groundHeight = 0f)
        {
            if (camera == null)
                return new float3(0f, groundHeight, 0f);

            // Create ray from camera through cursor
            Ray ray = camera.ScreenPointToRay(new Vector3(cursorScreenPos.x, cursorScreenPos.y, 0f));

            // Intersect with ground plane at specified height
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));

            if (groundPlane.Raycast(ray, out float distance))
            {
                return (float3)ray.GetPoint(distance);
            }

            // Fallback: project forward from camera if ray is parallel to plane
            return (float3)(ray.origin + ray.direction * 100f);
        }

        /// <summary>
        /// Project cursor screen position to world space using terrain raycast.
        /// </summary>
        /// <param name="cursorScreenPos">Cursor position in screen coordinates.</param>
        /// <param name="camera">Camera to use for projection.</param>
        /// <param name="terrainLayers">Layer mask for terrain raycast.</param>
        /// <param name="maxDistance">Maximum raycast distance.</param>
        /// <returns>World-space position on terrain, or ground plane fallback.</returns>
        public static float3 ProjectCursorToTerrain(float2 cursorScreenPos, UnityEngine.Camera camera, LayerMask terrainLayers, float maxDistance = 1000f)
        {
            if (camera == null)
                return float3.zero;

            // Create ray from camera through cursor
            Ray ray = camera.ScreenPointToRay(new Vector3(cursorScreenPos.x, cursorScreenPos.y, 0f));

            // Raycast to terrain
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, terrainLayers))
            {
                return (float3)hit.point;
            }

            // Fallback to ground plane
            return ProjectCursorToGroundPlane(cursorScreenPos, camera, 0f);
        }

        /// <summary>
        /// Project cursor to a fixed height (character's Y position).
        /// </summary>
        /// <param name="cursorScreenPos">Cursor position in screen coordinates.</param>
        /// <param name="camera">Camera to use for projection.</param>
        /// <param name="targetHeight">Fixed Y height to project to.</param>
        /// <returns>World-space position at the specified height.</returns>
        public static float3 ProjectCursorToFixedHeight(float2 cursorScreenPos, UnityEngine.Camera camera, float targetHeight)
        {
            return ProjectCursorToGroundPlane(cursorScreenPos, camera, targetHeight);
        }

        /// <summary>
        /// Project cursor to world space using the specified projection method.
        /// </summary>
        /// <param name="cursorScreenPos">Cursor position in screen coordinates.</param>
        /// <param name="camera">Camera to use for projection.</param>
        /// <param name="method">Projection method to use.</param>
        /// <param name="config">Camera config for additional parameters.</param>
        /// <param name="characterHeight">Current character Y position (for FixedHeight method).</param>
        /// <returns>World-space aim point.</returns>
        public static float3 ProjectCursor(
            float2 cursorScreenPos,
            UnityEngine.Camera camera,
            CursorProjectionMethod method,
            CameraConfig config,
            float characterHeight = 0f)
        {
            switch (method)
            {
                case CursorProjectionMethod.GroundPlane:
                    return ProjectCursorToGroundPlane(cursorScreenPos, camera, config?.CursorProjectionHeight ?? 0f);

                case CursorProjectionMethod.TerrainHit:
                    return ProjectCursorToTerrain(cursorScreenPos, camera, config?.TerrainLayers ?? ~0);

                case CursorProjectionMethod.FixedHeight:
                    return ProjectCursorToFixedHeight(cursorScreenPos, camera, characterHeight);

                case CursorProjectionMethod.SmartHeight:
                    // Try terrain first, fall back to character height
                    float3 terrainHit = ProjectCursorToTerrain(cursorScreenPos, camera, config?.TerrainLayers ?? ~0);
                    // If terrain hit is very far below character, use character height instead
                    if (terrainHit.y < characterHeight - 10f)
                        return ProjectCursorToFixedHeight(cursorScreenPos, camera, characterHeight);
                    return terrainHit;

                default:
                    return ProjectCursorToGroundPlane(cursorScreenPos, camera, 0f);
            }
        }

        // ============================================================
        // AIM DIRECTION CALCULATION
        // ============================================================

        /// <summary>
        /// Calculate aim direction from character position to target point.
        /// </summary>
        /// <param name="characterPosition">World position of the character.</param>
        /// <param name="targetPoint">World position to aim at.</param>
        /// <param name="flattenY">If true, creates a flat (horizontal) aim direction.</param>
        /// <returns>Normalized aim direction vector.</returns>
        public static float3 CalculateAimDirection(float3 characterPosition, float3 targetPoint, bool flattenY = false)
        {
            float3 direction = targetPoint - characterPosition;

            if (flattenY)
            {
                direction.y = 0f;
            }

            float len = math.length(direction);
            if (len < 0.0001f)
            {
                // Default to forward if target is at character position
                return new float3(0f, 0f, 1f);
            }

            return direction / len;
        }

        /// <summary>
        /// Get aim direction for third-person camera (from camera center).
        /// </summary>
        /// <param name="camera">Camera to get aim from.</param>
        /// <returns>Normalized aim direction (camera forward).</returns>
        public static float3 GetThirdPersonAimDirection(UnityEngine.Camera camera)
        {
            if (camera == null)
                return new float3(0f, 0f, 1f);

            return (float3)camera.transform.forward;
        }

        /// <summary>
        /// Get aim direction for isometric camera (character to cursor).
        /// </summary>
        /// <param name="characterPosition">World position of the character.</param>
        /// <param name="cursorWorldPoint">Cursor projected to world space.</param>
        /// <returns>Normalized aim direction on XZ plane.</returns>
        public static float3 GetIsometricAimDirection(float3 characterPosition, float3 cursorWorldPoint)
        {
            return CalculateAimDirection(characterPosition, cursorWorldPoint, flattenY: true);
        }

        // ============================================================
        // ROTATION HELPERS
        // ============================================================

        /// <summary>
        /// Snap a rotation to the nearest increment.
        /// Used for rotatable isometric camera Q/E rotation.
        /// </summary>
        /// <param name="currentRotation">Current rotation in degrees.</param>
        /// <param name="increment">Snap increment in degrees.</param>
        /// <returns>Snapped rotation in degrees.</returns>
        public static float SnapRotation(float currentRotation, float increment)
        {
            return math.round(currentRotation / increment) * increment;
        }

        /// <summary>
        /// Get the next rotation step in a direction.
        /// </summary>
        /// <param name="currentRotation">Current rotation in degrees.</param>
        /// <param name="increment">Rotation increment in degrees.</param>
        /// <param name="direction">Direction (-1 = counter-clockwise, 1 = clockwise).</param>
        /// <returns>New rotation in degrees (normalized to 0-360 range).</returns>
        public static float StepRotation(float currentRotation, float increment, int direction)
        {
            float newRotation = currentRotation + (increment * direction);
            // Normalize to 0-360 range
            while (newRotation < 0f) newRotation += 360f;
            while (newRotation >= 360f) newRotation -= 360f;
            return newRotation;
        }

        /// <summary>
        /// Smoothly interpolate between two rotations (handling wraparound).
        /// </summary>
        /// <param name="from">Starting rotation in degrees.</param>
        /// <param name="to">Target rotation in degrees.</param>
        /// <param name="t">Interpolation factor (0-1).</param>
        /// <returns>Interpolated rotation in degrees.</returns>
        public static float LerpRotation(float from, float to, float t)
        {
            // Handle wraparound by finding shortest path
            float diff = to - from;
            while (diff > 180f) diff -= 360f;
            while (diff < -180f) diff += 360f;

            float result = from + diff * math.saturate(t);
            // Normalize to 0-360 range
            while (result < 0f) result += 360f;
            while (result >= 360f) result -= 360f;
            return result;
        }
    }
}
