using UnityEngine;
using UnityEngine.UI;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Optional HUD widget showing "Now Playing" track name on zone transition.
    /// Fades in on track change, fades out after a configurable duration.
    /// Implements IMusicUIProvider for MusicUIBridgeSystem integration.
    /// </summary>
    public class MusicNowPlayingView : MonoBehaviour, IMusicUIProvider
    {
        [SerializeField] private Text _trackNameText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _displayDuration = 4f;
        [SerializeField] private float _fadeInSpeed = 3f;
        [SerializeField] private float _fadeOutSpeed = 1.5f;

        private float _displayTimer;
        private float _targetAlpha;

        private void OnEnable()
        {
            MusicUIRegistry.Register(this);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        private void OnDisable()
        {
            MusicUIRegistry.Unregister(this);
        }

        private void Update()
        {
            if (_canvasGroup == null) return;

            if (_displayTimer > 0f)
            {
                _displayTimer -= Time.deltaTime;
                _targetAlpha = 1f;

                if (_displayTimer <= 0f)
                    _targetAlpha = 0f;
            }

            float speed = _targetAlpha > _canvasGroup.alpha ? _fadeInSpeed : _fadeOutSpeed;
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, speed * Time.deltaTime);
        }

        public void OnTrackChanged(string trackName, MusicTrackCategory category)
        {
            if (_trackNameText != null)
                _trackNameText.text = trackName;

            _displayTimer = _displayDuration;
            _targetAlpha = 1f;
        }

        public void OnCombatIntensityChanged(float intensity) { }

        public void OnStingerPlayed(string stingerName, StingerCategory category) { }
    }
}
