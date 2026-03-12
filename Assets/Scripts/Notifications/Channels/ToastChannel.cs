using System;
using System.Collections.Generic;
using DIG.Notifications.Config;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Notifications.Channels
{
    /// <summary>
    /// EPIC 18.3: Toast channel — corner slide-in notifications with vertical stacking.
    /// </summary>
    public class ToastChannel : INotificationChannel
    {
        private readonly NotificationChannelConfig _config;
        private readonly VisualTreeAsset _template;
        private VisualElement _container;

        // Active elements keyed by handle ID
        private readonly Dictionary<int, NotificationElement> _active = new();
        // Pool of reusable elements
        private readonly Queue<NotificationElement> _pool = new();
        // Ordered list for iteration (avoids dict allocation during Tick)
        private readonly List<int> _activeIds = new();

        public int ActiveCount => _active.Count;
        public int MaxVisible => _config.MaxVisible;
        public bool HasCapacity => _active.Count < _config.MaxVisible;

        public event Action<int> OnDismissComplete;

        public ToastChannel(NotificationChannelConfig config, VisualTreeAsset template)
        {
            _config = config;
            _template = template;
        }

        public void AttachTo(VisualElement parentLayer)
        {
            _container = new VisualElement { name = "notification-toast-container" };
            _container.AddToClassList("notification-toast-container");
            _container.pickingMode = PickingMode.Ignore;

            // Apply position from config
            ApplyPosition(_container, _config.Position);

            parentLayer.Add(_container);
        }

        public int Show(int handleId, NotificationData data, NotificationStyleSO style)
        {
            var elem = AcquireElement();
            elem.Populate(handleId, data, style, _config.DefaultDuration);

            // Stack direction
            if (_config.StackDirection == StackDirection.Up)
                _container.Insert(0, elem.Root);
            else
                _container.Add(elem.Root);

            _active[handleId] = elem;
            _activeIds.Add(handleId);

            elem.OnExpired += OnElementExpired;
            elem.OnCloseClicked += OnElementCloseClicked;
            elem.OnActionClicked += id => data.OnAction?.Invoke();

            elem.PlayEnterAnimation();
            return handleId;
        }

        public void Dismiss(int handleId)
        {
            if (!_active.TryGetValue(handleId, out var elem) || elem.IsAnimatingOut) return;
            PlayExitAndRemove(elem);
        }

        public void Update(int handleId, NotificationData data)
        {
            if (!_active.TryGetValue(handleId, out var elem)) return;
            elem.Populate(handleId, data, null, _config.DefaultDuration);
        }

        public void Clear()
        {
            // Copy IDs to avoid modification during iteration
            var ids = new List<int>(_activeIds);
            for (int i = 0; i < ids.Count; i++)
            {
                if (_active.TryGetValue(ids[i], out var elem))
                    PlayExitAndRemove(elem);
            }
        }

        public void Tick(float deltaTime)
        {
            // Iterate by index, backwards so removals don't shift unvisited elements
            for (int i = _activeIds.Count - 1; i >= 0; i--)
            {
                if (_active.TryGetValue(_activeIds[i], out var elem))
                    elem.Tick(deltaTime);
            }
        }

        private void OnElementExpired(int handleId)
        {
            if (_active.TryGetValue(handleId, out var elem))
                PlayExitAndRemove(elem);
        }

        private void OnElementCloseClicked(int handleId)
        {
            if (_active.TryGetValue(handleId, out var elem))
                PlayExitAndRemove(elem);
        }

        private void PlayExitAndRemove(NotificationElement elem)
        {
            int id = elem.HandleId;
            elem.PlayExitAnimation(() =>
            {
                _container.Remove(elem.Root);
                _active.Remove(id);
                _activeIds.Remove(id);
                ReleaseElement(elem);
                OnDismissComplete?.Invoke(id);
            });
        }

        private NotificationElement AcquireElement()
        {
            if (_pool.Count > 0)
            {
                var elem = _pool.Dequeue();
                elem.Root.style.display = DisplayStyle.Flex;
                return elem;
            }

            var root = _template.Instantiate();
            var newElem = new NotificationElement();
            newElem.Initialize(root, "notification-toast--visible", "notification-toast--dismissing");
            return newElem;
        }

        private void ReleaseElement(NotificationElement elem)
        {
            elem.Reset();
            elem.Root.style.display = DisplayStyle.None;
            _pool.Enqueue(elem);
        }

        private static void ApplyPosition(VisualElement container, NotificationPosition pos)
        {
            // Reset all position styles
            container.style.top = StyleKeyword.Auto;
            container.style.bottom = StyleKeyword.Auto;
            container.style.left = StyleKeyword.Auto;
            container.style.right = StyleKeyword.Auto;
            container.style.alignItems = Align.FlexEnd;

            switch (pos)
            {
                case NotificationPosition.TopRight:
                    container.style.top = 16;
                    container.style.right = 16;
                    container.style.alignItems = Align.FlexEnd;
                    break;
                case NotificationPosition.TopLeft:
                    container.style.top = 16;
                    container.style.left = 16;
                    container.style.alignItems = Align.FlexStart;
                    break;
                case NotificationPosition.TopCenter:
                    container.style.top = 16;
                    container.style.left = 0;
                    container.style.right = 0;
                    container.style.alignItems = Align.Center;
                    break;
                case NotificationPosition.BottomRight:
                    container.style.bottom = 16;
                    container.style.right = 16;
                    container.style.alignItems = Align.FlexEnd;
                    break;
                case NotificationPosition.BottomLeft:
                    container.style.bottom = 16;
                    container.style.left = 16;
                    container.style.alignItems = Align.FlexStart;
                    break;
                case NotificationPosition.BottomCenter:
                    container.style.bottom = 16;
                    container.style.left = 0;
                    container.style.right = 0;
                    container.style.alignItems = Align.Center;
                    break;
                case NotificationPosition.Center:
                    container.style.top = 0;
                    container.style.bottom = 0;
                    container.style.left = 0;
                    container.style.right = 0;
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.Center;
                    break;
            }
        }
    }
}
