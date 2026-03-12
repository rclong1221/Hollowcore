using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Toast popup for achievement unlocks.
    /// Animated slide-in from right, icon + name + tier badge + reward text.
    /// Queue system: max N pending toasts, display one at a time.
    /// Implements IAchievementUIProvider for toast functionality.
    /// </summary>
    public class AchievementToastView : MonoBehaviour, IAchievementUIProvider
    {
        [Header("Toast Elements")]
        [SerializeField] private RectTransform _toastPanel;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private TMP_Text _tierText;
        [SerializeField] private Image _tierBorder;

        [Header("Animation")]
        [SerializeField] private float _slideInDuration = 0.3f;
        [SerializeField] private float _slideOutDuration = 0.2f;
        [SerializeField] private float _offscreenX = 400f;

        private readonly Queue<AchievementToastData> _toastQueue = new();
        private int _maxQueueSize = 5;
        private bool _isShowingToast;
        private float _toastTimer;
        private float _currentDisplayDuration;
        private ToastState _state;

        // Tier colors
        private static readonly Color BronzeColor = new Color32(205, 127, 50, 255);
        private static readonly Color SilverColor = new Color32(192, 192, 192, 255);
        private static readonly Color GoldColor = new Color32(255, 215, 0, 255);
        private static readonly Color PlatinumColor = new Color32(229, 228, 226, 255);

        private enum ToastState
        {
            Hidden,
            SlidingIn,
            Showing,
            SlidingOut
        }

        private void OnEnable()
        {
            AchievementUIRegistry.Register(this);
            if (_toastPanel != null)
                _toastPanel.gameObject.SetActive(false);

            var config = Resources.Load<AchievementConfigSO>("AchievementConfig");
            if (config != null)
                _maxQueueSize = config.ToastQueueMaxSize;
        }

        private void OnDisable()
        {
            AchievementUIRegistry.Unregister(this);
        }

        private void Update()
        {
            if (_state == ToastState.Hidden)
            {
                if (_toastQueue.Count > 0)
                    BeginShowToast(_toastQueue.Dequeue());
                return;
            }

            _toastTimer += Time.unscaledDeltaTime;

            switch (_state)
            {
                case ToastState.SlidingIn:
                    float inT = Mathf.Clamp01(_toastTimer / _slideInDuration);
                    float inX = Mathf.Lerp(_offscreenX, 0f, EaseOutBack(inT));
                    SetPanelX(inX);
                    if (inT >= 1f)
                    {
                        _state = ToastState.Showing;
                        _toastTimer = 0f;
                    }
                    break;

                case ToastState.Showing:
                    if (_toastTimer >= _currentDisplayDuration)
                    {
                        _state = ToastState.SlidingOut;
                        _toastTimer = 0f;
                    }
                    break;

                case ToastState.SlidingOut:
                    float outT = Mathf.Clamp01(_toastTimer / _slideOutDuration);
                    float outX = Mathf.Lerp(0f, _offscreenX, EaseInQuad(outT));
                    SetPanelX(outX);
                    if (outT >= 1f)
                    {
                        _state = ToastState.Hidden;
                        _isShowingToast = false;
                        if (_toastPanel != null)
                            _toastPanel.gameObject.SetActive(false);
                    }
                    break;
            }
        }

        private void BeginShowToast(AchievementToastData data)
        {
            if (_toastPanel == null) return;

            _toastPanel.gameObject.SetActive(true);
            _isShowingToast = true;

            if (_nameText != null) _nameText.text = data.AchievementName;
            if (_descriptionText != null) _descriptionText.text = data.Description;
            if (_rewardText != null) _rewardText.text = data.RewardText;
            if (_tierText != null) _tierText.text = data.Tier.ToString();
            if (_iconImage != null && data.Icon != null) _iconImage.sprite = data.Icon;

            if (_tierBorder != null)
                _tierBorder.color = GetTierColor(data.Tier);

            _currentDisplayDuration = data.DisplayDuration;
            _toastTimer = 0f;
            _state = ToastState.SlidingIn;
            SetPanelX(_offscreenX);
        }

        private void SetPanelX(float x)
        {
            if (_toastPanel == null) return;
            var pos = _toastPanel.anchoredPosition;
            pos.x = x;
            _toastPanel.anchoredPosition = pos;
        }

        private static Color GetTierColor(AchievementTier tier)
        {
            return tier switch
            {
                AchievementTier.Bronze => BronzeColor,
                AchievementTier.Silver => SilverColor,
                AchievementTier.Gold => GoldColor,
                AchievementTier.Platinum => PlatinumColor,
                _ => Color.white
            };
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float EaseInQuad(float t) => t * t;

        // --- IAchievementUIProvider ---

        public void ShowToast(AchievementToastData data)
        {
            if (_toastQueue.Count >= _maxQueueSize)
                _toastQueue.Dequeue(); // Drop oldest

            _toastQueue.Enqueue(data);
        }

        public void UpdatePanel(AchievementPanelData data)
        {
            // Toast view doesn't handle panel -- separate view
        }

        public void UpdateProgress(ushort achievementId, int currentValue, int nextThreshold)
        {
            // No-op for toast
        }

        public void HideToast()
        {
            if (_state != ToastState.Hidden)
            {
                _state = ToastState.SlidingOut;
                _toastTimer = 0f;
            }
        }
    }
}
