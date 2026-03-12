using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.SceneManagement.UI
{
    /// <summary>
    /// EPIC 18.6: MonoBehaviour on a DontDestroyOnLoad canvas.
    /// Provides direct UI control: background image, progress bar, tip text,
    /// fade via CanvasGroup (manual lerp, no DOTween — follows WidgetAnimator pattern).
    /// </summary>
    public class LoadingScreenView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private Image _progressBarFill;
        [SerializeField] private Text _tipText;
        [SerializeField] private Text _phaseText;
        [SerializeField] private GameObject _indeterminateSpinner;

        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0.01f;

        public void SetBackground(Sprite sprite)
        {
            if (_backgroundImage != null && sprite != null)
                _backgroundImage.sprite = sprite;
        }

        public void SetTip(string text)
        {
            if (_tipText != null)
                _tipText.text = text ?? "";
        }

        public void SetPhaseText(string text)
        {
            if (_phaseText != null)
                _phaseText.text = text ?? "";
        }

        public void SetProgress(float progress, ProgressBarStyle style)
        {
            if (_progressBar != null)
            {
                _progressBar.gameObject.SetActive(style != ProgressBarStyle.Indeterminate);
                _progressBar.value = Mathf.Clamp01(progress);
            }

            if (_indeterminateSpinner != null)
                _indeterminateSpinner.SetActive(style == ProgressBarStyle.Indeterminate);
        }

        /// <summary>
        /// Manual lerp fade-in coroutine using unscaled time.
        /// </summary>
        public IEnumerator FadeIn(float duration)
        {
            if (_canvasGroup == null) yield break;

            gameObject.SetActive(true);
            _canvasGroup.blocksRaycasts = true;

            if (duration <= 0f)
            {
                _canvasGroup.alpha = 1f;
                yield break;
            }

            float elapsed = 0f;
            _canvasGroup.alpha = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Manual lerp fade-out coroutine using unscaled time.
        /// </summary>
        public IEnumerator FadeOut(float duration)
        {
            if (_canvasGroup == null) yield break;

            if (duration <= 0f)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
                yield break;
            }

            float elapsed = 0f;
            _canvasGroup.alpha = 1f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public void ShowImmediate()
        {
            gameObject.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        public void HideImmediate()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }
    }
}
