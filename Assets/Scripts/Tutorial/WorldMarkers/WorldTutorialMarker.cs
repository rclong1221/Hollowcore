using UnityEngine;

namespace DIG.Tutorial.WorldMarkers
{
    /// <summary>
    /// EPIC 18.4: MonoBehaviour tracking a world-space target for tutorial markers.
    /// Projects world position to screen coordinates for UI overlay positioning.
    /// Supports off-screen edge clamping following OffScreenIndicatorRenderer pattern.
    /// </summary>
    public class WorldTutorialMarker : MonoBehaviour
    {
        public Vector3 TargetPosition { get; set; }
        public bool IsActive { get; private set; }

        public Vector2 ScreenPosition { get; private set; }
        public float Distance { get; private set; }
        public bool IsOnScreen { get; private set; }

        private float _screenEdgeMargin = 40f;
        private Camera _cachedCamera;

        public void Activate(Vector3 worldPosition, float edgeMargin = 40f)
        {
            TargetPosition = worldPosition;
            _screenEdgeMargin = edgeMargin;
            IsActive = true;
            _cachedCamera = null; // Force refresh on activate
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            IsActive = false;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!IsActive) return;

            if (_cachedCamera == null)
                _cachedCamera = Camera.main;
            if (_cachedCamera == null) return;

            Vector3 screenPos = _cachedCamera.WorldToScreenPoint(TargetPosition);
            float screenW = Screen.width;
            float screenH = Screen.height;

            IsOnScreen = screenPos.z > 0 &&
                         screenPos.x >= 0 && screenPos.x <= screenW &&
                         screenPos.y >= 0 && screenPos.y <= screenH;

            if (!IsOnScreen)
            {
                // Behind camera: flip
                if (screenPos.z < 0)
                {
                    screenPos.x = screenW - screenPos.x;
                    screenPos.y = screenH - screenPos.y;
                }

                // Clamp to screen edges
                screenPos.x = Mathf.Clamp(screenPos.x, _screenEdgeMargin, screenW - _screenEdgeMargin);
                screenPos.y = Mathf.Clamp(screenPos.y, _screenEdgeMargin, screenH - _screenEdgeMargin);
            }

            // Convert to UI Toolkit coords (top-left origin)
            ScreenPosition = new Vector2(screenPos.x, screenH - screenPos.y);
            Distance = Vector3.Distance(_cachedCamera.transform.position, TargetPosition);
        }
    }
}
