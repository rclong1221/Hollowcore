using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// UI controller for airlock progress display.
    /// Attach to a Canvas with progress bar and text elements.
    /// </summary>
    /// <remarks>
    /// Setup:
    /// 1. Create a Canvas with a panel for the airlock UI
    /// 2. Add a Slider or Image (filled) for progress bar
    /// 3. Add TextMeshProUGUI for status text and timer
    /// 4. Assign references in inspector
    /// 5. The panel should be inactive by default (hidden)
    /// </remarks>
    public class AirlockUIController : MonoBehaviour
    {
        public static AirlockUIController Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("The root panel that contains the airlock UI (enable/disable this)")]
        public GameObject AirlockPanel;

        [Tooltip("Progress bar slider (0-1 range)")]
        public Slider ProgressSlider;

        [Tooltip("Alternative: Progress bar as filled image")]
        public Image ProgressFill;

        [Tooltip("Status text (e.g., 'DEPRESSURIZING...')")]
        public TextMeshProUGUI StatusText;

        [Tooltip("Timer text showing remaining seconds")]
        public TextMeshProUGUI TimerText;

        [Tooltip("Optional: Icon that changes based on direction")]
        public Image DirectionIcon;

        [Header("Icons")]
        [Tooltip("Icon for exiting to vacuum")]
        public Sprite ExitIcon;

        [Tooltip("Icon for entering ship")]
        public Sprite EnterIcon;

        [Header("Colors")]
        public Color DepressurizeColor = new Color(1f, 0.5f, 0.2f, 1f); // Orange
        public Color PressurizeColor = new Color(0.2f, 0.8f, 1f, 1f);   // Cyan

        [Header("Animation")]
        [Tooltip("Smooth progress bar interpolation speed")]
        public float ProgressLerpSpeed = 8f;

        [Tooltip("Panel fade speed")]
        public float FadeSpeed = 5f;

        private float _targetProgress;
        private float _currentProgress;
        private bool _isVisible;
        private CanvasGroup _canvasGroup;

        void Awake()
        {
            Instance = this;
            
            // Get or add CanvasGroup for fading
            if (AirlockPanel != null)
            {
                _canvasGroup = AirlockPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = AirlockPanel.AddComponent<CanvasGroup>();
                }
                _canvasGroup.alpha = 0f;
                AirlockPanel.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // Smooth progress bar
            if (Mathf.Abs(_currentProgress - _targetProgress) > 0.001f)
            {
                _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.deltaTime * ProgressLerpSpeed);
                UpdateProgressVisuals();
            }

            // Fade in/out
            if (_canvasGroup != null)
            {
                float targetAlpha = _isVisible ? 1f : 0f;
                if (Mathf.Abs(_canvasGroup.alpha - targetAlpha) > 0.01f)
                {
                    _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, targetAlpha, Time.deltaTime * FadeSpeed);
                }

                // Disable panel when fully faded out
                if (!_isVisible && _canvasGroup.alpha < 0.01f && AirlockPanel.activeSelf)
                {
                    AirlockPanel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Show the airlock progress UI.
        /// </summary>
        /// <param name="progress">0-1 progress value</param>
        /// <param name="statusText">Status message to display</param>
        /// <param name="timeRemaining">Seconds remaining</param>
        public void ShowProgress(float progress, string statusText, float timeRemaining)
        {
            if (AirlockPanel != null && !AirlockPanel.activeSelf)
            {
                AirlockPanel.SetActive(true);
            }

            _isVisible = true;
            _targetProgress = Mathf.Clamp01(progress);

            // Update status text
            if (StatusText != null)
            {
                StatusText.text = statusText;

                // Color based on direction
                if (statusText.Contains("DEPRESSUR"))
                {
                    StatusText.color = DepressurizeColor;
                }
                else if (statusText.Contains("PRESSUR"))
                {
                    StatusText.color = PressurizeColor;
                }
            }

            // Update timer
            if (TimerText != null)
            {
                if (timeRemaining > 0)
                {
                    TimerText.text = $"{timeRemaining:F1}s";
                }
                else
                {
                    TimerText.text = "COMPLETE";
                }
            }

            // Update direction icon
            if (DirectionIcon != null)
            {
                if (statusText.Contains("DEPRESSUR") && ExitIcon != null)
                {
                    DirectionIcon.sprite = ExitIcon;
                    DirectionIcon.color = DepressurizeColor;
                }
                else if (statusText.Contains("PRESSUR") && EnterIcon != null)
                {
                    DirectionIcon.sprite = EnterIcon;
                    DirectionIcon.color = PressurizeColor;
                }
            }

            UpdateProgressVisuals();
        }

        /// <summary>
        /// Hide the airlock progress UI.
        /// </summary>
        public void HideProgress()
        {
            _isVisible = false;
        }

        private void UpdateProgressVisuals()
        {
            if (ProgressSlider != null)
            {
                ProgressSlider.value = _currentProgress;
            }

            if (ProgressFill != null)
            {
                ProgressFill.fillAmount = _currentProgress;
            }
        }

        /// <summary>
        /// Show a quick flash message (e.g., "AIRLOCK BUSY" or "AIRLOCK LOCKED").
        /// </summary>
        public void ShowFlashMessage(string message, float duration = 2f)
        {
            StartCoroutine(FlashMessageCoroutine(message, duration));
        }

        System.Collections.IEnumerator FlashMessageCoroutine(string message, float duration)
        {
            if (AirlockPanel != null)
            {
                AirlockPanel.SetActive(true);
            }

            _isVisible = true;
            _targetProgress = 0f;
            _currentProgress = 0f;

            if (StatusText != null)
            {
                StatusText.text = message;
                StatusText.color = Color.red;
            }

            if (TimerText != null)
            {
                TimerText.text = "";
            }

            yield return new WaitForSeconds(duration);

            _isVisible = false;
        }
    }
}
