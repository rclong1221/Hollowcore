using System;
using System.Collections.Generic;
using DIG.Notifications.Config;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Notifications.Channels
{
    /// <summary>
    /// EPIC 18.3: Wrapper around a cloned UXML notification VisualElement.
    /// Handles population, animations, and countdown progress bar.
    /// </summary>
    public class NotificationElement
    {
        public int HandleId { get; private set; }
        public VisualElement Root { get; private set; }
        public bool IsAnimatingOut { get; private set; }

        private Label _titleLabel;
        private Label _bodyLabel;
        private VisualElement _iconElement;
        private Button _actionBtn;
        private Button _closeBtn;
        private VisualElement _progressBar;

        private float _duration;
        private float _elapsed;
        private string _visibleClass;
        private string _dismissingClass;
        private bool _exitCompleted;

        public event Action<int> OnExpired;
        public event Action<int> OnCloseClicked;
        public event Action<int> OnActionClicked;

        public void Initialize(VisualElement root, string visibleClass, string dismissingClass)
        {
            Root = root;
            _visibleClass = visibleClass;
            _dismissingClass = dismissingClass;

            _titleLabel = root.Q<Label>("title");
            _bodyLabel = root.Q<Label>("body");
            _iconElement = root.Q("icon");
            _actionBtn = root.Q<Button>("action-btn");
            _closeBtn = root.Q<Button>("close-btn");
            _progressBar = root.Q("progress-bar");

            if (_closeBtn != null)
                _closeBtn.clicked += () => OnCloseClicked?.Invoke(HandleId);

            if (_actionBtn != null)
                _actionBtn.clicked += () => OnActionClicked?.Invoke(HandleId);
        }

        public void Populate(int handleId, NotificationData data, NotificationStyleSO style, float channelDefaultDuration)
        {
            HandleId = handleId;
            _elapsed = 0f;
            IsAnimatingOut = false;

            // Resolve duration: data > style > channel default
            _duration = data.Duration > 0 ? data.Duration
                : (style != null && style.DefaultDuration > 0 ? style.DefaultDuration : channelDefaultDuration);

            // Text
            if (_titleLabel != null) _titleLabel.text = data.Title ?? "";
            if (_bodyLabel != null) _bodyLabel.text = data.Body ?? "";

            // Icon
            if (_iconElement != null)
            {
                if (data.Icon != null)
                {
                    _iconElement.style.backgroundImage = new StyleBackground(data.Icon);
                    _iconElement.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _iconElement.style.display = DisplayStyle.None;
                }
            }

            // Action button
            if (_actionBtn != null)
            {
                if (!string.IsNullOrEmpty(data.ActionButtonLabel))
                {
                    _actionBtn.text = data.ActionButtonLabel;
                    _actionBtn.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _actionBtn.style.display = DisplayStyle.None;
                }
            }

            // Style colors
            if (style != null)
            {
                Root.style.backgroundColor = style.BackgroundColor;
                Root.style.borderTopColor = style.BorderColor;
                Root.style.borderBottomColor = style.BorderColor;
                Root.style.borderLeftColor = style.BorderColor;
                Root.style.borderRightColor = style.BorderColor;
                if (_titleLabel != null) _titleLabel.style.color = style.TitleColor;
                if (_bodyLabel != null) _bodyLabel.style.color = style.BodyColor;
                if (_iconElement != null) _iconElement.style.unityBackgroundImageTintColor = style.IconTint;
            }

            // Progress bar reset
            if (_progressBar != null)
            {
                _progressBar.style.width = Length.Percent(100);
                _progressBar.style.display = _duration > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void PlayEnterAnimation()
        {
            // Remove dismissing class, ensure visible class is not present (for CSS transition trigger)
            Root.RemoveFromClassList(_dismissingClass);
            Root.RemoveFromClassList(_visibleClass);

            // Defer by 1 frame so the layout pass registers the initial state
            Root.schedule.Execute(() => Root.AddToClassList(_visibleClass));
        }

        public void PlayExitAnimation(Action onComplete)
        {
            if (IsAnimatingOut) return;
            IsAnimatingOut = true;
            _exitCompleted = false;

            Root.RemoveFromClassList(_visibleClass);
            Root.AddToClassList(_dismissingClass);

            // Guard: onComplete must fire exactly once (TransitionEnd fires per-property)
            void CompleteOnce()
            {
                if (_exitCompleted) return;
                _exitCompleted = true;
                onComplete?.Invoke();
            }

            // Use one-shot callback pattern — unregister after first invocation
            EventCallback<TransitionEndEvent> handler = null;
            handler = evt =>
            {
                Root.UnregisterCallback(handler);
                CompleteOnce();
            };
            Root.RegisterCallback(handler);

            // Safety fallback — if TransitionEnd never fires (e.g., no transition defined)
            Root.schedule.Execute(() => CompleteOnce()).StartingIn(600);
        }

        /// <summary>Tick the countdown. Returns true if expired this frame.</summary>
        public bool Tick(float deltaTime)
        {
            if (IsAnimatingOut || _duration <= 0) return false;

            _elapsed += deltaTime;

            // Update progress bar — direct width set each frame (no CSS transition needed)
            if (_progressBar != null)
            {
                float pct = Mathf.Clamp01(1f - _elapsed / _duration) * 100f;
                _progressBar.style.width = Length.Percent(pct);
            }

            if (_elapsed >= _duration)
            {
                OnExpired?.Invoke(HandleId);
                return true;
            }

            return false;
        }

        /// <summary>Reset state for pooling reuse. Clears event subscribers to prevent stale callbacks.</summary>
        public void Reset()
        {
            HandleId = 0;
            _elapsed = 0f;
            _duration = 0f;
            IsAnimatingOut = false;
            _exitCompleted = false;
            Root.RemoveFromClassList(_visibleClass);
            Root.RemoveFromClassList(_dismissingClass);

            // Clear event subscribers to prevent accumulation across pool reuse cycles
            OnExpired = null;
            OnCloseClicked = null;
            OnActionClicked = null;
        }
    }
}
