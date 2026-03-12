using System;
using System.Collections.Generic;
using DIG.Notifications.Config;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Notifications.Channels
{
    /// <summary>
    /// EPIC 18.3: Center screen channel — centered modal-like panel with optional backdrop.
    /// Typically persists until dismissed by user action.
    /// </summary>
    public class CenterScreenChannel : INotificationChannel
    {
        private readonly NotificationChannelConfig _config;
        private readonly VisualTreeAsset _template;
        private VisualElement _container;

        private NotificationElement _currentElement;
        private int _currentHandleId;
        private readonly Queue<NotificationElement> _pool = new();

        public int ActiveCount => _currentElement != null && !_currentElement.IsAnimatingOut ? 1 : 0;
        public int MaxVisible => _config.MaxVisible;
        public bool HasCapacity => _currentElement == null || _currentElement.IsAnimatingOut;

        public event Action<int> OnDismissComplete;

        public CenterScreenChannel(NotificationChannelConfig config, VisualTreeAsset template)
        {
            _config = config;
            _template = template;
        }

        public void AttachTo(VisualElement parentLayer)
        {
            _container = new VisualElement { name = "notification-center-container" };
            _container.AddToClassList("notification-center-container");
            _container.pickingMode = PickingMode.Ignore;
            parentLayer.Add(_container);
        }

        public int Show(int handleId, NotificationData data, NotificationStyleSO style)
        {
            // If a center notification is already showing, dismiss it first
            if (_currentElement != null && !_currentElement.IsAnimatingOut)
                PlayExitAndRemove(_currentElement);

            var elem = AcquireElement();
            elem.Populate(handleId, data, style, _config.DefaultDuration);
            _container.Add(elem.Root);

            // Enable picking on container when center notification is showing (backdrop blocks input)
            _container.pickingMode = PickingMode.Position;

            _currentElement = elem;
            _currentHandleId = handleId;

            elem.OnExpired += OnElementExpired;
            elem.OnCloseClicked += OnDismissClicked;
            elem.OnActionClicked += id => data.OnAction?.Invoke();

            // Wire dismiss button
            var dismissBtn = elem.Root.Q<Button>("dismiss-btn");
            if (dismissBtn != null)
                dismissBtn.clicked += () => OnDismissClicked(handleId);

            elem.PlayEnterAnimation();
            return handleId;
        }

        public void Dismiss(int handleId)
        {
            if (_currentElement == null || _currentHandleId != handleId || _currentElement.IsAnimatingOut) return;
            PlayExitAndRemove(_currentElement);
        }

        public void Update(int handleId, NotificationData data)
        {
            if (_currentElement == null || _currentHandleId != handleId) return;
            _currentElement.Populate(handleId, data, null, _config.DefaultDuration);
        }

        public void Clear()
        {
            if (_currentElement != null && !_currentElement.IsAnimatingOut)
                PlayExitAndRemove(_currentElement);
        }

        public void Tick(float deltaTime)
        {
            _currentElement?.Tick(deltaTime);
        }

        private void OnElementExpired(int handleId)
        {
            if (_currentElement != null && _currentHandleId == handleId)
                PlayExitAndRemove(_currentElement);
        }

        private void OnDismissClicked(int handleId)
        {
            if (_currentElement != null && _currentHandleId == handleId)
                PlayExitAndRemove(_currentElement);
        }

        private void PlayExitAndRemove(NotificationElement elem)
        {
            int id = _currentHandleId;
            elem.PlayExitAnimation(() =>
            {
                _container.Remove(elem.Root);
                if (_currentElement == elem) _currentElement = null;
                _container.pickingMode = PickingMode.Ignore;
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
            newElem.Initialize(root, "notification-center--visible", "notification-center--dismissing");
            return newElem;
        }

        private void ReleaseElement(NotificationElement elem)
        {
            elem.Reset();
            elem.Root.style.display = DisplayStyle.None;
            _pool.Enqueue(elem);
        }
    }
}
