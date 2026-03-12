using Unity.Mathematics;
using DIG.CameraSystem;

namespace DIG.Widgets
{
    /// <summary>
    /// EPIC 15.26 Phase 1: Static camera data updated each frame by WidgetProjectionSystem.
    /// Provides VP matrix and screen dimensions for world-to-screen projection.
    /// Follows the static data pattern used by DamageVisualQueue.
    /// </summary>
    public static class WidgetCameraData
    {
        /// <summary>View-Projection matrix from Camera.main.</summary>
        public static float4x4 VPMatrix;

        /// <summary>Screen dimensions in pixels (width, height).</summary>
        public static float2 ScreenSize;

        /// <summary>Camera world-space position.</summary>
        public static float3 CameraPosition;

        /// <summary>Camera forward direction.</summary>
        public static float3 CameraForward;

        /// <summary>Active camera mode from CameraModeProvider.</summary>
        public static CameraMode CameraMode;

        /// <summary>True if camera is orthographic (no perspective divide needed).</summary>
        public static bool IsOrthographic;

        /// <summary>Whether camera data has been set at least once this session.</summary>
        public static bool IsValid;

        /// <summary>
        /// Project a world position to screen-space pixel coordinates.
        /// Returns false if the position is behind the camera.
        /// </summary>
        public static bool WorldToScreen(float3 worldPos, out float2 screenPos)
        {
            float4 clipPos = math.mul(VPMatrix, new float4(worldPos, 1f));

            // Behind camera check
            if (clipPos.w <= 0f)
            {
                screenPos = float2.zero;
                return false;
            }

            // Perspective divide → NDC (-1 to 1)
            float3 ndc = clipPos.xyz / clipPos.w;

            // NDC to pixel coordinates
            screenPos = new float2(
                (ndc.x * 0.5f + 0.5f) * ScreenSize.x,
                (ndc.y * 0.5f + 0.5f) * ScreenSize.y
            );

            return true;
        }

        /// <summary>
        /// Test if a screen position is within the visible screen area.
        /// </summary>
        public static bool IsOnScreen(float2 screenPos)
        {
            return screenPos.x >= 0f && screenPos.x <= ScreenSize.x &&
                   screenPos.y >= 0f && screenPos.y <= ScreenSize.y;
        }
    }
}
