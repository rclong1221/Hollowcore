using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Map.UI
{
    /// <summary>
    /// EPIC 17.6: Toast popup for map discovery events ("Discovered: Whispering Ruins").
    /// Supports fade-in/out animation and queued notifications.
    /// Registers as IMapNotificationProvider.
    /// </summary>
    public class MapNotificationView : MonoBehaviour, IMapNotificationProvider
    {
        [Header("Toast Elements")]
        [SerializeField] private CanvasGroup _toastGroup;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _subtitleText;
        [SerializeField] private Image _iconImage;

        [Header("Animation")]
        [SerializeField] private float _fadeInDuration = 0.3f;
        [SerializeField] private float _displayDuration = 2.5f;
        [SerializeField] private float _fadeOutDuration = 0.5f;

        private readonly System.Collections.Generic.Queue<(string title, string subtitle)> _queue
            = new System.Collections.Generic.Queue<(string, string)>();
        private bool _isShowing;

        private void Awake()
        {
            if (_toastGroup != null)
                _toastGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            MapUIRegistry.RegisterNotification(this);
        }

        private void OnDisable()
        {
            MapUIRegistry.UnregisterNotification(this);
        }

        public void OnPOIDiscovered(string label, POIType type)
        {
            string subtitle = type switch
            {
                POIType.Town => "Settlement",
                POIType.Dungeon => "Dungeon",
                POIType.BossArena => "Boss Arena",
                POIType.FastTravel => "Fast Travel Point",
                POIType.Landmark => "Landmark",
                POIType.Camp => "Camp",
                POIType.Cave => "Cave",
                POIType.Ruins => "Ruins",
                POIType.Shrine => "Shrine",
                POIType.Vendor => "Vendor",
                _ => "Location"
            };

            EnqueueToast($"Discovered: {label}", subtitle);
        }

        public void OnZoneEntered(string zoneName)
        {
            EnqueueToast(zoneName, "Entering Area");
        }

        private void EnqueueToast(string title, string subtitle)
        {
            _queue.Enqueue((title, subtitle));
            if (!_isShowing)
                StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _isShowing = true;

            while (_queue.Count > 0)
            {
                var (title, subtitle) = _queue.Dequeue();

                if (_titleText != null) _titleText.text = title;
                if (_subtitleText != null) _subtitleText.text = subtitle;

                // Fade in
                yield return FadeToast(0f, 1f, _fadeInDuration);

                // Hold
                yield return new WaitForSeconds(_displayDuration);

                // Fade out
                yield return FadeToast(1f, 0f, _fadeOutDuration);

                // Brief pause between queued toasts
                if (_queue.Count > 0)
                    yield return new WaitForSeconds(0.2f);
            }

            _isShowing = false;
        }

        private IEnumerator FadeToast(float from, float to, float duration)
        {
            if (_toastGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _toastGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            _toastGroup.alpha = to;
        }
    }
}
