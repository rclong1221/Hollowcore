using UnityEngine;
using UnityEngine.UI;

namespace DIG.Weapons.UI
{
    /// <summary>
    /// EPIC 14.20: Dynamic crosshair that responds to weapon spread and movement.
    /// </summary>
    public class CrosshairController : MonoBehaviour
    {
        public static CrosshairController Instance { get; private set; }

        [Header("Crosshair Elements")]
        [Tooltip("Center dot")]
        [SerializeField] private RectTransform centerDot;

        [Tooltip("Top line")]
        [SerializeField] private RectTransform topLine;

        [Tooltip("Bottom line")]
        [SerializeField] private RectTransform bottomLine;

        [Tooltip("Left line")]
        [SerializeField] private RectTransform leftLine;

        [Tooltip("Right line")]
        [SerializeField] private RectTransform rightLine;

        [Header("Spread Settings")]
        [Tooltip("Minimum crosshair spread (pixels)")]
        [SerializeField] private float minSpread = 10f;

        [Tooltip("Maximum crosshair spread (pixels)")]
        [SerializeField] private float maxSpread = 100f;

        [Tooltip("Spread interpolation speed")]
        [SerializeField] private float spreadLerpSpeed = 10f;

        [Header("Hit Feedback")]
        [Tooltip("Scale punch on hit")]
        [SerializeField] private float hitPunchScale = 1.2f;

        [Tooltip("Hit punch duration")]
        [SerializeField] private float hitPunchDuration = 0.1f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color enemyHoverColor = Color.red;
        [SerializeField] private Color friendlyHoverColor = Color.green;

        [Header("Visibility")]
        [SerializeField] private bool hideWhenAiming = true;
        [SerializeField] private float aimFadeSpeed = 10f;

        // State
        private float _targetSpread;
        private float _currentSpread;
        private float _hitPunchTimer;
        private float _currentScale = 1f;
        private bool _isAiming;
        private float _currentAlpha = 1f;
        private Image[] _crosshairImages;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Cache components
            _crosshairImages = GetComponentsInChildren<Image>();
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Update()
        {
            // Interpolate spread
            _currentSpread = Mathf.Lerp(_currentSpread, _targetSpread, Time.deltaTime * spreadLerpSpeed);
            UpdateCrosshairSpread();

            // Update hit punch
            if (_hitPunchTimer > 0)
            {
                _hitPunchTimer -= Time.deltaTime;
                float punchProgress = _hitPunchTimer / hitPunchDuration;
                _currentScale = Mathf.Lerp(1f, hitPunchScale, punchProgress);
                transform.localScale = Vector3.one * _currentScale;
            }

            // Update aim visibility
            if (hideWhenAiming)
            {
                float targetAlpha = _isAiming ? 0f : 1f;
                _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, Time.deltaTime * aimFadeSpeed);
                _canvasGroup.alpha = _currentAlpha;
            }
        }

        private void UpdateCrosshairSpread()
        {
            float spread = _currentSpread;

            if (topLine != null)
            {
                topLine.anchoredPosition = new Vector2(0, spread);
            }

            if (bottomLine != null)
            {
                bottomLine.anchoredPosition = new Vector2(0, -spread);
            }

            if (leftLine != null)
            {
                leftLine.anchoredPosition = new Vector2(-spread, 0);
            }

            if (rightLine != null)
            {
                rightLine.anchoredPosition = new Vector2(spread, 0);
            }
        }

        /// <summary>
        /// Set the target spread (0-1 normalized).
        /// </summary>
        public void SetSpread(float normalizedSpread)
        {
            _targetSpread = Mathf.Lerp(minSpread, maxSpread, Mathf.Clamp01(normalizedSpread));
        }

        /// <summary>
        /// Set spread directly in pixels.
        /// </summary>
        public void SetSpreadPixels(float spreadPixels)
        {
            _targetSpread = Mathf.Clamp(spreadPixels, minSpread, maxSpread);
        }

        /// <summary>
        /// Trigger hit punch animation.
        /// </summary>
        public void TriggerHitPunch()
        {
            _hitPunchTimer = hitPunchDuration;
        }

        /// <summary>
        /// Set whether player is aiming down sights.
        /// </summary>
        public void SetAiming(bool isAiming)
        {
            _isAiming = isAiming;
        }

        /// <summary>
        /// Set crosshair color based on target type.
        /// </summary>
        public void SetTargetType(CrosshairTargetType targetType)
        {
            Color targetColor = targetType switch
            {
                CrosshairTargetType.Enemy => enemyHoverColor,
                CrosshairTargetType.Friendly => friendlyHoverColor,
                _ => normalColor
            };

            foreach (var image in _crosshairImages)
            {
                if (image != null)
                {
                    image.color = targetColor;
                }
            }
        }

        /// <summary>
        /// Show/hide the crosshair.
        /// </summary>
        public void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    public enum CrosshairTargetType
    {
        None,
        Enemy,
        Friendly
    }
}
