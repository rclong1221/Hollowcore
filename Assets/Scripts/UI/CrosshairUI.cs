using UnityEngine;
using UnityEngine.UI;

namespace DIG.UI
{
    /// <summary>
    /// Simple crosshair UI that displays at screen center.
    /// Shows where the player is aiming (camera center).
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("Crosshair Settings")]
        [SerializeField] private float _size = 20f;
        [SerializeField] private float _thickness = 2f;
        [SerializeField] private float _gap = 4f;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private bool _showDot = true;
        [SerializeField] private float _dotSize = 4f;
        
        [Header("Style")]
        [SerializeField] private CrosshairStyle _style = CrosshairStyle.Cross;
        
        private RectTransform _top, _bottom, _left, _right, _dot;
        private Image _topImg, _bottomImg, _leftImg, _rightImg, _dotImg;
        
        public enum CrosshairStyle
        {
            Cross,      // + shape
            Dot,        // Single dot
            Circle,     // Circle outline (requires sprite)
            Custom      // Use assigned sprite
        }
        
        private void Awake()
        {
            CreateCrosshair();
        }
        
        private void CreateCrosshair()
        {
            // Create canvas if we're not already on one
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // On top of everything
                gameObject.AddComponent<CanvasScaler>();
            }
            
            // Container for crosshair elements
            var container = new GameObject("CrosshairContainer");
            container.transform.SetParent(transform);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(_size * 2, _size * 2);
            
            if (_style == CrosshairStyle.Cross || _style == CrosshairStyle.Dot)
            {
                // Create cross lines
                _top = CreateLine(container.transform, "Top", new Vector2(0, _gap + _size / 2), new Vector2(_thickness, _size));
                _bottom = CreateLine(container.transform, "Bottom", new Vector2(0, -(_gap + _size / 2)), new Vector2(_thickness, _size));
                _left = CreateLine(container.transform, "Left", new Vector2(-(_gap + _size / 2), 0), new Vector2(_size, _thickness));
                _right = CreateLine(container.transform, "Right", new Vector2(_gap + _size / 2, 0), new Vector2(_size, _thickness));
                
                _topImg = _top.GetComponent<Image>();
                _bottomImg = _bottom.GetComponent<Image>();
                _leftImg = _left.GetComponent<Image>();
                _rightImg = _right.GetComponent<Image>();
                
                // Hide lines if dot-only style
                if (_style == CrosshairStyle.Dot)
                {
                    _top.gameObject.SetActive(false);
                    _bottom.gameObject.SetActive(false);
                    _left.gameObject.SetActive(false);
                    _right.gameObject.SetActive(false);
                }
            }
            
            // Center dot
            if (_showDot || _style == CrosshairStyle.Dot)
            {
                _dot = CreateLine(container.transform, "Dot", Vector2.zero, new Vector2(_dotSize, _dotSize));
                _dotImg = _dot.GetComponent<Image>();
            }
            
            UpdateColors();
        }
        
        private RectTransform CreateLine(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            var image = go.AddComponent<Image>();
            image.color = _color;
            image.raycastTarget = false;
            
            return rect;
        }
        
        private void UpdateColors()
        {
            if (_topImg != null) _topImg.color = _color;
            if (_bottomImg != null) _bottomImg.color = _color;
            if (_leftImg != null) _leftImg.color = _color;
            if (_rightImg != null) _rightImg.color = _color;
            if (_dotImg != null) _dotImg.color = _color;
        }
        
        /// <summary>
        /// Set crosshair visibility.
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
        
        /// <summary>
        /// Set crosshair color.
        /// </summary>
        public void SetColor(Color color)
        {
            _color = color;
            UpdateColors();
        }
        
        /// <summary>
        /// Animate crosshair spread (e.g., when firing).
        /// </summary>
        public void SetSpread(float spread)
        {
            float offset = _gap + _size / 2 + spread;
            
            if (_top != null) _top.anchoredPosition = new Vector2(0, offset);
            if (_bottom != null) _bottom.anchoredPosition = new Vector2(0, -offset);
            if (_left != null) _left.anchoredPosition = new Vector2(-offset, 0);
            if (_right != null) _right.anchoredPosition = new Vector2(offset, 0);
        }
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Update in editor when values change
            if (Application.isPlaying && _topImg != null)
            {
                UpdateColors();
            }
        }
        #endif
    }
}
